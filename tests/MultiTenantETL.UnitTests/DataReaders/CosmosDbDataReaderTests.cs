using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;
using MultiTenantETL.Infrastructure.DataReaders;
using NSubstitute;
using System.Text.Json;

namespace MultiTenantETL.UnitTests.DataReaders;

public class CosmosDbDataReaderTests
{
    private readonly ILogger<CosmosDbDataReader> _logger = Substitute.For<ILogger<CosmosDbDataReader>>();
    private readonly IOptions<EtlSettings> _settings = Substitute.For<IOptions<EtlSettings>>();
    private readonly ISecretResolver _secretResolver = Substitute.For<ISecretResolver>();
    private readonly CosmosDbDataReader _reader;

    public CosmosDbDataReaderTests()
    {
        _settings.Value.Returns(new EtlSettings());
        _secretResolver.ResolveSecretsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(x => Task.FromResult(JsonDocument.Parse(x.ArgAt<string>(0)).RootElement));
        _reader = new CosmosDbDataReader(_logger, _settings, _secretResolver);
    }

    [Fact]
    public async Task TestConnectionAsync_WithInvalidJsonConfig_ShouldReturnFalse()
    {
        // Arrange
        var connector = new Connector
        {
            Name = "Test Cosmos",
            Type = ConnectorTypes.Database,
            Provider = ConnectorProviders.CosmosDb,
            Direction = ConnectorDirections.Source,
            ConfigJson = "invalid-json"
        };

        // Act
        var result = await _reader.TestConnectionAsync(connector, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task TestConnectionAsync_WithMissingFields_ShouldReturnFalse()
    {
        // Arrange
        var connector = new Connector
        {
            Name = "Test Cosmos",
            Type = ConnectorTypes.Database,
            Provider = ConnectorProviders.CosmosDb,
            Direction = ConnectorDirections.Source,
            ConfigJson = "{}"
        };

        // Act
        var result = await _reader.TestConnectionAsync(connector, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DetectSchemaAsync_WithInvalidJsonConfig_ShouldReturnFailure()
    {
        // Arrange
        var connector = new Connector
        {
            Name = "Test Cosmos",
            Type = ConnectorTypes.Database,
            Provider = ConnectorProviders.CosmosDb,
            Direction = ConnectorDirections.Source,
            ConfigJson = "invalid-json"
        };

        // Act
        var result = await _reader.DetectSchemaAsync(connector, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
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
            Direction = ConnectorDirections.Source,
            ConfigJson = JsonSerializer.Serialize(config)
        };
    }
}
