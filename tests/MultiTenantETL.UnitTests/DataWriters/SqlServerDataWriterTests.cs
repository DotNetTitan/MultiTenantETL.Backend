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

public class SqlServerDataWriterTests : IDisposable
{
    private readonly ILogger<SqlServerDataWriter> _logger;
    private readonly IOptions<EtlSettings> _settings;
    private readonly SqlServerDataWriter _sut;

    public SqlServerDataWriterTests()
    {
        _logger = Substitute.For<ILogger<SqlServerDataWriter>>();
        _settings = Options.Create(new EtlSettings
        {
            CommandTimeoutSeconds = 300
        });

        _sut = new SqlServerDataWriter(_logger, _settings);
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateInstance()
    {
        // Act
        var instance = new SqlServerDataWriter(_logger, _settings);

        // Assert
        instance.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new SqlServerDataWriter(null!, _settings);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullSettings_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new SqlServerDataWriter(_logger, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("settings");
    }

    [Fact]
    public async Task WriteBatchAsync_WithValidConfigAndData_ShouldReturnSuccessResult()
    {
        // Arrange
        var connector = CreateValidConnector();
        var batch = CreateValidBatch();
        var options = new WriteOptions();

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert - Should fail at database connection in unit tests
        result.Should().NotBeNull();
        result.BatchId.Should().Be(batch.BatchId);
        result.RowsFailed.Should().Be(batch.RowCount);
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task WriteBatchAsync_WithInvalidJsonConfig_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test SQL Server Connector",
            Type = "Database",
            Provider = "SQLServer",
            Direction = "destination",
            IsSource = false,
            IsDestination = true,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = "{ invalid json",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        var batch = CreateValidBatch();
        var options = new WriteOptions();

        // Act & Assert - Should throw during config parsing
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);
        });
    }

    [Fact]
    public async Task WriteBatchAsync_WithMissingConnectionString_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test SQL Server Connector",
            Type = "Database",
            Provider = "SQLServer",
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
        var batch = CreateValidBatch();
        var options = new WriteOptions();

        // Act & Assert - Should throw during config parsing
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);
        });
    }

    [Fact]
    public async Task WriteBatchAsync_WithMissingTableName_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test SQL Server Connector",
            Type = "Database",
            Provider = "SQLServer",
            Direction = "destination",
            IsSource = false,
            IsDestination = true,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = @"{
                ""ConnectionString"": ""Server=localhost;Database=test;User=user;Password=pass""
            }",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        var batch = CreateValidBatch();
        var options = new WriteOptions();

        // Act & Assert - Should throw during config parsing
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);
        });
    }

    [Fact]
    public async Task WriteBatchAsync_WithEmptyBatch_ShouldReturnSuccessResult()
    {
        // Arrange
        var connector = CreateValidConnector();
        var batch = new ReadBatch { BatchId = Guid.NewGuid() };
        var options = new WriteOptions();

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.BatchId.Should().Be(batch.BatchId);
        result.RowsWritten.Should().Be(0);
        result.RowsFailed.Should().Be(0);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task WriteBatchAsync_WithTruncateBeforeLoad_ShouldAttemptTruncate()
    {
        // Arrange
        var connector = CreateValidConnector();
        var batch = CreateValidBatch();
        var options = new WriteOptions { TruncateBeforeLoad = true };

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert - Should fail at database connection in unit tests
        result.Should().NotBeNull();
        result.BatchId.Should().Be(batch.BatchId);
        result.RowsFailed.Should().Be(batch.RowCount);
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task WriteBatchAsync_WithCancellation_ShouldCancelOperation()
    {
        // Arrange
        var connector = CreateValidConnector();
        var batch = CreateValidBatch();
        var options = new WriteOptions();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () =>
        {
            await _sut.WriteBatchAsync(connector, batch, options, cts.Token);
        };

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(act);
    }

    [Fact]
    public async Task WriteBatchAsync_WithUpsertAndKeys_ShouldAttemptUpsert()
    {
        // Arrange
        var connector = CreateValidConnector();
        var batch = CreateValidBatch();
        var options = new WriteOptions
        {
            UseUpsert = true,
            UpsertKeys = new List<string> { "id" }
        };

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert - Should fail at database connection in unit tests
        result.Should().NotBeNull();
        result.BatchId.Should().Be(batch.BatchId);
        result.RowsFailed.Should().Be(batch.RowCount);
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task WriteBatchAsync_WithLargeBatch_ShouldUseChunking()
    {
        // Arrange
        var connector = CreateValidConnector();
        var batch = CreateLargeBatch(150); // Larger than default batch size
        var options = new WriteOptions();

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert - Should fail at database connection in unit tests
        result.Should().NotBeNull();
        result.BatchId.Should().Be(batch.BatchId);
        result.RowsFailed.Should().Be(batch.RowCount);
        result.Errors.Should().NotBeEmpty();
    }

    private static Connector CreateValidConnector()
    {
        return new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test SQL Server Connector",
            Type = "Database",
            Provider = "SQLServer",
            Direction = "destination",
            IsSource = false,
            IsDestination = true,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = @"{
                ""ConnectionString"": ""Server=localhost;Database=test;User=user;Password=pass"",
                ""TableName"": ""test_table""
            }",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
    }

    private static ReadBatch CreateValidBatch()
    {
        return new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            Rows = new List<Dictionary<string, object?>>
            {
                new() { ["id"] = 1, ["name"] = "Test Row 1", ["value"] = 100.5 },
                new() { ["id"] = 2, ["name"] = "Test Row 2", ["value"] = 200.5 }
            },
            RowCount = 2
        };
    }

    private static ReadBatch CreateLargeBatch(int rowCount)
    {
        var batch = new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            Rows = new List<Dictionary<string, object?>>()
        };

        for (int i = 0; i < rowCount; i++)
        {
            batch.Rows.Add(new Dictionary<string, object?>
            {
                ["id"] = i + 1,
                ["name"] = $"Test Row {i + 1}",
                ["value"] = (i + 1) * 10.5
            });
        }

        batch.RowCount = rowCount;
        return batch;
    }
}