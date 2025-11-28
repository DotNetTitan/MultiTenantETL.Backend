using FluentAssertions;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.DataWriters;
using System.Text.Json;
using Xunit;

namespace MultiTenantETL.IntegrationTests.DataWriters;

public class JsonLinesDataWriterTests : IAsyncDisposable
{
    private readonly JsonLinesDataWriter _writer;
    private readonly string _testFilePath;

    public JsonLinesDataWriterTests()
    {
        var logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<JsonLinesDataWriter>();
        
        _writer = new JsonLinesDataWriter(logger);
        _testFilePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.jsonl");
    }

    public async ValueTask DisposeAsync()
    {
        await _writer.DisposeAsync();
        
        if (File.Exists(_testFilePath))
            File.Delete(_testFilePath);
    }

    [Fact]
    public async Task WriteBatchAsync_ShouldCreateFileWithJsonLines()
    {
        // Arrange
        var connector = CreateConnector();
        var batch = CreateTestBatch(3);
        var options = new WriteOptions();

        // Act
        var result = await _writer.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        result.RowsWritten.Should().Be(3);
        result.RowsFailed.Should().Be(0);
        
        File.Exists(_testFilePath).Should().BeTrue();
        var lines = File.ReadAllLines(_testFilePath);
        lines.Should().HaveCount(3);
        
        // Each line should be valid JSON
        foreach (var line in lines)
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, object>>(line);
            parsed.Should().NotBeNull();
            parsed.Should().ContainKey("id");
            parsed.Should().ContainKey("name");
        }
    }

    [Fact]
    public async Task WriteBatchAsync_ShouldSupportTrueStreamingAppend()
    {
        // Arrange
        var connector = CreateConnector();
        var batch1 = CreateTestBatch(2);
        var batch2 = CreateTestBatch(2, startId: 10);
        var batch3 = CreateTestBatch(2, startId: 20);
        var options = new WriteOptions { TruncateBeforeLoad = false };

        // Act - Write multiple batches
        await _writer.WriteBatchAsync(connector, batch1, options, CancellationToken.None);
        await _writer.DisposeAsync(); // Ensure first write completes
        
        var writer2 = new JsonLinesDataWriter(
            LoggerFactory.Create(b => b.AddConsole()).CreateLogger<JsonLinesDataWriter>());
        await writer2.WriteBatchAsync(connector, batch2, options, CancellationToken.None);
        await writer2.DisposeAsync();
        
        var writer3 = new JsonLinesDataWriter(
            LoggerFactory.Create(b => b.AddConsole()).CreateLogger<JsonLinesDataWriter>());
        await writer3.WriteBatchAsync(connector, batch3, options, CancellationToken.None);
        await writer3.DisposeAsync();

        // Assert
        var lines = File.ReadAllLines(_testFilePath);
        lines.Should().HaveCount(6); // 2 + 2 + 2 rows
        
        // Verify each line is valid JSON
        var allRecords = lines.Select(line => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(line)).ToList();
        allRecords.Should().HaveCount(6);
        allRecords[0]["id"].GetInt32().Should().Be(1);
        allRecords[2]["id"].GetInt32().Should().Be(10);
        allRecords[4]["id"].GetInt32().Should().Be(20);
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
        lines.Should().HaveCount(2); // Only batch2 rows
        
        var firstRecord = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(lines[0]);
        firstRecord!["id"].GetInt32().Should().Be(10);
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
        lines.Should().HaveCount(10000);
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
        await _writer.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        var lines = File.ReadAllLines(_testFilePath);
        lines.Should().HaveCount(2);
        
        var record1 = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(lines[0]);
        record1!["email"].ValueKind.Should().Be(JsonValueKind.Null);
        
        var record2 = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(lines[1]);
        record2!["name"].ValueKind.Should().Be(JsonValueKind.Null);
        record2["age"].ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task WriteBatchAsync_ShouldHandleComplexObjects()
    {
        // Arrange
        var connector = CreateConnector();
        var batch = new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            Rows = new List<Dictionary<string, object?>>
            {
                new()
                {
                    ["id"] = 1,
                    ["name"] = "John",
                    ["metadata"] = new Dictionary<string, object>
                    {
                        ["tags"] = new[] { "vip", "premium" },
                        ["score"] = 95.5
                    }
                }
            },
            RowCount = 1
        };
        var options = new WriteOptions();

        // Act
        var result = await _writer.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        result.RowsWritten.Should().Be(1);
        
        var line = File.ReadAllText(_testFilePath);
        line.Should().Contain("metadata");
        line.Should().Contain("tags");
        line.Should().Contain("vip");
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
    public async Task WriteBatchAsync_ShouldNotRequireLoadingEntireFile_ForAppend()
    {
        // Arrange - This test demonstrates JSONL's key advantage over JSON arrays
        var connector = CreateConnector();
        var options = new WriteOptions { TruncateBeforeLoad = false };

        // Act - Write 3 separate batches
        for (int i = 0; i < 3; i++)
        {
            var batch = CreateTestBatch(1000, startId: i * 1000 + 1);
            var writer = new JsonLinesDataWriter(
                LoggerFactory.Create(b => b.AddConsole()).CreateLogger<JsonLinesDataWriter>());
            
            await writer.WriteBatchAsync(connector, batch, options, CancellationToken.None);
            await writer.DisposeAsync();
        }

        // Assert
        var lines = File.ReadAllLines(_testFilePath);
        lines.Should().HaveCount(3000);
        
        // Verify no duplicate data (true append, not reload+append)
        var ids = lines.Select(line => 
        {
            var record = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(line);
            return record!["id"].GetInt32();
        }).ToList();
        
        ids.Should().OnlyHaveUniqueItems();
        ids.Should().BeInAscendingOrder();
    }

    private Connector CreateConnector()
    {
        return new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test JSONL",
            Type = "File",
            Provider = "JSONL",
            Direction = "destination",
            IsSource = false,
            IsDestination = true,
            RequiresCredentials = false,
            IsActive = true,
            ConfigJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                FilePath = _testFilePath
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
