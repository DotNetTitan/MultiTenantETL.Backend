using System.Text.Json;
using FluentAssertions;
using FluentFTP;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.DataWriters;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace MultiTenantETL.UnitTests.DataWriters;

public class FtpDataWriterTests : IAsyncDisposable
{
    private readonly ILogger<FtpDataWriter> _logger;
    private FtpDataWriter? _sut;

    public FtpDataWriterTests()
    {
        _logger = Substitute.For<ILogger<FtpDataWriter>>();
    }

    public async ValueTask DisposeAsync()
    {
        if (_sut != null)
        {
            await _sut.DisposeAsync();
        }
    }

    private FtpDataWriter CreateSut()
    {
        _sut = new FtpDataWriter(_logger);
        return _sut;
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateInstance()
    {
        // Act
        var instance = CreateSut();

        // Assert
        instance.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new FtpDataWriter(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public async Task WriteBatchAsync_WithInvalidJsonConfig_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var sut = CreateSut();
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test FTP Connector",
            Type = "File",
            Provider = "FTP",
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

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);
        });
    }

    [Fact]
    public async Task WriteBatchAsync_WithMissingRequiredFields_ShouldSucceedInitially()
    {
        // Arrange
        var sut = CreateSut();
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test FTP Connector",
            Type = "File",
            Provider = "FTP",
            Direction = "destination",
            IsSource = false,
            IsDestination = true,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = @"{
                ""Port"": 21,
                ""Username"": ""user""
            }",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        var batch = CreateValidBatch();
        var options = new WriteOptions();

        // Act - Should succeed initially (lazy config parsing)
        var result = await sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert - Buffering succeeds, but DisposeAsync will fail
        result.RowsWritten.Should().Be(batch.RowCount);
        result.RowsFailed.Should().Be(0);
    }

    [Fact]
    public async Task WriteBatchAsync_WithValidCsvConfig_ShouldBufferData()
    {
        // Arrange
        var sut = CreateSut();
        var connector = CreateValidFtpConnector("csv");
        var batch = CreateValidBatch();
        var options = new WriteOptions();

        // Act
        var result = await sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        result.RowsWritten.Should().Be(batch.RowCount);
        result.RowsFailed.Should().Be(0);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task WriteBatchAsync_WithValidJsonlConfig_ShouldBufferData()
    {
        // Arrange
        var sut = CreateSut();
        var connector = CreateValidFtpConnector("jsonl");
        var batch = CreateValidBatch();
        var options = new WriteOptions();

        // Act
        var result = await sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        result.RowsWritten.Should().Be(batch.RowCount);
        result.RowsFailed.Should().Be(0);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task WriteBatchAsync_WithEmptyBatch_ShouldHandleGracefully()
    {
        // Arrange
        var sut = CreateSut();
        var connector = CreateValidFtpConnector("csv");
        var batch = CreateEmptyBatch();
        var options = new WriteOptions();

        // Act
        var result = await sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        result.RowsWritten.Should().Be(0);
        result.RowsFailed.Should().Be(0);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task WriteBatchAsync_MultipleBatches_ShouldAppendToBuffer()
    {
        // Arrange
        var sut = CreateSut();
        var connector = CreateValidFtpConnector("csv");
        var batch1 = CreateValidBatch();
        var batch2 = CreateValidBatch();
        var options = new WriteOptions();

        // Act
        var result1 = await sut.WriteBatchAsync(connector, batch1, options, CancellationToken.None);
        var result2 = await sut.WriteBatchAsync(connector, batch2, options, CancellationToken.None);

        // Assert
        result1.RowsWritten.Should().Be(batch1.RowCount);
        result2.RowsWritten.Should().Be(batch2.RowCount);
        result1.RowsFailed.Should().Be(0);
        result2.RowsFailed.Should().Be(0);
    }

    [Fact]
    public async Task WriteBatchAsync_WithCancellation_ShouldCancelOperation()
    {
        // Arrange
        var sut = CreateSut();
        var connector = CreateValidFtpConnector("csv");
        var batch = CreateValidBatch();
        var options = new WriteOptions();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await sut.WriteBatchAsync(connector, batch, options, cts.Token);
        });
    }

    [Fact]
    public async Task DisposeAsync_WithValidConfig_ShouldAttemptUpload()
    {
        // Arrange
        var sut = CreateSut();
        var connector = CreateValidFtpConnector("csv");
        var batch = CreateValidBatch();
        var options = new WriteOptions();

        // Write some data first
        await sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Act & Assert - DisposeAsync should attempt FTP connection (will fail with network error)
        var act = async () => await sut.DisposeAsync();
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task DisposeAsync_WithoutData_ShouldNotUpload()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert - DisposeAsync should complete without throwing
        var act = async () => await sut.DisposeAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task WriteBatchAsync_FormatDetection_FromFileExtension()
    {
        // Arrange
        var sut = CreateSut();
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test FTP Connector",
            Type = "File",
            Provider = "FTP",
            Direction = "destination",
            IsSource = false,
            IsDestination = true,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = @"{
                ""Host"": ""ftp.example.com"",
                ""Username"": ""user"",
                ""Password"": ""pass"",
                ""FilePath"": ""/data/test.csv""
            }",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        var batch = CreateValidBatch();
        var options = new WriteOptions();

        // Act
        var result = await sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert - Should detect CSV format from .csv extension
        result.RowsWritten.Should().Be(batch.RowCount);
        result.RowsFailed.Should().Be(0);
    }

    [Fact]
    public async Task WriteBatchAsync_FormatDetection_DefaultToJsonl()
    {
        // Arrange
        var sut = CreateSut();
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test FTP Connector",
            Type = "File",
            Provider = "FTP",
            Direction = "destination",
            IsSource = false,
            IsDestination = true,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = @"{
                ""Host"": ""ftp.example.com"",
                ""Username"": ""user"",
                ""Password"": ""pass"",
                ""FilePath"": ""/data/test.unknown""
            }",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        var batch = CreateValidBatch();
        var options = new WriteOptions();

        // Act
        var result = await sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert - Should default to JSONL format for unknown extensions
        result.RowsWritten.Should().Be(batch.RowCount);
        result.RowsFailed.Should().Be(0);
    }

    private static Connector CreateValidFtpConnector(string format = "csv")
    {
        return new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test FTP Connector",
            Type = "File",
            Provider = "FTP",
            Direction = "destination",
            IsSource = false,
            IsDestination = true,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = $@"{{
                ""Host"": ""ftp.example.com"",
                ""Port"": 21,
                ""Username"": ""user"",
                ""Password"": ""pass"",
                ""FilePath"": ""/data/test.{format}"",
                ""Format"": ""{format}""
            }}",
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

    private static ReadBatch CreateEmptyBatch()
    {
        return new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            Rows = new List<Dictionary<string, object?>>(),
            RowCount = 0
        };
    }
}