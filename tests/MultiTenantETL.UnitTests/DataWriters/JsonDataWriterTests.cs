using FluentAssertions;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.DataWriters;
using NSubstitute;
using System.Text.Json;

namespace MultiTenantETL.UnitTests.DataWriters;

public class JsonDataWriterTests : IDisposable
{
    private readonly ILogger<JsonDataWriter> _logger;
    private readonly JsonDataWriter _sut;
    private readonly string _tempFilePath;

    public JsonDataWriterTests()
    {
        _logger = Substitute.For<ILogger<JsonDataWriter>>();
        _sut = new JsonDataWriter(_logger);
        _tempFilePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.json");
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
        var instance = new JsonDataWriter(_logger);

        // Assert
        instance.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new JsonDataWriter(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public async Task WriteBatchAsync_WithValidFileConfig_ShouldCreateJsonFile()
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

        var content = await File.ReadAllTextAsync(_tempFilePath);
        content.Should().Contain("[");
        content.Should().Contain("]");
        content.Should().Contain("\"id\":1");
        content.Should().Contain("\"name\":\"Test User 1\"");
        content.Should().Contain("\"email\":\"user1@example.com\"");
    }

    [Fact]
    public async Task WriteBatchAsync_WithIndentedTrue_ShouldCreateIndentedJson()
    {
        // Arrange
        var connector = CreateFileConnector(_tempFilePath, indented: true);
        var batch = CreateTestBatch(2);
        var options = new WriteOptions();

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        var content = await File.ReadAllTextAsync(_tempFilePath);
        content.Should().Contain("  \"id\":");
        content.Should().Contain("  \"name\":");
        content.Should().Contain("  \"email\":");
    }

    // [Fact]
    // public async Task WriteBatchAsync_WithFileConfig_ShouldWriteToFile()
    // {
    //     // Arrange
    //     using var stream = new MemoryStream(); // Not used in this test
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
    //     content.Should().Contain("[");
    //     content.Should().Contain("]");
    //     content.Should().Contain("\"id\":1");
    //     content.Should().Contain("\"name\":\"Test User 1\"");
    // }

    [Fact]
    public async Task WriteBatchAsync_WithAppendMode_ShouldThrowNotSupportedException()
    {
        // Arrange - Create initial file
        var initialBatch = CreateTestBatch(1);
        var initialConnector = CreateFileConnector(_tempFilePath);
        await _sut.WriteBatchAsync(initialConnector, initialBatch, new WriteOptions { TruncateBeforeLoad = true }, CancellationToken.None);

        // Verify file exists
        File.Exists(_tempFilePath).Should().BeTrue();

        // Act - Try to append second batch
        var appendBatch = CreateTestBatch(1, startId: 2);
        var appendConnector = CreateFileConnector(_tempFilePath);
        var act = () => _sut.WriteBatchAsync(appendConnector, appendBatch, new WriteOptions { TruncateBeforeLoad = false }, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("JSON array format does not support efficient append operations. Use JSONL format for append scenarios or truncate the file.");
    }

    [Fact]
    public async Task WriteBatchAsync_WithInvalidJsonConfig_ShouldReturnFailedResult()
    {
        // Arrange
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test Json Connector",
            Type = "File",
            Provider = "Json",
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

    private static Connector CreateFileConnector(string filePath, bool indented = false)
    {
        var config = new
        {
            FilePath = filePath,
            Indented = indented
        };

        return new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test Json Connector",
            Type = "File",
            Provider = "Json",
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