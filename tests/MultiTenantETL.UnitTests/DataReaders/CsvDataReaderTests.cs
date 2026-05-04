using FluentAssertions;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.DataReaders;
using NSubstitute;

namespace MultiTenantETL.UnitTests.DataReaders;

public class CsvDataReaderTests : IDisposable
{
    private readonly ILogger<CsvDataReader> _logger;
    private readonly CsvDataReader _sut;
    private readonly string _tempFilePath;

    public CsvDataReaderTests()
    {
        _logger = Substitute.For<ILogger<CsvDataReader>>();
        _sut = new CsvDataReader(_logger);
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
    public async Task ReadAsync_WithValidCsv_ShouldReturnBatches()
    {
        // Arrange
        const string csvContent = @"id,name,email,age
1,John Doe,john@example.com,30
2,Jane Smith,jane@example.com,25
3,Bob Johnson,bob@example.com,35";

        await File.WriteAllTextAsync(_tempFilePath, csvContent);
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
        batches[1].RowCount.Should().Be(1);

        var firstBatch = batches[0];
        firstBatch.Rows[0]["id"].Should().Be("1");
        firstBatch.Rows[0]["name"].Should().Be("John Doe");
        firstBatch.Rows[0]["email"].Should().Be("john@example.com");
        firstBatch.Rows[0]["age"].Should().Be("30");

        firstBatch.Rows[1]["id"].Should().Be("2");
        firstBatch.Rows[1]["name"].Should().Be("Jane Smith");
    }

    [Fact]
    public async Task ReadAsync_WithMaxRows_ShouldLimitRows()
    {
        // Arrange
        const string csvContent = @"id,name
1,John
2,Jane
3,Bob
4,Alice";

        await File.WriteAllTextAsync(_tempFilePath, csvContent);
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
        batches[0].Rows[0]["name"].Should().Be("John");
        batches[0].Rows[1]["name"].Should().Be("Jane");
    }

    [Fact]
    public async Task ReadAsync_WithCustomDelimiter_ShouldParseCorrectly()
    {
        // Arrange
        const string csvContent = @"id;name;email
1;John Doe;john@example.com
2;Jane Smith;jane@example.com";

        await File.WriteAllTextAsync(_tempFilePath, csvContent);
        var connector = CreateConnector(_tempFilePath, delimiter: ";");
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
        batches[0].Rows[0]["name"].Should().Be("John Doe");
        batches[0].Rows[1]["name"].Should().Be("Jane Smith");
    }

    [Fact]
    public async Task ReadAsync_WithNoHeader_ShouldUseDefaultColumnNames()
    {
        // Arrange
        const string csvContent = @"1,John,30
2,Jane,25";

        await File.WriteAllTextAsync(_tempFilePath, csvContent);
        var connector = CreateConnector(_tempFilePath, hasHeader: false);
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
        // CSVHelper uses Column1, Column2, etc. for headerless files
        batches[0].Rows[0].Should().ContainKey("Column1");
        batches[0].Rows[0]["Column1"].Should().Be("1");
        batches[0].Rows[0]["Column2"].Should().Be("John");
    }

    [Fact]
    public async Task ReadAsync_WithEmptyValues_ShouldHandleNulls()
    {
        // Arrange
        const string csvContent = @"id,name,email,age
1,John,,
2,,jane@example.com,25";

        await File.WriteAllTextAsync(_tempFilePath, csvContent);
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
        batches[0].Rows[0]["email"].Should().BeNull();
        batches[0].Rows[1]["name"].Should().BeNull();
    }

    [Fact]
    public async Task ReadAsync_WithQuotedFields_ShouldHandleQuotes()
    {
        // Arrange
        const string csvContent = @"id,name,email
1,""John Doe"",john@example.com
2,""Jane, Smith"",jane@example.com";

        await File.WriteAllTextAsync(_tempFilePath, csvContent);
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
        batches[0].Rows[0]["name"].Should().Be("John Doe");
        batches[0].Rows[1]["name"].Should().Be("Jane, Smith");
    }

    [Fact]
    public async Task TestConnectionAsync_WithExistingFile_ShouldReturnTrue()
    {
        // Arrange
        await File.WriteAllTextAsync(_tempFilePath, "test");
        var connector = CreateConnector(_tempFilePath);

        // Act
        var result = await _sut.TestConnectionAsync(connector, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TestConnectionAsync_WithNonExistingFile_ShouldReturnFalse()
    {
        // Arrange
        var nonExistingPath = Path.Combine(Path.GetTempPath(), "nonexistent.csv");
        var connector = CreateConnector(nonExistingPath);

        // Act
        var result = await _sut.TestConnectionAsync(connector, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task TestConnectionAsync_WithInvalidConfig_ShouldReturnFalse()
    {
        // Arrange
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test CSV Invalid",
            Type = "File",
            Provider = "Local",
            Direction = "source",
            IsSource = true,
            IsDestination = false,
            RequiresCredentials = false,
            IsActive = true,
            ConfigJson = "invalid json",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        // Act
        var result = await _sut.TestConnectionAsync(connector, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DetectSchemaAsync_WithValidCsv_ShouldReturnSchema()
    {
        // Arrange
        const string csvContent = @"id,name,email,age
1,John,john@example.com,30";

        await File.WriteAllTextAsync(_tempFilePath, csvContent);
        var connector = CreateConnector(_tempFilePath);

        // Act
        var result = await _sut.DetectSchemaAsync(connector, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Fields.Should().HaveCount(4);
        result.Fields.Should().Contain(f => f.Name == "id" && f.DataType == "string" && f.IsNullable);
        result.Fields.Should().Contain(f => f.Name == "name" && f.DataType == "string" && f.IsNullable);
        result.Fields.Should().Contain(f => f.Name == "email" && f.DataType == "string" && f.IsNullable);
        result.Fields.Should().Contain(f => f.Name == "age" && f.DataType == "string" && f.IsNullable);
    }

    [Fact]
    public async Task DetectSchemaAsync_WithNoHeader_ShouldReturnDefaultColumns()
    {
        // Arrange
        const string csvContent = @"1,John,30";

        await File.WriteAllTextAsync(_tempFilePath, csvContent);
        var connector = CreateConnector(_tempFilePath, hasHeader: false);

        // Act
        var result = await _sut.DetectSchemaAsync(connector, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Fields.Should().HaveCount(3);
        result.Fields[0].Name.Should().Be("Column1");
        result.Fields[1].Name.Should().Be("Column2");
        result.Fields[2].Name.Should().Be("Column3");
    }

    [Fact]
    public async Task DetectSchemaAsync_WithInvalidFile_ShouldReturnError()
    {
        // Arrange
        var connector = CreateConnector("nonexistent.csv");

        // Act
        var result = await _sut.DetectSchemaAsync(connector, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    private Connector CreateConnector(string filePath, bool hasHeader = true, string delimiter = ",")
    {
        return new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test CSV Connector",
            Type = "File",
            Provider = "Local",
            Direction = "source",
            IsSource = true,
            IsDestination = false,
            RequiresCredentials = false,
            IsActive = true,
            ConfigJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                FilePath = filePath,
                HasHeader = hasHeader,
                Delimiter = delimiter
            }),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
    }
}