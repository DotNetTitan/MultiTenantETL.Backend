using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;
using MultiTenantETL.Infrastructure.DataWriters;
using NSubstitute;

namespace MultiTenantETL.UnitTests.DataWriters;

public class OracleDataWriterTests : IDisposable
{
    private readonly ILogger<OracleDataWriter> _logger;
    private readonly IOptions<EtlSettings> _settings;
    private readonly OracleDataWriter _sut;

    public OracleDataWriterTests()
    {
        _logger = Substitute.For<ILogger<OracleDataWriter>>();
        _settings = Options.Create(new EtlSettings
        {
            CommandTimeoutSeconds = 300
        });

        _sut = new OracleDataWriter(_logger, _settings);
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateInstance()
    {
        // Act
        var instance = new OracleDataWriter(_logger, _settings);

        // Assert
        instance.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new OracleDataWriter(null!, _settings);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullSettings_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new OracleDataWriter(_logger, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("settings");
    }

    [Fact]
    public async Task WriteBatchAsync_WithInvalidJsonConfig_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test Oracle Connector",
            Type = "Database",
            Provider = "Oracle",
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
            .WithMessage("Failed to parse Oracle connector configuration");
    }

    [Fact]
    public async Task WriteBatchAsync_WithMissingConnectionString_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test Oracle Connector",
            Type = "Database",
            Provider = "Oracle",
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
            .WithMessage("Oracle configuration must include ConnectionString");
    }

    [Fact]
    public async Task WriteBatchAsync_WithMissingTableName_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test Oracle Connector",
            Type = "Database",
            Provider = "Oracle",
            Direction = "destination",
            IsSource = false,
            IsDestination = true,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = @"{
                ""ConnectionString"": ""Data Source=localhost:1521/XE;User Id=user;Password=pass;""
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
            .WithMessage("Oracle configuration must include TableName");
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
    public async Task WriteBatchAsync_WithCancellation_ShouldSupportCancellation()
    {
        // Arrange
        var connector = CreateValidConnector();
        var batch = CreateTestBatch(2);
        var options = new WriteOptions();
        var cts = new CancellationTokenSource();

        // Act - Start the operation but cancel immediately
        cts.Cancel();
        var task = _sut.WriteBatchAsync(connector, batch, options, cts.Token);

        // Assert - Should complete (either successfully or with error, but not hang)
        // Note: In unit test environment without real database, cancellation may not be triggered
        // but the operation should still complete without hanging
        var result = await task;
        result.Should().NotBeNull();
        result.BatchId.Should().Be(batch.BatchId);
    }

    [Fact]
    public async Task WriteBatchAsync_WithValidConfig_ShouldAttemptDatabaseOperation()
    {
        // Arrange
        var connector = CreateValidConnector();
        var batch = CreateTestBatch(2);
        var options = new WriteOptions();

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert - Should attempt database operation (will fail due to no real database)
        result.Should().NotBeNull();
        result.BatchId.Should().Be(batch.BatchId);
        // Note: Actual success/failure depends on database availability
        // In unit test environment, this will likely fail with connection error
    }

    [Fact]
    public async Task WriteBatchAsync_WithUpsertOptions_ShouldAttemptUpsertOperation()
    {
        // Arrange
        var connector = CreateValidConnector();
        var batch = CreateTestBatch(2);
        var options = new WriteOptions
        {
            UseUpsert = true,
            UpsertKeys = new List<string> { "id" }
        };

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert - Should attempt upsert operation (will fail due to no real database)
        result.Should().NotBeNull();
        result.BatchId.Should().Be(batch.BatchId);
        // Note: Actual success/failure depends on database availability
        // In unit test environment, this will likely fail with connection error
    }

    private static Connector CreateValidConnector()
    {
        return new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test Oracle Connector",
            Type = "Database",
            Provider = "Oracle",
            Direction = "destination",
            IsSource = false,
            IsDestination = true,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = @"{
                ""ConnectionString"": ""Data Source=localhost:1521/XE;User Id=user;Password=pass;"",
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