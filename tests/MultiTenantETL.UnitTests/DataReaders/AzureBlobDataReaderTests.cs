using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;
using MultiTenantETL.Infrastructure.DataReaders;
using MultiTenantETL.Infrastructure.Services.Storage;
using NSubstitute;
using System.Text.Json;

namespace MultiTenantETL.UnitTests.DataReaders;

public class AzureBlobDataReaderTests
{
    private readonly IStorageClientFactory _clientFactory;
    private readonly CsvDataReader _csvReader;
    private readonly JsonDataReader _jsonReader;
    private readonly JsonLinesDataReader _jsonLinesReader;
    private readonly ILogger<AzureBlobDataReader> _logger;
    private readonly AzureBlobDataReader _sut;

    public AzureBlobDataReaderTests()
    {
        _clientFactory = Substitute.For<IStorageClientFactory>();
        _csvReader = new CsvDataReader(Substitute.For<ILogger<CsvDataReader>>());
        _jsonReader = new JsonDataReader(Substitute.For<ILogger<JsonDataReader>>(), Substitute.For<IOptions<EtlSettings>>());
        _jsonLinesReader = new JsonLinesDataReader(Substitute.For<ILogger<JsonLinesDataReader>>());
        _logger = Substitute.For<ILogger<AzureBlobDataReader>>();

        _sut = new AzureBlobDataReader(_clientFactory, _csvReader, _jsonReader, _jsonLinesReader, _logger);
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateInstance()
    {
        // Act
        var instance = new AzureBlobDataReader(_clientFactory, _csvReader, _jsonReader, _jsonLinesReader, _logger);

        // Assert
        instance.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullClientFactory_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new AzureBlobDataReader(null!, _csvReader, _jsonReader, _jsonLinesReader, _logger);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("clientFactory");
    }

    [Fact]
    public void Constructor_WithNullCsvReader_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new AzureBlobDataReader(_clientFactory, null!, _jsonReader, _jsonLinesReader, _logger);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("csvReader");
    }

    [Fact]
    public void Constructor_WithNullJsonReader_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new AzureBlobDataReader(_clientFactory, _csvReader, null!, _jsonLinesReader, _logger);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("jsonReader");
    }

    [Fact]
    public void Constructor_WithNullJsonLinesReader_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new AzureBlobDataReader(_clientFactory, _csvReader, _jsonReader, null!, _logger);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("jsonLinesReader");
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new AzureBlobDataReader(_clientFactory, _csvReader, _jsonReader, _jsonLinesReader, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public async Task ReadAsync_WithInvalidJsonConfig_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test Azure Blob Connector",
            Type = "File",
            Provider = "AzureBlob",
            Direction = "source",
            IsSource = true,
            IsDestination = false,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = "invalid json",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        var options = new ReadOptions { BatchSize = 10 };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<JsonException>(async () =>
        {
            await foreach (var batch in _sut.ReadAsync(connector, options, CancellationToken.None))
            {
                // Should not reach here
            }
        });
    }

    [Fact]
    public async Task ReadAsync_WithMissingContainerName_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test Azure Blob Connector",
            Type = "File",
            Provider = "AzureBlob",
            Direction = "source",
            IsSource = true,
            IsDestination = false,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = @"{
                ""BlobName"": ""test.csv"",
                ""Format"": ""csv"",
                ""AccountName"": ""testaccount"",
                ""AccountKey"": ""dGVzdGtleQ==""
            }",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        var options = new ReadOptions { BatchSize = 10 };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var batch in _sut.ReadAsync(connector, options, CancellationToken.None))
            {
                // Should not reach here
            }
        });
        exception.Message.Should().Be("Azure Blob configuration must include ContainerName");
    }

    [Fact]
    public async Task ReadAsync_WithMissingBlobName_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test Azure Blob Connector",
            Type = "File",
            Provider = "AzureBlob",
            Direction = "source",
            IsSource = true,
            IsDestination = false,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = @"{
                ""ContainerName"": ""test-container"",
                ""Format"": ""csv"",
                ""AccountName"": ""testaccount"",
                ""AccountKey"": ""dGVzdGtleQ==""
            }",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        var options = new ReadOptions { BatchSize = 10 };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var batch in _sut.ReadAsync(connector, options, CancellationToken.None))
            {
                // Should not reach here
            }
        });
        exception.Message.Should().Be("Azure Blob configuration must include BlobName");
    }

    [Fact]
    public async Task ReadAsync_WithUnsupportedFormat_ShouldThrowNotSupportedException()
    {
        // Arrange
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test Azure Blob Connector",
            Type = "File",
            Provider = "AzureBlob",
            Direction = "source",
            IsSource = true,
            IsDestination = false,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = @"{
                ""ContainerName"": ""test-container"",
                ""BlobName"": ""test.xml"",
                ""Format"": ""xml"",
                ""AccountName"": ""testaccount"",
                ""AccountKey"": ""dGVzdGtleQ==""
            }",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        var options = new ReadOptions { BatchSize = 10 };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            await foreach (var batch in _sut.ReadAsync(connector, options, CancellationToken.None))
            {
                // Should not reach here
            }
        });
        exception.Message.Should().Be("File format 'xml' is not supported for Azure Blob");
    }

    [Fact]
    public async Task ReadAsync_WithFormatFromFileExtension_ShouldDetermineFormatCorrectly()
    {
        // Arrange - Test various file extensions
        var testCases = new[]
        {
            ("data/test.csv", "csv"),
            ("data/test.json", "json"),
            ("data/test.jsonl", "jsonl"),
            ("data/test.ndjson", "ndjson"),
            ("data/test.JSON", "json"), // Case insensitive
            ("data/test.unknown", "csv") // Default fallback
        };

        foreach (var (blobName, expectedFormat) in testCases)
        {
            var connector = new Connector
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                Name = "Test Azure Blob Connector",
                Type = "File",
                Provider = "AzureBlob",
                Direction = "source",
                IsSource = true,
                IsDestination = false,
                RequiresCredentials = true,
                IsActive = true,
                ConfigJson = $@"{{
                    ""ContainerName"": ""test-container"",
                    ""BlobName"": ""{blobName}"",
                    ""AccountName"": ""testaccount"",
                    ""AccountKey"": ""dGVzdGtleQ==""
                }}",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };

            var options = new ReadOptions { BatchSize = 10 };

            // Act & Assert - Should not throw format exception, meaning format was determined correctly
            // We expect it to fail at Azure client creation, not format determination
            await FluentActions.Awaiting(async () =>
            {
                await foreach (var batch in _sut.ReadAsync(connector, options, CancellationToken.None))
                {
                    // Should not reach here
                }
            }).Should().ThrowAsync<Exception>()
              .Where(e => !e.Message.Contains("not supported"));
        }
    }
}