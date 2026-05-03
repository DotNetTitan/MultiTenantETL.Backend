using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.DataReaders;
using NSubstitute;

namespace MultiTenantETL.UnitTests.DataReaders;

public class JsonLinesDataReaderTests : IDisposable
{
    private readonly JsonLinesDataReader _sut;
    private readonly ILogger<JsonLinesDataReader> _logger;
    private readonly string _tempFilePath;

    public JsonLinesDataReaderTests()
    {
        _logger = Substitute.For<ILogger<JsonLinesDataReader>>();
        _sut = new JsonLinesDataReader(_logger);
        _tempFilePath = Path.GetTempFileName();
    }

    public void Dispose()
    {
        if (File.Exists(_tempFilePath))
        {
            File.Delete(_tempFilePath);
        }
    }

    private Connector CreateConnector(string filePath)
    {
        var config = new
        {
            FilePath = filePath
        };

        return new Connector
        {
            Id = Guid.NewGuid(),
            Name = "Test JSONL Connector",
            Type = "File",
            Provider = "Local",
            Direction = "source",
            ConfigJson = JsonSerializer.Serialize(config),
            TenantId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
    }

    [Fact]
    public async Task ReadAsync_WithValidJsonLines_ShouldReturnMultipleRows()
    {
        // Arrange
        var jsonlContent = @"{""name"":""John"",""age"":30}
{""name"":""Jane"",""age"":25}
{""name"":""Bob"",""age"":35}";
        await File.WriteAllTextAsync(_tempFilePath, jsonlContent);
        var connector = CreateConnector(_tempFilePath);
        var options = new ReadOptions { BatchSize = 10 };

        // Act
        var batches = new List<ReadBatch>();
        await foreach (var batch in _sut.ReadAsync(connector, options, CancellationToken.None))
        {
            batches.Add(batch);
        }

        // Assert
        batches.Should().HaveCount(1);
        batches[0].RowCount.Should().Be(3);
        batches[0].Rows[0]["name"].Should().Be("John");
        batches[0].Rows[0]["age"].Should().Be(30);
        batches[0].Rows[1]["name"].Should().Be("Jane");
        batches[0].Rows[2]["name"].Should().Be("Bob");
    }

    [Fact]
    public async Task ReadAsync_WithBatching_ShouldSplitIntoMultipleBatches()
    {
        // Arrange
        var jsonlContent = @"{""id"":1,""value"":""A""}
{""id"":2,""value"":""B""}
{""id"":3,""value"":""C""}
{""id"":4,""value"":""D""}";
        await File.WriteAllTextAsync(_tempFilePath, jsonlContent);
        var connector = CreateConnector(_tempFilePath);
        var options = new ReadOptions { BatchSize = 2 };

        // Act
        var batches = new List<ReadBatch>();
        await foreach (var batch in _sut.ReadAsync(connector, options, CancellationToken.None))
        {
            batches.Add(batch);
        }

        // Assert
        batches.Should().HaveCount(2);
        batches[0].RowCount.Should().Be(2);
        batches[1].RowCount.Should().Be(2);
        batches[0].Rows[0]["id"].Should().Be(1);
        batches[1].Rows[0]["id"].Should().Be(3);
    }

    [Fact]
    public async Task ReadAsync_WithMaxRows_ShouldLimitRows()
    {
        // Arrange
        var jsonlContent = @"{""id"":1}
{""id"":2}
{""id"":3}
{""id"":4}";
        await File.WriteAllTextAsync(_tempFilePath, jsonlContent);
        var connector = CreateConnector(_tempFilePath);
        var options = new ReadOptions { BatchSize = 10, MaxRows = 2 };

        // Act
        var batches = new List<ReadBatch>();
        await foreach (var batch in _sut.ReadAsync(connector, options, CancellationToken.None))
        {
            batches.Add(batch);
        }

        // Assert
        batches.Should().HaveCount(1);
        batches[0].RowCount.Should().Be(2);
        batches[0].Rows[0]["id"].Should().Be(1);
        batches[0].Rows[1]["id"].Should().Be(2);
    }

    [Fact]
    public async Task ReadAsync_WithEmptyLines_ShouldSkipEmptyLines()
    {
        // Arrange
        var jsonlContent = @"{""id"":1}

{""id"":2}

{""id"":3}";
        await File.WriteAllTextAsync(_tempFilePath, jsonlContent);
        var connector = CreateConnector(_tempFilePath);
        var options = new ReadOptions { BatchSize = 10 };

        // Act
        var batches = new List<ReadBatch>();
        await foreach (var batch in _sut.ReadAsync(connector, options, CancellationToken.None))
        {
            batches.Add(batch);
        }

        // Assert
        batches.Should().HaveCount(1);
        batches[0].RowCount.Should().Be(3);
    }

    [Fact]
    public async Task ReadAsync_WithNullValues_ShouldHandleNulls()
    {
        // Arrange
        var jsonlContent = @"{""name"":""John"",""age"":null}
{""name"":null,""age"":25}";
        await File.WriteAllTextAsync(_tempFilePath, jsonlContent);
        var connector = CreateConnector(_tempFilePath);
        var options = new ReadOptions { BatchSize = 10 };

        // Act
        var batches = new List<ReadBatch>();
        await foreach (var batch in _sut.ReadAsync(connector, options, CancellationToken.None))
        {
            batches.Add(batch);
        }

        // Assert
        batches.Should().HaveCount(1);
        batches[0].RowCount.Should().Be(2);
        batches[0].Rows[0]["name"].Should().Be("John");
        batches[0].Rows[0]["age"].Should().BeNull();
        batches[0].Rows[1]["name"].Should().BeNull();
        batches[0].Rows[1]["age"].Should().Be(25);
    }

    [Fact]
    public async Task ReadAsync_WithEmptyFile_ShouldReturnEmptyBatch()
    {
        // Arrange
        await File.WriteAllTextAsync(_tempFilePath, "");
        var connector = CreateConnector(_tempFilePath);
        var options = new ReadOptions { BatchSize = 10 };

        // Act
        var batches = new List<ReadBatch>();
        await foreach (var batch in _sut.ReadAsync(connector, options, CancellationToken.None))
        {
            batches.Add(batch);
        }

        // Assert
        batches.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadAsync_WithInvalidJson_ShouldThrowException()
    {
        // Arrange
        var invalidJsonl = @"{""name"":""John""}
{invalid json}";
        await File.WriteAllTextAsync(_tempFilePath, invalidJsonl);
        var connector = CreateConnector(_tempFilePath);
        var options = new ReadOptions { BatchSize = 10 };

        // Act & Assert
        await Assert.ThrowsAsync<JsonException>(async () =>
        {
            var batches = new List<ReadBatch>();
            await foreach (var batch in _sut.ReadAsync(connector, options, CancellationToken.None))
            {
                batches.Add(batch);
            }
        });
    }

    [Fact]
    public async Task DetectSchemaAsync_WithValidJsonLines_ShouldReturnSchema()
    {
        // Arrange
        var jsonlContent = @"{""name"":""John"",""age"":30,""active"":true}
{""name"":""Jane"",""age"":25,""active"":false}";
        await File.WriteAllTextAsync(_tempFilePath, jsonlContent);
        var connector = CreateConnector(_tempFilePath);

        // Act
        var result = await _sut.DetectSchemaAsync(connector, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Fields.Should().HaveCount(3);
        result.Fields.Select(f => f.Name).Should().BeEquivalentTo(new[] { "name", "age", "active" });
        result.Fields.All(f => f.DataType == "string").Should().BeTrue();
        result.Fields.All(f => f.IsNullable).Should().BeTrue();
    }

    [Fact]
    public async Task DetectSchemaAsync_WithEmptyFile_ShouldReturnError()
    {
        // Arrange
        await File.WriteAllTextAsync(_tempFilePath, "");
        var connector = CreateConnector(_tempFilePath);

        // Act
        var result = await _sut.DetectSchemaAsync(connector, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("File is empty");
        result.Fields.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectSchemaAsync_WithInvalidJson_ShouldReturnError()
    {
        // Arrange
        var invalidJson = @"{invalid}";
        await File.WriteAllTextAsync(_tempFilePath, invalidJson);
        var connector = CreateConnector(_tempFilePath);

        // Act
        var result = await _sut.DetectSchemaAsync(connector, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        result.Fields.Should().BeEmpty();
    }

    [Fact]
    public async Task TestConnectionAsync_WithValidFile_ShouldReturnTrue()
    {
        // Arrange
        await File.WriteAllTextAsync(_tempFilePath, "{}");
        var connector = CreateConnector(_tempFilePath);

        // Act
        var result = await _sut.TestConnectionAsync(connector, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TestConnectionAsync_WithNonExistentFile_ShouldReturnFalse()
    {
        // Arrange
        var connector = CreateConnector("nonexistent.jsonl");

        // Act
        var result = await _sut.TestConnectionAsync(connector, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }
}