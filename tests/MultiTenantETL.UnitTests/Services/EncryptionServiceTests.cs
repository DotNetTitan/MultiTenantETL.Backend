using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Infrastructure.Security;
using NSubstitute;

namespace MultiTenantETL.UnitTests.Services;

public class EncryptionServiceTests
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EncryptionService> _logger;
    private readonly EncryptionService _sut; // System Under Test

    public EncryptionServiceTests()
    {
        // Arrange - Set up test configuration with encryption key
        var inMemorySettings = new Dictionary<string, string>
        {
            {"Encryption:Key", "test-encryption-key-for-unit-tests-12345"},
            {"Encryption:Salt", "test-salt-for-unit-tests-67890"}
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        _logger = Substitute.For<ILogger<EncryptionService>>();
        _sut = new EncryptionService(_configuration, _logger);
    }

    [Fact]
    public void Constructor_WithoutEncryptionKey_ThrowsInvalidOperationException()
    {
        // Arrange
        var emptyConfig = new ConfigurationBuilder().Build();

        // Act
        Action act = () => new EncryptionService(emptyConfig, _logger);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Encryption key not configured*");
    }

    [Fact]
    public void Encrypt_ValidString_ReturnsNonEmptyEncryptedValue()
    {
        // Arrange
        var plainText = "my-secret-password";

        // Act
        var encrypted = _sut.Encrypt(plainText);

        // Assert
        encrypted.Should().NotBeNullOrEmpty();
        encrypted.Should().NotBe(plainText);
    }

    [Fact]
    public void Decrypt_EncryptedString_ReturnsOriginalValue()
    {
        // Arrange
        var originalText = "my-secret-password";
        var encrypted = _sut.Encrypt(originalText);

        // Act
        var decrypted = _sut.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(originalText);
    }

    [Fact]
    public void EncryptDecrypt_RoundTrip_PreservesOriginalValue()
    {
        // Arrange
        var testCases = new[]
        {
            "simple text",
            "Text with 123 numbers!",
            "Special chars: @#$%^&*()",
            "Multi\nLine\nText",
            "Unicode: 你好世界 🌍",
            ""
        };

        foreach (var testCase in testCases)
        {
            // Act
            var encrypted = _sut.Encrypt(testCase);
            var decrypted = _sut.Decrypt(encrypted);

            // Assert
            decrypted.Should().Be(testCase, $"round trip should preserve '{testCase}'");
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Encrypt_NullOrEmpty_ReturnsInput(string? input)
    {
        // Act
        var result = _sut.Encrypt(input!);

        // Assert
        result.Should().Be(input);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Decrypt_NullOrEmpty_ReturnsInput(string? input)
    {
        // Act
        var result = _sut.Decrypt(input!);

        // Assert
        result.Should().Be(input);
    }

    [Fact]
    public void Encrypt_SameInput_ProducesDifferentOutputs()
    {
        // Arrange
        var plainText = "same-input-text";

        // Act - Encrypt the same text multiple times
        var encrypted1 = _sut.Encrypt(plainText);
        var encrypted2 = _sut.Encrypt(plainText);
        var encrypted3 = _sut.Encrypt(plainText);

        // Assert - Each encryption should be different due to random IV
        encrypted1.Should().NotBe(encrypted2);
        encrypted2.Should().NotBe(encrypted3);
        encrypted1.Should().NotBe(encrypted3);

        // But all should decrypt to the same original value
        _sut.Decrypt(encrypted1).Should().Be(plainText);
        _sut.Decrypt(encrypted2).Should().Be(plainText);
        _sut.Decrypt(encrypted3).Should().Be(plainText);
    }

    [Fact]
    public void EncryptJsonFields_SpecifiedFields_EncryptsCorrectly()
    {
        // Arrange
        var jsonString = """
            {
                "username": "admin",
                "password": "secret123",
                "email": "admin@example.com",
                "apiKey": "key-12345"
            }
            """;
        var jsonElement = JsonDocument.Parse(jsonString).RootElement;

        // Act
        var encrypted = _sut.EncryptJsonFields(jsonElement, "password", "apiKey");

        // Assert
        encrypted.GetProperty("username").GetString().Should().Be("admin");
        encrypted.GetProperty("email").GetString().Should().Be("admin@example.com");
        encrypted.GetProperty("password").GetString().Should().NotBe("secret123");
        encrypted.GetProperty("apiKey").GetString().Should().NotBe("key-12345");
    }

    [Fact]
    public void DecryptJsonFields_EncryptedFields_DecryptsCorrectly()
    {
        // Arrange
        var jsonString = """
            {
                "username": "admin",
                "password": "secret123",
                "email": "admin@example.com",
                "apiKey": "key-12345"
            }
            """;
        var jsonElement = JsonDocument.Parse(jsonString).RootElement;
        var encrypted = _sut.EncryptJsonFields(jsonElement, "password", "apiKey");

        // Act
        var decrypted = _sut.DecryptJsonFields(encrypted, "password", "apiKey");

        // Assert
        decrypted.GetProperty("username").GetString().Should().Be("admin");
        decrypted.GetProperty("email").GetString().Should().Be("admin@example.com");
        decrypted.GetProperty("password").GetString().Should().Be("secret123");
        decrypted.GetProperty("apiKey").GetString().Should().Be("key-12345");
    }

    [Fact]
    public void EncryptJsonFields_CaseInsensitive_EncryptsCorrectly()
    {
        // Arrange
        var jsonString = """
            {
                "Password": "secret123",
                "ApiKey": "key-12345"
            }
            """;
        var jsonElement = JsonDocument.Parse(jsonString).RootElement;

        // Act - Using lowercase field names
        var encrypted = _sut.EncryptJsonFields(jsonElement, "password", "apikey");

        // Assert - Should encrypt regardless of case
        encrypted.GetProperty("Password").GetString().Should().NotBe("secret123");
        encrypted.GetProperty("ApiKey").GetString().Should().NotBe("key-12345");
    }

    [Fact]
    public void EncryptJsonFields_NoFieldsSpecified_ReturnsOriginal()
    {
        // Arrange
        var jsonString = """
            {
                "username": "admin",
                "password": "secret123"
            }
            """;
        var jsonElement = JsonDocument.Parse(jsonString).RootElement;

        // Act
        var result = _sut.EncryptJsonFields(jsonElement);

        // Assert
        result.GetProperty("username").GetString().Should().Be("admin");
        result.GetProperty("password").GetString().Should().Be("secret123");
    }

    [Fact]
    public void EncryptJsonFields_NullOrEmptyFieldValues_HandlesCorrectly()
    {
        // Arrange
        var jsonString = """
            {
                "username": "admin",
                "password": "",
                "apiKey": null
            }
            """;
        var jsonElement = JsonDocument.Parse(jsonString).RootElement;

        // Act
        var encrypted = _sut.EncryptJsonFields(jsonElement, "password", "apiKey");

        // Assert - Should not throw, empty/null should remain unchanged
        encrypted.GetProperty("username").GetString().Should().Be("admin");
        encrypted.GetProperty("password").GetString().Should().BeEmpty();
        encrypted.GetProperty("apiKey").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void EncryptJsonFields_ComplexJson_PreservesStructure()
    {
        // Arrange
        var jsonString = """
            {
                "name": "Test Connector",
                "config": {
                    "host": "localhost",
                    "port": 5432,
                    "password": "secret"
                },
                "tags": ["tag1", "tag2"],
                "enabled": true
            }
            """;
        var jsonElement = JsonDocument.Parse(jsonString).RootElement;

        // Act
        var encrypted = _sut.EncryptJsonFields(jsonElement, "password");

        // Assert
        encrypted.GetProperty("name").GetString().Should().Be("Test Connector");
        encrypted.GetProperty("enabled").GetBoolean().Should().BeTrue();
        
        // Note: The current implementation only encrypts top-level fields
        // Nested "password" won't be encrypted
        var config = encrypted.GetProperty("config");
        config.GetProperty("host").GetString().Should().Be("localhost");
    }
}
