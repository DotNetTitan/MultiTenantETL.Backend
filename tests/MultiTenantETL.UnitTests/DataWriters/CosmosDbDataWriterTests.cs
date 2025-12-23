using System.Text.Json;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.DataWriters;
using NSubstitute;
using FluentAssertions;
using Xunit;

namespace MultiTenantETL.UnitTests.DataWriters;

public class CosmosDbDataWriterTests
{
    private readonly ILogger<CosmosDbDataWriter> _logger = Substitute.For<ILogger<CosmosDbDataWriter>>();
    private readonly IEncryptionService _encryptionService = Substitute.For<IEncryptionService>();
    private readonly CosmosDbDataWriter _writer;

    public CosmosDbDataWriterTests()
    {
        _writer = new CosmosDbDataWriter(_logger, _encryptionService);

        // Default mock behavior for encryption service (no-op)
        _encryptionService.DecryptJsonFields(Arg.Any<JsonElement>(), Arg.Any<string[]>())
            .Returns(x => x[0]);
    }

    [Fact]
    public async Task WriteBatchAsync_WithInvalidJsonConfig_ShouldThrowJsonException()
    {
        // Arrange
        var connector = new Connector
        {
            Name = "Test Cosmos",
            Type = ConnectorTypes.Database,
            Provider = ConnectorProviders.CosmosDb,
            Direction = ConnectorDirections.Destination,
            ConfigJson = "invalid-json"
        };
        var batch = new ReadBatch { BatchId = Guid.NewGuid(), Rows = new List<Dictionary<string, object?>>() };
        var options = new WriteOptions { UseUpsert = false };

        // Act & Assert
        await Assert.ThrowsAsync<JsonException>(() => _writer.WriteBatchAsync(connector, batch, options, CancellationToken.None));
    }

    [Fact]
    public async Task WriteBatchAsync_WithMissingFields_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var connector = new Connector
        {
            Name = "Test Cosmos",
            Type = ConnectorTypes.Database,
            Provider = ConnectorProviders.CosmosDb,
            Direction = ConnectorDirections.Destination,
            ConfigJson = "{}"
        };
        var batch = new ReadBatch { BatchId = Guid.NewGuid(), Rows = new List<Dictionary<string, object?>>() };
        var options = new WriteOptions { UseUpsert = false };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _writer.WriteBatchAsync(connector, batch, options, CancellationToken.None));
    }

    private Connector CreateValidConnector()
    {
        var config = new
        {
            CosmosEndpoint = "https://localhost:8081",
            CosmosKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
            Database = "TestDb",
            Container = "TestContainer"
        };

        return new Connector
        {
            Id = Guid.NewGuid(),
            Name = "Valid Cosmos",
            Type = ConnectorTypes.Database,
            Provider = ConnectorProviders.CosmosDb,
            Direction = ConnectorDirections.Destination,
            ConfigJson = JsonSerializer.Serialize(config)
        };
    }
}
