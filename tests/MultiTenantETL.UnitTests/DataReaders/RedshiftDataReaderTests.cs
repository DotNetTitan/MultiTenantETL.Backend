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
using Npgsql;

namespace MultiTenantETL.UnitTests.DataReaders;

public class RedshiftDataReaderTests
{
    private readonly ILogger<RedshiftDataReader> _logger;
    private readonly IOptions<EtlSettings> _settings;
    private readonly IEncryptionService _encryptionService;
    private readonly RedshiftDataReader _sut;

    public RedshiftDataReaderTests()
    {
        _logger = Substitute.For<ILogger<RedshiftDataReader>>();
        _settings = Options.Create(new EtlSettings
        {
            CommandTimeoutSeconds = 300
        });
        _encryptionService = Substitute.For<IEncryptionService>();
        _encryptionService.DecryptJsonFields(Arg.Any<JsonElement>(), Arg.Any<string[]>())
            .Returns(x => x.ArgAt<JsonElement>(0));

        _sut = new RedshiftDataReader(_logger, _settings, _encryptionService);
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateInstance()
    {
        // Act
        var instance = new RedshiftDataReader(_logger, _settings, _encryptionService);

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
            Provider = "Redshift",
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
    public async Task ReadAsync_WithMissingConnectionString_ShouldThrowArgumentException()
    {
        // Arrange
        var connector = new Connector
        {
            Name = "Test",
            Type = "Database",
            Provider = "Redshift",
            Direction = "source",
            ConfigJson = @"{ ""TableName"": ""test"" }"
        };
        var options = new ReadOptions();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
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
            Provider = "Redshift",
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
            Provider = "Redshift",
            Direction = "source",
            ConfigJson = "invalid json"
        };

        // Act
        var result = await _sut.DetectSchemaAsync(connector, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    private static Connector CreateValidConnector()
    {
        return new Connector
        {
            Name = "Test",
            Type = "Database",
            Provider = "Redshift",
            Direction = "source",
            ConfigJson = @"{
                ""Host"": ""redshift.example.com"",
                ""Database"": ""dev"",
                ""Username"": ""admin"",
                ""Password"": ""pass"",
                ""TableName"": ""test_table""
            }"
        };
    }
}
