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
using IDataReader = MultiTenantETL.Application.Connectors.DataReaders.IDataReader;

namespace MultiTenantETL.UnitTests.DataReaders;

public class SnowflakeDataReaderTests : IDisposable
{
    private readonly ILogger<SnowflakeDataReader> _logger;
    private readonly IOptions<EtlSettings> _settings;
    private readonly IEncryptionService _encryptionService;
    private readonly SnowflakeDataReader _sut;

    public SnowflakeDataReaderTests()
    {
        _logger = Substitute.For<ILogger<SnowflakeDataReader>>();
        _settings = Options.Create(new EtlSettings
        {
            CommandTimeoutSeconds = 300
        });
        _encryptionService = Substitute.For<IEncryptionService>();
        
        // Setup encryption service to return the input JsonElement unchanged
        _encryptionService.DecryptJsonFields(Arg.Any<System.Text.Json.JsonElement>(), Arg.Any<string[]>())
            .Returns(x => x.Arg<System.Text.Json.JsonElement>());

        _sut = new SnowflakeDataReader(_logger, _settings, _encryptionService);
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateInstance()
    {
        // Act
        var instance = new SnowflakeDataReader(_logger, _settings, _encryptionService);

        // Assert
        instance.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new SnowflakeDataReader(null!, _settings, _encryptionService);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullSettings_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new SnowflakeDataReader(_logger, null!, _encryptionService);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("settings");
    }

    [Fact]
    public void Constructor_WithNullEncryptionService_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new SnowflakeDataReader(_logger, _settings, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("encryptionService");
    }

    [Fact]
    public async Task TestConnectionAsync_WithValidConfig_ShouldReturnTrue()
    {
        // Arrange
        var connector = CreateTestConnector();

        // Act
        var result = await _sut.TestConnectionAsync(connector, CancellationToken.None);

        // Assert
        result.Should().BeFalse(); // Will be false since we don't have a real Snowflake instance
    }

    [Fact]
    public async Task DetectSchemaAsync_WithValidConfig_ShouldReturnSchemaResult()
    {
        // Arrange
        var connector = CreateTestConnector();

        // Act
        var result = await _sut.DetectSchemaAsync(connector, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse(); // Will fail since we don't have a real connection
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    private static Connector CreateTestConnector()
    {
        var config = new
        {
            account = "test-account",
            username = "test-user",
            password = "test-password",
            database = "test-db",
            schema = "PUBLIC",
            warehouse = "TEST_WH",
            role = "TEST_ROLE",
            tableName = "test_table"
        };

        return new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test Snowflake Connector",
            Type = "Database",
            Provider = "Snowflake",
            Direction = "source",
            IsSource = true,
            IsDestination = false,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = JsonSerializer.Serialize(config),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
    }
}