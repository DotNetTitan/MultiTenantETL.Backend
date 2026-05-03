using FluentAssertions;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.DataWriters;
using NSubstitute;
using System.Text;

namespace MultiTenantETL.UnitTests.DataWriters;

public class CsvDataWriterTests : IDisposable
{
    private readonly ILogger<CsvDataWriter> _logger;
    private readonly CsvDataWriter _sut;
    private readonly string _tempFilePath;

    public CsvDataWriterTests()
    {
        _logger = Substitute.For<ILogger<CsvDataWriter>>();
        _sut = new CsvDataWriter(_logger);
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
        var instance = new CsvDataWriter(_logger);

        // Assert
        instance.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new CsvDataWriter(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public async Task WriteBatchAsync_WithValidFileConfig_ShouldCreateCsvFile()
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
        content.Should().Contain("id,name,email");
        content.Should().Contain("1,Test User 1,user1@example.com");
        content.Should().Contain("2,Test User 2,user2@example.com");
        content.Should().Contain("3,Test User 3,user3@example.com");
    }

    [Fact]
    public async Task WriteBatchAsync_WithHasHeaderFalse_ShouldNotWriteHeader()
    {
        // Arrange
        var connector = CreateFileConnector(_tempFilePath, hasHeader: false);
        var batch = CreateTestBatch(2);
        var options = new WriteOptions();

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        var content = await File.ReadAllTextAsync(_tempFilePath);
        content.Should().NotContain("id,name,email");
        content.Should().Contain("1,Test User 1,user1@example.com");
        content.Should().Contain("2,Test User 2,user2@example.com");
    }

    [Fact]
    public async Task WriteBatchAsync_WithCustomDelimiter_ShouldUseCustomDelimiter()
    {
        // Arrange
        var connector = CreateFileConnector(_tempFilePath, delimiter: "|");
        var batch = CreateTestBatch(2);
        var options = new WriteOptions();

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        var content = await File.ReadAllTextAsync(_tempFilePath);
        content.Should().Contain("id|name|email");
        content.Should().Contain("1|Test User 1|user1@example.com");
    }

    [Fact]
    public async Task WriteBatchAsync_WithEmptyBatch_ShouldCreateEmptyFile()
    {
        // Arrange
        var connector = CreateFileConnector(_tempFilePath);
        var batch = new ReadBatch { BatchId = Guid.NewGuid(), Rows = new List<Dictionary<string, object?>>() };
        var options = new WriteOptions();

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        result.RowsWritten.Should().Be(0);
        result.RowsFailed.Should().Be(0);

        File.Exists(_tempFilePath).Should().BeTrue();
        var content = await File.ReadAllTextAsync(_tempFilePath);
        content.Should().BeEmpty();
    }

    [Fact]
    public async Task WriteBatchAsync_WithInvalidJsonConfig_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test CSV Connector",
            Type = "File",
            Provider = "Local",
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
        var act = () => _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Failed to parse CSV connector configuration");
    }

    [Fact]
    public async Task WriteBatchAsync_WithCancellation_ShouldCancelOperation()
    {
        // Arrange
        var connector = CreateFileConnector(_tempFilePath);
        var batch = CreateTestBatch(1000); // Large batch to ensure it takes time
        var options = new WriteOptions();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => _sut.WriteBatchAsync(connector, batch, options, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task WriteBatchAsync_WithFileModeAppend_ShouldAppendToExistingFile()
    {
        // Arrange - Create initial file
        await File.WriteAllTextAsync(_tempFilePath, "existing,header,data\n");
        var connector = CreateFileConnector(_tempFilePath);
        var batch = CreateTestBatch(1);
        var options = new WriteOptions(); // This should append since file exists

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        var content = await File.ReadAllTextAsync(_tempFilePath);
        content.Should().Contain("existing,header,data");
        content.Should().NotContain("id,name,email"); // Header should not be written in append mode
        content.Should().Contain("1,Test User 1,user1@example.com");
    }

    [Fact]
    public async Task WriteBatchAsync_WithTruncateBeforeLoad_ShouldOverwriteFile()
    {
        // Arrange - Create initial file
        await File.WriteAllTextAsync(_tempFilePath, "existing,header,data\n");
        var connector = CreateFileConnector(_tempFilePath);
        var batch = CreateTestBatch(1);
        var options = new WriteOptions { TruncateBeforeLoad = true };

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        var content = await File.ReadAllTextAsync(_tempFilePath);
        content.Should().NotContain("existing,header,data");
        content.Should().Contain("id,name,email");
        content.Should().Contain("1,Test User 1,user1@example.com");
    }

    private static Connector CreateFileConnector(string filePath, bool hasHeader = true, string delimiter = ",")
    {
        return new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test CSV Connector",
            Type = "File",
            Provider = "Local",
            Direction = "destination",
            IsSource = false,
            IsDestination = true,
            RequiresCredentials = false,
            IsActive = true,
            ConfigJson = $@"{{
                ""FilePath"": ""{filePath.Replace("\\", "\\\\")}"",
                ""HasHeader"": {hasHeader.ToString().ToLower()},
                ""Delimiter"": ""{delimiter}""
            }}",
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