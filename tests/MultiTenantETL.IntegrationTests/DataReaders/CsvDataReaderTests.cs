using FluentAssertions;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.DataReaders;
using Xunit;

namespace MultiTenantETL.IntegrationTests.DataReaders;

public class CsvDataReaderTests : IDisposable
{
    private readonly CsvDataReader _reader;
    private readonly string _testFilePath;
    private readonly string _largeTestFilePath;

    public CsvDataReaderTests()
    {
        var logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<CsvDataReader>();
        
        _reader = new CsvDataReader(logger);
        _testFilePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.csv");
        _largeTestFilePath = Path.Combine(Path.GetTempPath(), $"large_test_{Guid.NewGuid()}.csv");
    }

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
            File.Delete(_testFilePath);
        
        if (File.Exists(_largeTestFilePath))
            File.Delete(_largeTestFilePath);
    }

    [Fact]
    public async Task ReadAsync_ShouldStreamDataInBatches()
    {
        // Arrange
        CreateTestCsvFile(_testFilePath, 250);
        var connector = CreateConnector(_testFilePath);
        var options = new ReadOptions { BatchSize = 100 };
        var totalRows = 0;
        var batchCount = 0;

        // Act
        await foreach (var batch in _reader.ReadAsync(connector, options, CancellationToken.None))
        {
            // Assert
            batch.Should().NotBeNull();
            batch.BatchId.Should().NotBeEmpty();
            batch.Rows.Should().NotBeEmpty();
            batch.RowCount.Should().Be(batch.Rows.Count);
            batch.RowCount.Should().BeLessThanOrEqualTo(100);

            totalRows += batch.RowCount;
            batchCount++;

            // Verify row structure
            var firstRow = batch.Rows[0];
            firstRow.Should().ContainKey("id");
            firstRow.Should().ContainKey("name");
            firstRow.Should().ContainKey("email");
            firstRow.Should().ContainKey("age");
        }

        // Assert
        totalRows.Should().Be(250);
        batchCount.Should().Be(3); // 100 + 100 + 50
    }

    [Fact]
    public async Task ReadAsync_ShouldHandleLargeFile()
    {
        // Arrange - Create 50k row CSV file
        CreateTestCsvFile(_largeTestFilePath, 50000);
        var connector = CreateConnector(_largeTestFilePath);
        var options = new ReadOptions { BatchSize = 5000 };
        var totalRows = 0;

        // Act
        await foreach (var batch in _reader.ReadAsync(connector, options, CancellationToken.None))
        {
            totalRows += batch.RowCount;
            batch.RowCount.Should().BeLessThanOrEqualTo(5000);
        }

        // Assert
        totalRows.Should().Be(50000);
    }

    [Fact]
    public async Task ReadAsync_ShouldRespectMaxRows()
    {
        // Arrange
        CreateTestCsvFile(_testFilePath, 1000);
        var connector = CreateConnector(_testFilePath);
        var options = new ReadOptions
        {
            BatchSize = 200,
            MaxRows = 450
        };
        var totalRows = 0;

        // Act
        await foreach (var batch in _reader.ReadAsync(connector, options, CancellationToken.None))
        {
            totalRows += batch.RowCount;
        }

        // Assert
        totalRows.Should().Be(450);
    }

    [Fact]
    public async Task ReadAsync_ShouldHandleEmptyFile()
    {
        // Arrange - Create file with only header
        File.WriteAllText(_testFilePath, "id,name,email,age\n");
        var connector = CreateConnector(_testFilePath);
        var options = new ReadOptions { BatchSize = 100 };
        var batchCount = 0;

        // Act
        await foreach (var batch in _reader.ReadAsync(connector, options, CancellationToken.None))
        {
            batchCount++;
        }

        // Assert
        batchCount.Should().Be(0);
    }

    [Fact]
    public async Task ReadAsync_ShouldHandleQuotedFields()
    {
        // Arrange
        var csvContent = @"id,name,email,age
1,""Smith, John"",""john@test.com"",30
2,""Doe, Jane"",""jane@test.com"",25
3,""O'Brien, Mike"",""mike@test.com"",35";
        
        File.WriteAllText(_testFilePath, csvContent);
        var connector = CreateConnector(_testFilePath);
        var options = new ReadOptions { BatchSize = 10 };

        // Act
        var batches = new List<ReadBatch>();
        await foreach (var batch in _reader.ReadAsync(connector, options, CancellationToken.None))
        {
            batches.Add(batch);
        }

        // Assert
        batches.Should().HaveCount(1);
        batches[0].RowCount.Should().Be(3);
        batches[0].Rows[0]["name"].Should().Be("Smith, John");
        batches[0].Rows[1]["name"].Should().Be("Doe, Jane");
        batches[0].Rows[2]["name"].Should().Be("O'Brien, Mike");
    }

    [Fact]
    public async Task ReadAsync_ShouldHandleCustomDelimiter()
    {
        // Arrange
        var csvContent = @"id;name;email;age
1;John Smith;john@test.com;30
2;Jane Doe;jane@test.com;25";
        
        File.WriteAllText(_testFilePath, csvContent);
        var connector = CreateConnectorWithDelimiter(_testFilePath, ";");
        var options = new ReadOptions { BatchSize = 10 };

        // Act
        var batches = new List<ReadBatch>();
        await foreach (var batch in _reader.ReadAsync(connector, options, CancellationToken.None))
        {
            batches.Add(batch);
        }

        // Assert
        batches.Should().HaveCount(1);
        batches[0].RowCount.Should().Be(2);
        batches[0].Rows[0]["name"].Should().Be("John Smith");
    }

    [Fact]
    public async Task ReadAsync_ShouldHandleNullValues()
    {
        // Arrange
        var csvContent = @"id,name,email,age
1,John,john@test.com,30
2,Jane,,
3,,mike@test.com,35";
        
        File.WriteAllText(_testFilePath, csvContent);
        var connector = CreateConnector(_testFilePath);
        var options = new ReadOptions { BatchSize = 10 };

        // Act
        var batches = new List<ReadBatch>();
        await foreach (var batch in _reader.ReadAsync(connector, options, CancellationToken.None))
        {
            batches.Add(batch);
        }

        // Assert
        batches[0].Rows[1]["email"].Should().BeNull();
        batches[0].Rows[1]["age"].Should().BeNull();
        batches[0].Rows[2]["name"].Should().BeNull();
    }

    [Fact]
    public async Task TestConnectionAsync_ShouldReturnTrue_WhenFileExists()
    {
        // Arrange
        CreateTestCsvFile(_testFilePath, 10);
        var connector = CreateConnector(_testFilePath);

        // Act
        var result = await _reader.TestConnectionAsync(connector, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TestConnectionAsync_ShouldReturnFalse_WhenFileDoesNotExist()
    {
        // Arrange
        var connector = CreateConnector("nonexistent.csv");

        // Act
        var result = await _reader.TestConnectionAsync(connector, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DetectSchemaAsync_ShouldReturnCorrectSchema()
    {
        // Arrange
        CreateTestCsvFile(_testFilePath, 10);
        var connector = CreateConnector(_testFilePath);

        // Act
        var result = await _reader.DetectSchemaAsync(connector, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Fields.Should().HaveCount(4);
        
        result.Fields.Should().Contain(f => f.Name == "id");
        result.Fields.Should().Contain(f => f.Name == "name");
        result.Fields.Should().Contain(f => f.Name == "email");
        result.Fields.Should().Contain(f => f.Name == "age");
        
        // CSV reader infers all fields as string and nullable
        result.Fields.Should().AllSatisfy(f =>
        {
            f.DataType.Should().Be("string");
            f.IsNullable.Should().BeTrue();
        });
    }

    private void CreateTestCsvFile(string filePath, int rowCount)
    {
        using var writer = new StreamWriter(filePath);
        writer.WriteLine("id,name,email,age");
        
        for (int i = 1; i <= rowCount; i++)
        {
            writer.WriteLine($"{i},User {i},user{i}@test.com,{20 + (i % 50)}");
        }
    }

    private Connector CreateConnector(string filePath)
    {
        return new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test CSV",
            Type = "File",
            Provider = "CSV",
            Direction = "source",
            IsSource = true,
            IsDestination = false,
            RequiresCredentials = false,
            IsActive = true,
            ConfigJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                FilePath = filePath,
                HasHeader = true,
                Delimiter = ","
            }),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
    }

    private Connector CreateConnectorWithDelimiter(string filePath, string delimiter)
    {
        return new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test CSV",
            Type = "File",
            Provider = "CSV",
            Direction = "source",
            IsSource = true,
            IsDestination = false,
            RequiresCredentials = false,
            IsActive = true,
            ConfigJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                FilePath = filePath,
                HasHeader = true,
                Delimiter = delimiter
            }),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
    }
}
