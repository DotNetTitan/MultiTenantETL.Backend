using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.DataWriters;
using NSubstitute;
using Xunit;

namespace MultiTenantETL.UnitTests.DataWriters;

public class MongoDbDataWriterTests
{
    private readonly ILogger<MongoDbDataWriter> _logger;
    private readonly IEncryptionService _encryptionService;
    private readonly MongoDbDataWriter _sut;

    public MongoDbDataWriterTests()
    {
        _logger = Substitute.For<ILogger<MongoDbDataWriter>>();
        _encryptionService = Substitute.For<IEncryptionService>();
        _encryptionService.DecryptJsonFields(Arg.Any<JsonElement>(), Arg.Any<string[]>())
            .Returns(x => x.ArgAt<JsonElement>(0));

        _sut = new MongoDbDataWriter(_logger, _encryptionService);
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateInstance()
    {
        // Act
        var instance = new MongoDbDataWriter(_logger, _encryptionService);

        // Assert
        instance.Should().NotBeNull();
    }

    [Fact]
    public async Task WriteBatchAsync_WithInvalidJsonConfig_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var connector = new Connector 
        { 
            Name = "Test",
            Type = "Database",
            Provider = "MongoDb",
            Direction = "destination",
            ConfigJson = "invalid json" 
        };
        var batch = new ReadBatch { BatchId = Guid.NewGuid() };
        var options = new WriteOptions();

        // Act
        var act = () => _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<JsonException>();
    }

    [Fact]
    public async Task WriteBatchAsync_WithEmptyBatch_ShouldReturnSuccessResult()
    {
        // Arrange
        var connector = CreateValidConnector();
        var batch = new ReadBatch { BatchId = Guid.NewGuid(), Rows = new List<Dictionary<string, object?>>() };
        var options = new WriteOptions();

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.RowsWritten.Should().Be(0);
    }

    private static Connector CreateValidConnector()
    {
        return new Connector
        {
            Name = "Test",
            Type = "Database",
            Provider = "MongoDb",
            Direction = "destination",
            ConfigJson = @"{
                ""ConnectionString"": ""mongodb://localhost:27017"",
                ""Database"": ""test"",
                ""CollectionName"": ""test_collection""
            }"
        };
    }
}
