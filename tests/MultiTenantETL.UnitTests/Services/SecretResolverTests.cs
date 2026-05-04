using FluentAssertions;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Infrastructure.Security;
using NSubstitute;
using System.Text.Json;

namespace MultiTenantETL.UnitTests.Services;

#pragma warning disable CS8620 // Argument of type cannot be used due to differences in nullability

public class SecretResolverTests
{
    private readonly ISecretStorageService _secretStorageService;
    private readonly ILogger<SecretResolver> _logger;
    private readonly SecretResolver _sut;

    public SecretResolverTests()
    {
        _secretStorageService = Substitute.For<ISecretStorageService>();
        _logger = Substitute.For<ILogger<SecretResolver>>();
        _sut = new SecretResolver(_secretStorageService, _logger);
    }

    [Fact]
    public async Task ResolveSecretsAsync_NoSecretReferences_ReturnsOriginalJson()
    {
        // Arrange
        var json = @"{
            ""host"": ""localhost"",
            ""port"": 5432,
            ""database"": ""mydb""
        }";

        // Act
        var result = await _sut.ResolveSecretsAsync(json);

        // Assert
        result.GetProperty("host").GetString().Should().Be("localhost");
        result.GetProperty("port").GetInt32().Should().Be(5432);
        result.GetProperty("database").GetString().Should().Be("mydb");

        // Should not call Key Vault when no references exist
        await _secretStorageService.DidNotReceive().GetSecretAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task ResolveSecretsAsync_WithSecretReference_ResolvesToActualValue()
    {
        // Arrange
        var json = @"{
            ""host"": ""localhost"",
            ""password"": ""keyvault:connector-test-secret-password""
        }";

        _secretStorageService.GetSecretAsync("connector-test-secret-password")
            .Returns(Task.FromResult("actual-secret-value"));

        // Act
        var result = await _sut.ResolveSecretsAsync(json);

        // Assert
        result.GetProperty("host").GetString().Should().Be("localhost");
        result.GetProperty("password").GetString().Should().Be("actual-secret-value");

        await _secretStorageService.Received(1).GetSecretAsync("connector-test-secret-password");
    }

    [Fact]
    public async Task ResolveSecretsAsync_MultipleSecretReferences_ResolvesAll()
    {
        // Arrange
        var json = @"{
            ""username"": ""keyvault:connector-user"",
            ""password"": ""keyvault:connector-pass"",
            ""apiKey"": ""keyvault:connector-key"",
            ""host"": ""localhost""
        }";

        _secretStorageService.GetSecretAsync("connector-user")
            .Returns(Task.FromResult("my-username"));
        _secretStorageService.GetSecretAsync("connector-pass")
            .Returns(Task.FromResult("my-password"));
        _secretStorageService.GetSecretAsync("connector-key")
            .Returns(Task.FromResult("my-api-key"));

        // Act
        var result = await _sut.ResolveSecretsAsync(json);

        // Assert
        result.GetProperty("username").GetString().Should().Be("my-username");
        result.GetProperty("password").GetString().Should().Be("my-password");
        result.GetProperty("apiKey").GetString().Should().Be("my-api-key");
        result.GetProperty("host").GetString().Should().Be("localhost");

        await _secretStorageService.Received(3).GetSecretAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task ResolveSecretsAsync_NestedObjects_ResolvesReferences()
    {
        // Arrange
        var json = @"{
            ""database"": {
                ""host"": ""localhost"",
                ""credentials"": {
                    ""username"": ""admin"",
                    ""password"": ""keyvault:db-password""
                }
            }
        }";

        _secretStorageService.GetSecretAsync("db-password")
            .Returns(Task.FromResult("secret-password"));

        // Act
        var result = await _sut.ResolveSecretsAsync(json);

        // Assert
        var credentials = result.GetProperty("database").GetProperty("credentials");
        credentials.GetProperty("username").GetString().Should().Be("admin");
        credentials.GetProperty("password").GetString().Should().Be("secret-password");
    }

    [Fact]
    public async Task ResolveSecretsAsync_ArrayValues_ResolvesReferences()
    {
        // Arrange
        var json = @"{
            ""servers"": [
                {
                    ""host"": ""server1"",
                    ""apiKey"": ""keyvault:server1-key""
                },
                {
                    ""host"": ""server2"",
                    ""apiKey"": ""keyvault:server2-key""
                }
            ]
        }";

        _secretStorageService.GetSecretAsync("server1-key")
            .Returns(Task.FromResult("key1"));
        _secretStorageService.GetSecretAsync("server2-key")
            .Returns(Task.FromResult("key2"));

        // Act
        var result = await _sut.ResolveSecretsAsync(json);

        // Assert
        var servers = result.GetProperty("servers").EnumerateArray().ToList();
        servers[0].GetProperty("apiKey").GetString().Should().Be("key1");
        servers[1].GetProperty("apiKey").GetString().Should().Be("key2");
    }

    [Fact]
    public async Task ResolveSecretsAsync_NonStringValues_PreservesOriginalValue()
    {
        // Arrange
        var json = @"{
            ""port"": 5432,
            ""enabled"": true,
            ""timeout"": 30.5,
            ""password"": ""keyvault:secret""
        }";

        _secretStorageService.GetSecretAsync("secret")
            .Returns(Task.FromResult("resolved-secret"));

        // Act
        var result = await _sut.ResolveSecretsAsync(json);

        // Assert
        result.GetProperty("port").GetInt32().Should().Be(5432);
        result.GetProperty("enabled").GetBoolean().Should().BeTrue();
        result.GetProperty("timeout").GetDouble().Should().Be(30.5);
        result.GetProperty("password").GetString().Should().Be("resolved-secret");
    }

    [Fact]
    public async Task ResolveSecretsAsync_SecretNotFound_ThrowsException()
    {
        // Arrange
        var json = @"{""password"": ""keyvault:missing-secret""}";

        _secretStorageService.GetSecretAsync("missing-secret")
            .Returns(Task.FromException<string>(new KeyNotFoundException("Secret not found")));

        // Act
        Func<Task> act = async () => await _sut.ResolveSecretsAsync(json);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task ResolveSecretsAsync_EmptyOrNullJson_ThrowsArgumentException(string? invalidJson)
    {
        // Act
        Func<Task> act = async () => await _sut.ResolveSecretsAsync(invalidJson!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*configJson*");
    }

    [Fact]
    public async Task ResolveSecretsAsync_InvalidJson_ThrowsJsonException()
    {
        // Arrange
        var invalidJson = "{invalid json}";

        // Act
        Func<Task> act = async () => await _sut.ResolveSecretsAsync(invalidJson);

        // Assert
        await act.Should().ThrowAsync<JsonException>();
    }

    [Fact]
    public async Task ResolveSecretsAsync_PartiallyResolvedReferences_ResolvesOnlyPrefixedValues()
    {
        // Arrange
        var json = @"{
            ""secretField"": ""keyvault:my-secret"",
            ""plainField"": ""not-a-secret-value"",
            ""anotherPlain"": ""keyvault-but-not-prefixed""
        }";

        _secretStorageService.GetSecretAsync("my-secret")
            .Returns(Task.FromResult("resolved-value"));

        // Act
        var result = await _sut.ResolveSecretsAsync(json);

        // Assert
        result.GetProperty("secretField").GetString().Should().Be("resolved-value");
        result.GetProperty("plainField").GetString().Should().Be("not-a-secret-value");
        result.GetProperty("anotherPlain").GetString().Should().Be("keyvault-but-not-prefixed");

        // Should only resolve properly prefixed references
        await _secretStorageService.Received(1).GetSecretAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task ResolveSecretsAsync_EmptyStringValue_PreservesEmptyString()
    {
        // Arrange
        var json = @"{
            ""emptyField"": """",
            ""password"": ""keyvault:secret""
        }";

        _secretStorageService.GetSecretAsync("secret")
            .Returns(Task.FromResult("my-secret"));

        // Act
        var result = await _sut.ResolveSecretsAsync(json);

        // Assert
        result.GetProperty("emptyField").GetString().Should().BeEmpty();
        result.GetProperty("password").GetString().Should().Be("my-secret");
    }

    [Fact]
    public async Task ResolveSecretsAsync_NullValues_PreservesNull()
    {
        // Arrange
        var json = @"{
            ""nullField"": null,
            ""password"": ""keyvault:secret""
        }";

        _secretStorageService.GetSecretAsync("secret")
            .Returns(Task.FromResult("my-secret"));

        // Act
        var result = await _sut.ResolveSecretsAsync(json);

        // Assert
        result.GetProperty("nullField").ValueKind.Should().Be(JsonValueKind.Null);
        result.GetProperty("password").GetString().Should().Be("my-secret");
    }

    [Fact]
    public void Constructor_WithNullSecretStorage_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new SecretResolver(null!, _logger);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("secretStorageService");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new SecretResolver(_secretStorageService, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }
}
