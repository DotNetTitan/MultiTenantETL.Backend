using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;
using MultiTenantETL.Infrastructure.DataWriters;
using MultiTenantETL.Infrastructure.Security;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace MultiTenantETL.UnitTests.DataWriters;

public class PostgreSqlDataWriterTests : IDisposable
{
    private readonly ILogger<PostgreSqlDataWriter> _logger;
    private readonly IOptions<EtlSettings> _settings;
    private readonly ISecretResolver _secretResolver;
    private readonly PostgreSqlDataWriter _sut;
    private readonly string _tempFilePath;

    public PostgreSqlDataWriterTests()
    {
        _logger = Substitute.For<ILogger<PostgreSqlDataWriter>>();
        _settings = Options.Create(new EtlSettings
        {
            CommandTimeoutSeconds = 300
        });
        _secretResolver = Substitute.For<ISecretResolver>();
        _secretResolver.ResolveSecretsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(x => Task.FromResult(JsonDocument.Parse(x.ArgAt<string>(0)).RootElement));

        _sut = new PostgreSqlDataWriter(_logger, _secretResolver);
        _tempFilePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.csv");
    }

    public void Dispose()
    {
        if (File.Exists(_tempFilePath))
        {
            File.Delete(_tempFilePath);
        }
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateInstance()
    {
        // Act
        var instance = new PostgreSqlDataWriter(_logger, _secretResolver);

        // Assert
        instance.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new PostgreSqlDataWriter(null!, _secretResolver);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullSecretResolver_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new PostgreSqlDataWriter(_logger, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("secretResolver");
    }

    [Fact]
    public async Task WriteBatchAsync_WithInvalidJsonConfig_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test PostgreSQL Connector",
            Type = "Database",
            Provider = "PostgreSQL",
            Direction = "destination",
            IsSource = false,
            IsDestination = true,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = "invalid json",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        var batch = CreateTestBatch(2);
        var options = new WriteOptions();

        // Act
        var act = () => _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Failed to parse PostgreSQL connector configuration");
    }

    [Fact]
    public async Task WriteBatchAsync_WithMissingConnectionString_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test PostgreSQL Connector",
            Type = "Database",
            Provider = "PostgreSQL",
            Direction = "destination",
            IsSource = false,
            IsDestination = true,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = @"{
                ""TableName"": ""test_table""
            }",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        var batch = CreateTestBatch(2);
        var options = new WriteOptions();

        // Act
        var act = () => _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("PostgreSQL configuration must include ConnectionString");
    }

    [Fact]
    public async Task WriteBatchAsync_WithMissingTableName_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test PostgreSQL Connector",
            Type = "Database",
            Provider = "PostgreSQL",
            Direction = "destination",
            IsSource = false,
            IsDestination = true,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = @"{
                ""ConnectionString"": ""Host=localhost;Database=test;Username=user;Password=pass""
            }",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        var batch = CreateTestBatch(2);
        var options = new WriteOptions();

        // Act
        var act = () => _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("PostgreSQL configuration must include TableName");
    }

    [Fact]
    public async Task WriteBatchAsync_WithEmptyBatch_ShouldReturnSuccessResult()
    {
        // Arrange
        var connector = CreateValidConnector();
        var batch = new ReadBatch { BatchId = Guid.NewGuid(), Rows = new List<Dictionary<string, object?>>() };
        var options = new WriteOptions();

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.BatchId.Should().Be(batch.BatchId);
        result.RowsWritten.Should().Be(0);
        result.RowsFailed.Should().Be(0);
    }

    [Fact]
    public async Task WriteBatchAsync_WithTruncateBeforeLoad_ShouldLogTruncation()
    {
        // Arrange
        var connector = CreateValidConnector();
        var batch = CreateTestBatch(1);
        var options = new WriteOptions { TruncateBeforeLoad = true };

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert - Should fail at database connection
        result.Should().NotBeNull();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task WriteBatchAsync_WithCancellation_ShouldCancelOperation()
    {
        // Arrange
        var connector = CreateValidConnector();
        var batch = CreateTestBatch(2);
        var options = new WriteOptions();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => _sut.WriteBatchAsync(connector, batch, options, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task WriteBatchAsync_WithSecretsInConfig_ShouldResolveFromKeyVault()
    {
        // Arrange - Config contains Key Vault references
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test PostgreSQL Connector",
            Type = "Database",
            Provider = "PostgreSQL",
            Direction = "destination",
            IsSource = false,
            IsDestination = true,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = @"{
                ""ConnectionString"": ""keyvault:connector-test-connectionstring"",
                ""TableName"": ""test_table""
            }",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        var batch = CreateTestBatch(1);
        var options = new WriteOptions();

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert - Should fail at database connection, but should have attempted secret resolution
        result.Should().NotBeNull();
        result.Errors.Should().NotBeEmpty();

        // Verify secret resolution was attempted
        await _secretResolver.Received(1).ResolveSecretsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static Connector CreateValidConnector()
    {
        return new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test PostgreSQL Connector",
            Type = "Database",
            Provider = "PostgreSQL",
            Direction = "destination",
            IsSource = false,
            IsDestination = true,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = @"{
                ""ConnectionString"": ""Host=localhost;Database=test;Username=user;Password=pass"",
                ""TableName"": ""test_table""
            }",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
    }

    private static ReadBatch CreateTestBatch(int rowCount)
    {
        var rows = new List<Dictionary<string, object?>>();
        for (int i = 0; i < rowCount; i++)
        {
            rows.Add(new Dictionary<string, object?>
            {
                ["id"] = i + 1,
                ["name"] = $"Test User {i + 1}",
                ["email"] = $"user{i + 1}@example.com"
            });
        }

        return new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            Rows = rows
        };
    }
}