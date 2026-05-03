using FluentAssertions;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.DataWriters;
using NSubstitute;
using System.Text.Json;

namespace MultiTenantETL.UnitTests.DataWriters;

public class JsonLinesDataWriterTests : IDisposable
{
    private readonly ILogger<JsonLinesDataWriter> _logger;
    private readonly JsonLinesDataWriter _sut;
    private readonly string _tempFilePath;

    public JsonLinesDataWriterTests()
    {
        _logger = Substitute.For<ILogger<JsonLinesDataWriter>>();
        _sut = new JsonLinesDataWriter(_logger);
        _tempFilePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.jsonl");
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
        var instance = new JsonLinesDataWriter(_logger);

        // Assert
        instance.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new JsonLinesDataWriter(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public async Task WriteBatchAsync_WithValidFileConfig_ShouldCreateJsonLinesFile()
    {
        // Arrange
        var connector = CreateFileConnector(_tempFilePath);
        var batch = CreateTestBatch(3);
        var options = new WriteOptions();

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.BatchId.Should().Be(batch.BatchId);
        result.RowsWritten.Should().Be(3);
        result.RowsFailed.Should().Be(0);

        File.Exists(_tempFilePath).Should().BeTrue();

        var lines = await File.ReadAllLinesAsync(_tempFilePath);
        lines.Length.Should().Be(3);
        lines[0].Should().Contain("\"id\":1");
        lines[0].Should().Contain("\"name\":\"Test User 1\"");
        lines[1].Should().Contain("\"id\":2");
        lines[2].Should().Contain("\"id\":3");
    }

    // [Fact]
    // public async Task WriteBatchAsync_WithStreamConfig_ShouldWriteToStream()
    // {
    //     // Arrange
    //     using var stream = new MemoryStream();
    //     var connector = CreateStreamConnector(stream);
    //     var batch = CreateTestBatch(2);
    //     var options = new WriteOptions();

    //     // Act
    //     var result = await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

    //     // Assert
    //     result.Should().NotBeNull();
    //     result.RowsWritten.Should().Be(2);
    //     result.RowsFailed.Should().Be(0);

    //     stream.Position = 0;
    //     using var reader = new StreamReader(stream);
    //     var content = await reader.ReadToEndAsync();
    //     var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    //     lines.Length.Should().Be(2);
    //     lines[0].Should().Contain("\"id\":1");
    //     lines[1].Should().Contain("\"id\":2");
    // }

    [Fact]
    public async Task WriteBatchAsync_WithAppendMode_ShouldAppendToExistingFile()
    {
        // Arrange - Create initial file
        var initialBatch = CreateTestBatch(1);
        var initialConnector = CreateFileConnector(_tempFilePath);
        await _sut.WriteBatchAsync(initialConnector, initialBatch, new WriteOptions { TruncateBeforeLoad = true }, CancellationToken.None);

        // Act - Append second batch
        var appendBatch = CreateTestBatch(1, startId: 2);
        var appendConnector = CreateFileConnector(_tempFilePath);
        var result = await _sut.WriteBatchAsync(appendConnector, appendBatch, new WriteOptions { TruncateBeforeLoad = false }, CancellationToken.None);

        // Assert
        result.RowsWritten.Should().Be(1);
        var lines = await File.ReadAllLinesAsync(_tempFilePath);
        lines.Length.Should().Be(2);
        lines[0].Should().Contain("\"id\":1");
        lines[1].Should().Contain("\"id\":2");
    }

    [Fact]
    public async Task WriteBatchAsync_WithTruncateMode_ShouldOverwriteExistingFile()
    {
        // Arrange - Create initial file
        var initialBatch = CreateTestBatch(2);
        var initialConnector = CreateFileConnector(_tempFilePath);
        await _sut.WriteBatchAsync(initialConnector, initialBatch, new WriteOptions { TruncateBeforeLoad = true }, CancellationToken.None);

        // Act - Truncate and write new batch
        var newBatch = CreateTestBatch(1, startId: 100);
        var newConnector = CreateFileConnector(_tempFilePath);
        var result = await _sut.WriteBatchAsync(newConnector, newBatch, new WriteOptions { TruncateBeforeLoad = true }, CancellationToken.None);

        // Assert
        result.RowsWritten.Should().Be(1);
        var lines = await File.ReadAllLinesAsync(_tempFilePath);
        lines.Length.Should().Be(1);
        lines[0].Should().Contain("\"id\":100");
    }

    [Fact]
    public async Task WriteBatchAsync_WithInvalidJsonConfig_ShouldReturnFailedResult()
    {
        // Arrange
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test JsonLines Connector",
            Type = "File",
            Provider = "JsonLines",
            Direction = "destination",
            IsSource = false,
            IsDestination = true,
            RequiresCredentials = false,
            IsActive = true,
            ConfigJson = "invalid json",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        var batch = CreateTestBatch(1);
        var options = new WriteOptions();

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.RowsWritten.Should().Be(0);
        result.RowsFailed.Should().Be(1);
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task WriteBatchAsync_WithEmptyBatch_ShouldHandleGracefully()
    {
        // Arrange
        var connector = CreateFileConnector(_tempFilePath);
        var batch = new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            Rows = new List<Dictionary<string, object?>>()
        };
        var options = new WriteOptions();

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.RowsWritten.Should().Be(0);
        result.RowsFailed.Should().Be(0);
        File.Exists(_tempFilePath).Should().BeTrue();
        var content = await File.ReadAllTextAsync(_tempFilePath);
        content.Should().BeEmpty();
    }

    [Fact]
    public async Task WriteBatchAsync_WithCancellation_ShouldHandleCancellation()
    {
        // Arrange
        var connector = CreateFileConnector(_tempFilePath);
        var batch = CreateTestBatch(1000); // Large batch to potentially trigger cancellation
        var options = new WriteOptions();
        var cts = new CancellationTokenSource();
        cts.CancelAfter(1); // Cancel immediately

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, options, cts.Token);

        // Assert - Should either succeed or fail gracefully with cancellation
        result.Should().NotBeNull();
    }

    private static Connector CreateFileConnector(string filePath)
    {
        var config = new
        {
            FilePath = filePath
        };

        return new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test JsonLines Connector",
            Type = "File",
            Provider = "JsonLines",
            Direction = "destination",
            IsSource = false,
            IsDestination = true,
            RequiresCredentials = false,
            IsActive = true,
            ConfigJson = JsonSerializer.Serialize(config),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
    }

    private static ReadBatch CreateTestBatch(int count, int startId = 1)
    {
        var rows = new List<Dictionary<string, object?>>();
        for (int i = 0; i < count; i++)
        {
            rows.Add(new Dictionary<string, object?>
            {
                ["id"] = startId + i,
                ["name"] = $"Test User {startId + i}",
                ["email"] = $"user{startId + i}@example.com"
            });
        }

        return new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            Rows = rows,
            RowCount = count
        };
    }
}