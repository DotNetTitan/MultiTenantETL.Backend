using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;
using MultiTenantETL.Infrastructure.DataReaders;
using NSubstitute;
using Xunit;

namespace MultiTenantETL.UnitTests.DataReaders;

public class MongoDbDataReaderTests
{
    private readonly ILogger<MongoDbDataReader> _logger;
    private readonly IOptions<EtlSettings> _settings;
    private readonly IEncryptionService _encryptionService;
    private readonly MongoDbDataReader _sut;

    public MongoDbDataReaderTests()
    {
        _logger = Substitute.For<ILogger<MongoDbDataReader>>();
        _settings = Options.Create(new EtlSettings());
        _encryptionService = Substitute.For<IEncryptionService>();
        _encryptionService.DecryptJsonFields(Arg.Any<JsonElement>(), Arg.Any<string[]>())
            .Returns(x => x.ArgAt<JsonElement>(0));

        _sut = new MongoDbDataReader(_logger, _settings, _encryptionService);
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateInstance()
    {
        // Act
        var instance = new MongoDbDataReader(_logger, _settings, _encryptionService);

        // Assert
        instance.Should().NotBeNull();
    }

    [Fact]
    public async Task ReadAsync_WithInvalidJsonConfig_ShouldThrowJsonException()
    {
        // Arrange
        var connector = new Connector 
        { 
            Name = "Test",
            Type = "Database",
            Provider = "MongoDb",
            Direction = "source",
            ConfigJson = "invalid json" 
        };
        var options = new ReadOptions();

        // Act & Assert
        await Assert.ThrowsAsync<JsonException>(async () =>
        {
            await foreach (var batch in _sut.ReadAsync(connector, options, CancellationToken.None))
            {
            }
        });
    }

    [Fact]
    public async Task TestConnectionAsync_WithInvalidConfig_ShouldReturnFalse()
    {
        // Arrange
        var connector = new Connector 
        { 
            Name = "Test",
            Type = "Database",
            Provider = "MongoDb",
            Direction = "source",
            ConfigJson = "invalid json" 
        };

        // Act
        var result = await _sut.TestConnectionAsync(connector, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DetectSchemaAsync_WithInvalidConfig_ShouldReturnErrorResult()
    {
        // Arrange
        var connector = new Connector 
        { 
            Name = "Test",
            Type = "Database",
            Provider = "MongoDb",
            Direction = "source",
            ConfigJson = "invalid json" 
        };

        // Act
        var result = await _sut.DetectSchemaAsync(connector, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }
}
