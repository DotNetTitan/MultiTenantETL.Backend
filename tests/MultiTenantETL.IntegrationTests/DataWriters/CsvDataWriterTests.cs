using FluentAssertions;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.DataWriters;

namespace MultiTenantETL.IntegrationTests.DataWriters;

public class CsvDataWriterTests : IAsyncDisposable
{
    private readonly CsvDataWriter _writer;
    private readonly string _testFilePath;

    public CsvDataWriterTests()
    {
        var logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<CsvDataWriter>();

        _writer = new CsvDataWriter(logger);
        _testFilePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.csv");
    }

    public async ValueTask DisposeAsync()
    {
        await _writer.DisposeAsync();

        if (File.Exists(_testFilePath))
            File.Delete(_testFilePath);
    }

    [Fact]
    public async Task WriteBatchAsync_ShouldCreateFileWithHeader()
    {
        // Arrange
        var connector = CreateConnector();
        var batch = CreateTestBatch(5);
        var options = new WriteOptions { TruncateBeforeLoad = false };

        // Act
        var result = await _writer.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        result.RowsWritten.Should().Be(5);
        result.RowsFailed.Should().Be(0);

        File.Exists(_testFilePath).Should().BeTrue();
        var lines = File.ReadAllLines(_testFilePath);
        lines.Should().HaveCount(6); // Header + 5 rows
        lines[0].Should().Be("id,name,email,age");
        lines[1].Should().Contain("User 1");
    }

    [Fact]
    public async Task WriteBatchAsync_ShouldAppendToExistingFile()
    {
        // Arrange
        var connector = CreateConnector();
        var batch1 = CreateTestBatch(3);
        var batch2 = CreateTestBatch(2, startId: 10);
        var options = new WriteOptions { TruncateBeforeLoad = false };

        // Act
        await _writer.WriteBatchAsync(connector, batch1, options, CancellationToken.None);
        await _writer.WriteBatchAsync(connector, batch2, options, CancellationToken.None);

        // Assert
        var lines = File.ReadAllLines(_testFilePath);
        lines.Should().HaveCount(6); // Header + 3 + 2 rows
        lines[0].Should().Be("id,name,email,age");
        lines[4].Should().Contain("User 10");
    }

    [Fact]
    public async Task WriteBatchAsync_ShouldTruncateFile_WhenTruncateBeforeLoadIsTrue()
    {
        // Arrange
        var connector = CreateConnector();
        var batch1 = CreateTestBatch(3);
        var batch2 = CreateTestBatch(2, startId: 10);

        await _writer.WriteBatchAsync(connector, batch1, new WriteOptions(), CancellationToken.None);

        // Act
        var result = await _writer.WriteBatchAsync(connector, batch2,
            new WriteOptions { TruncateBeforeLoad = true }, CancellationToken.None);

        // Assert
        result.RowsWritten.Should().Be(2);

        var lines = File.ReadAllLines(_testFilePath);
        lines.Should().HaveCount(3); // Header + 2 rows (batch1 was truncated)
        lines[1].Should().Contain("User 10");
    }

    [Fact]
    public async Task WriteBatchAsync_ShouldHandleLargeBatch()
    {
        // Arrange
        var connector = CreateConnector();
        var batch = CreateTestBatch(10000);
        var options = new WriteOptions();

        // Act
        var result = await _writer.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        result.RowsWritten.Should().Be(10000);
        result.RowsFailed.Should().Be(0);

        var lines = File.ReadAllLines(_testFilePath);
        lines.Should().HaveCount(10001); // Header + 10000 rows
    }

    [Fact]
    public async Task WriteBatchAsync_ShouldEscapeCommasInFields()
    {
        // Arrange
        var connector = CreateConnector();
        var batch = new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            Rows = new List<Dictionary<string, object?>>
            {
                new() { ["id"] = 1, ["name"] = "Smith, John", ["email"] = "john@test.com", ["age"] = 30 },
                new() { ["id"] = 2, ["name"] = "Doe, Jane", ["email"] = "jane@test.com", ["age"] = 25 }
            },
            RowCount = 2
        };
        var options = new WriteOptions();

        // Act
        await _writer.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        var content = File.ReadAllText(_testFilePath);
        content.Should().Contain("\"Smith, John\"");
        content.Should().Contain("\"Doe, Jane\"");
    }

    [Fact]
    public async Task WriteBatchAsync_ShouldHandleNullValues()
    {
        // Arrange
        var connector = CreateConnector();
        var batch = new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            Rows = new List<Dictionary<string, object?>>
            {
                new() { ["id"] = 1, ["name"] = "John", ["email"] = null, ["age"] = 30 },
                new() { ["id"] = 2, ["name"] = null, ["email"] = "jane@test.com", ["age"] = null }
            },
            RowCount = 2
        };
        var options = new WriteOptions();

        // Act
        var result = await _writer.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        result.RowsWritten.Should().Be(2);

        var lines = File.ReadAllLines(_testFilePath);
        lines[1].Should().Contain(",,"); // null email
        lines[2].Should().StartWith("2,"); // null name
    }

    [Fact]
    public async Task WriteBatchAsync_ShouldHandleEmptyBatch()
    {
        // Arrange
        var connector = CreateConnector();
        var batch = new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            Rows = new List<Dictionary<string, object?>>(),
            RowCount = 0
        };
        var options = new WriteOptions();

        // Act
        var result = await _writer.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        result.RowsWritten.Should().Be(0);
        result.RowsFailed.Should().Be(0);
    }

    [Fact]
    public async Task WriteBatchAsync_WithCustomDelimiter_ShouldUseSemicolon()
    {
        // Arrange
        var connector = CreateConnectorWithDelimiter(";");
        var batch = CreateTestBatch(2);
        var options = new WriteOptions();

        // Act
        await _writer.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        var lines = File.ReadAllLines(_testFilePath);
        lines[0].Should().Be("id;name;email;age");
        lines[1].Should().Contain(";");
        lines[1].Should().NotContain(",");
    }

    private Connector CreateConnector()
    {
        return new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test CSV",
            Type = "File",
            Provider = "CSV",
            Direction = "destination",
            IsSource = false,
            IsDestination = true,
            RequiresCredentials = false,
            IsActive = true,
            ConfigJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                FilePath = _testFilePath,
                HasHeader = true,
                Delimiter = ","
            }),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
    }

    private Connector CreateConnectorWithDelimiter(string delimiter)
    {
        return new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test CSV",
            Type = "File",
            Provider = "CSV",
            Direction = "destination",
            IsSource = false,
            IsDestination = true,
            RequiresCredentials = false,
            IsActive = true,
            ConfigJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                FilePath = _testFilePath,
                HasHeader = true,
                Delimiter = delimiter
            }),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
    }

    private ReadBatch CreateTestBatch(int rowCount, int startId = 1)
    {
        var rows = new List<Dictionary<string, object?>>();

        for (int i = 0; i < rowCount; i++)
        {
            var id = startId + i;
            rows.Add(new Dictionary<string, object?>
            {
                ["id"] = id,
                ["name"] = $"User {id}",
                ["email"] = $"user{id}@test.com",
                ["age"] = 20 + (i % 50)
            });
        }

        return new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            Rows = rows,
            RowCount = rowCount
        };
    }
}
