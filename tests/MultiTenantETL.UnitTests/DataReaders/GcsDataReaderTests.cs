using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;
using MultiTenantETL.Infrastructure.DataReaders;
using MultiTenantETL.Infrastructure.Services.Storage;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace MultiTenantETL.UnitTests.DataReaders;

public class GcsDataReaderTests
{
    private readonly IStorageClientFactory _clientFactory;
    private readonly CsvDataReader _csvReader;
    private readonly JsonDataReader _jsonReader;
    private readonly JsonLinesDataReader _jsonLinesReader;
    private readonly ILogger<GcsDataReader> _logger;
    private readonly GcsDataReader _sut;

    public GcsDataReaderTests()
    {
        _clientFactory = Substitute.For<IStorageClientFactory>();
        _csvReader = new CsvDataReader(Substitute.For<ILogger<CsvDataReader>>());
        _jsonReader = new JsonDataReader(Substitute.For<ILogger<JsonDataReader>>(), Substitute.For<IOptions<EtlSettings>>());
        _jsonLinesReader = new JsonLinesDataReader(Substitute.For<ILogger<JsonLinesDataReader>>());
        _logger = Substitute.For<ILogger<GcsDataReader>>();

        _sut = new GcsDataReader(_clientFactory, _csvReader, _jsonReader, _jsonLinesReader, _logger);
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateInstance()
    {
        // Act
        var instance = new GcsDataReader(_clientFactory, _csvReader, _jsonReader, _jsonLinesReader, _logger);

        // Assert
        instance.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullClientFactory_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new GcsDataReader(null!, _csvReader, _jsonReader, _jsonLinesReader, _logger);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("clientFactory");
    }

    [Fact]
    public async Task ReadAsync_WithInvalidJsonConfig_ShouldThrowJsonException()
    {
        // Arrange
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test GCS",
            Type = "File",
            Provider = "GCS",
            Direction = "source",
            ConfigJson = "invalid json"
        };
        var options = new ReadOptions();

        // Act & Assert
        await FluentActions.Awaiting(() => 
            _sut.ReadAsync(connector, options, CancellationToken.None).GetAsyncEnumerator().MoveNextAsync().AsTask())
            .Should().ThrowAsync<JsonException>();
    }

    [Fact]
    public async Task ReadAsync_WithMissingBucket_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var config = new { ProjectId = "p", JsonCredentials = "{}", Key = "k" };
        var connector = new Connector 
        { 
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test GCS",
            Type = "File",
            Provider = "GCS",
            Direction = "source",
            ConfigJson = JsonSerializer.Serialize(config) 
        };
        
        // Act & Assert
        await FluentActions.Awaiting(() => 
            _sut.ReadAsync(connector, new ReadOptions(), CancellationToken.None).GetAsyncEnumerator().MoveNextAsync().AsTask())
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("GCS configuration must include BucketName");
    }

    [Fact]
    public async Task TestConnectionAsync_WithValidConfig_ShouldReturnTrue_WhenBucketExists()
    {
        // Arrange
        var config = new { ProjectId = "p", JsonCredentials = "{}", Bucket = "b", Key = "k" };
        var connector = new Connector 
        { 
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test GCS",
            Type = "File",
            Provider = "GCS",
            Direction = "source",
            ConfigJson = JsonSerializer.Serialize(config) 
        };
        var storageClient = Substitute.For<Google.Cloud.Storage.V1.StorageClient>();
        _clientFactory.CreateGcsClient(Arg.Any<string>(), Arg.Any<string>()).Returns(storageClient);

        // Act
        var result = await _sut.TestConnectionAsync(connector, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TestConnectionAsync_WithException_ShouldReturnFalse()
    {
        // Arrange
        var config = new { ProjectId = "p", JsonCredentials = "{}", Bucket = "b", Key = "k" };
        var connector = new Connector 
        { 
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test GCS",
            Type = "File",
            Provider = "GCS",
            Direction = "source",
            ConfigJson = JsonSerializer.Serialize(config) 
        };
        _clientFactory.CreateGcsClient(Arg.Any<string>(), Arg.Any<string>()).Throws(new Exception("Fail"));

        // Act
        var result = await _sut.TestConnectionAsync(connector, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }
}
