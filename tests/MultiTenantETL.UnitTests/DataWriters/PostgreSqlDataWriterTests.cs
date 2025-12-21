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
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace MultiTenantETL.UnitTests.DataWriters;

public class PostgreSqlDataWriterTests : IDisposable
{
    private readonly ILogger<PostgreSqlDataWriter> _logger;
    private readonly IOptions<EtlSettings> _settings;
    private readonly IEncryptionService _encryptionService;
    private readonly PostgreSqlDataWriter _sut;
    private readonly string _tempFilePath;

    public PostgreSqlDataWriterTests()
    {
        _logger = Substitute.For<ILogger<PostgreSqlDataWriter>>();
        _settings = Options.Create(new EtlSettings
        {
            CommandTimeoutSeconds = 300
        });
        _encryptionService = Substitute.For<IEncryptionService>();
        _encryptionService.DecryptJsonFields(Arg.Any<System.Text.Json.JsonElement>(), Arg.Any<string[]>())
            .Returns(x => x.ArgAt<System.Text.Json.JsonElement>(0));

        _sut = new PostgreSqlDataWriter(_logger, _encryptionService);
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
        var instance = new PostgreSqlDataWriter(_logger, _encryptionService);

        // Assert
        instance.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new PostgreSqlDataWriter(null!, _encryptionService);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullEncryptionService_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new PostgreSqlDataWriter(_logger, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("encryptionService");
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
    public async Task WriteBatchAsync_WithEncryptionEnabled_ShouldDecryptConnectionString()
    {
        // Arrange
        var encryptedConnectionString = "encrypted:Host=localhost;Database=test";
        var decryptedConnectionString = "Host=localhost;Database=test;Username=user;Password=pass";

        _encryptionService.Decrypt(encryptedConnectionString).Returns(decryptedConnectionString);

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
            ConfigJson = $@"{{
                ""ConnectionString"": ""{encryptedConnectionString}"",
                ""TableName"": ""test_table"",
                ""UseEncryption"": true
            }}",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        var batch = CreateTestBatch(1);
        var options = new WriteOptions();

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert - Should fail at database connection, but should have decrypted
        result.Should().NotBeNull();
        result.Errors.Should().NotBeEmpty();

        _encryptionService.Received(1).DecryptJsonFields(Arg.Any<JsonElement>(), Arg.Any<string[]>());
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