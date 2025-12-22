using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.DataWriters;
using MultiTenantETL.Infrastructure.Services.Storage;
using NSubstitute;
using Xunit;

namespace MultiTenantETL.UnitTests.DataWriters;

public class GcsDataWriterTests
{
    private readonly IStorageClientFactory _clientFactory;
    private readonly ILogger<GcsDataWriter> _logger;
    private readonly GcsDataWriter _sut;

    public GcsDataWriterTests()
    {
        _clientFactory = Substitute.For<IStorageClientFactory>();
        _logger = Substitute.For<ILogger<GcsDataWriter>>();
        _sut = new GcsDataWriter(_clientFactory, _logger);
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateInstance()
    {
        // Act
        var instance = new GcsDataWriter(_clientFactory, _logger);

        // Assert
        instance.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullClientFactory_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new GcsDataWriter(null!, _logger);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("clientFactory");
    }

    [Fact]
    public async Task WriteBatchAsync_WithInvalidJsonConfig_ShouldReturnFailedResult()
    {
        // Arrange
        var connector = new Connector 
        { 
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test GCS",
            Type = "File",
            Provider = "GCS",
            Direction = "destination",
            ConfigJson = "{ invalid" 
        };
        var batch = new ReadBatch 
        { 
            BatchId = Guid.NewGuid(), 
            Rows = new() { new() { ["id"] = 1 } },
            RowCount = 1
        };

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, new WriteOptions(), CancellationToken.None);

        // Assert
        result.RowsWritten.Should().Be(0);
        result.RowsFailed.Should().Be(1);
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task WriteBatchAsync_WithValidConfig_ShouldReturnSuccessResult()
    {
        // Arrange
        var config = new { ProjectId = "p", JsonCredentials = "{}", Bucket = "b", Key = "k.csv", Format = "csv" };
        var connector = new Connector 
        { 
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test GCS",
            Type = "File",
            Provider = "GCS",
            Direction = "destination",
            ConfigJson = JsonSerializer.Serialize(config) 
        };
        var batch = new ReadBatch 
        { 
            BatchId = Guid.NewGuid(), 
            Rows = new() { new() { ["id"] = 1, ["name"] = "test" } },
            RowCount = 1
        };

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, new WriteOptions(), CancellationToken.None);

        // Assert
        result.RowsWritten.Should().Be(1);
        result.RowsFailed.Should().Be(0);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task DisposeAsync_WithInitializedStream_ShouldNotThrow()
    {
        // Arrange
        var config = new { ProjectId = "p", JsonCredentials = "{}", Bucket = "b", Key = "k.csv", Format = "csv" };
        var connector = new Connector 
        { 
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test GCS",
            Type = "File",
            Provider = "GCS",
            Direction = "destination",
            ConfigJson = JsonSerializer.Serialize(config) 
        };
        var batch = new ReadBatch 
        { 
            Rows = new() { new() { ["id"] = 1 } },
            RowCount = 1
        };
        
        var storageClient = Substitute.For<Google.Cloud.Storage.V1.StorageClient>();
        _clientFactory.CreateGcsClient(Arg.Any<string>(), Arg.Any<string>()).Returns(storageClient);

        await _sut.WriteBatchAsync(connector, batch, new WriteOptions(), CancellationToken.None);

        // Act & Assert
        await _sut.Awaiting(x => x.DisposeAsync().AsTask()).Should().NotThrowAsync();
    }
}
