using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;
using MultiTenantETL.Infrastructure.DataReaders;
using NSubstitute;
using Xunit;

namespace MultiTenantETL.UnitTests.DataReaders;

public class BigQueryDataReaderTests : IDisposable
{
    private readonly ILogger<BigQueryDataReader> _logger;
    private readonly IOptions<EtlSettings> _settings;
    private readonly BigQueryDataReader _sut;

    public BigQueryDataReaderTests()
    {
        _logger = Substitute.For<ILogger<BigQueryDataReader>>();
        _settings = Options.Create(new EtlSettings
        {
            CommandTimeoutSeconds = 300
        });

        _sut = new BigQueryDataReader(_logger, _settings);
    }

    public void Dispose()
    {
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateInstance()
    {
        // Act
        var instance = new BigQueryDataReader(_logger, _settings);

        // Assert
        instance.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new BigQueryDataReader(null!, _settings);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullSettings_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new BigQueryDataReader(_logger, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("settings");
    }

    [Fact]
    public async Task TestConnectionAsync_WithValidConfig_ShouldReturnFalseDueToRealConnectionMissing()
    {
        // Arrange
        var connector = CreateTestConnector();

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
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test BigQuery Connector",
            Type = "Database",
            Provider = "BigQuery",
            Direction = "source",
            IsSource = true,
            IsDestination = false,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = @"{
                ""ProjectId"": """",
                ""DatasetId"": ""test_dataset"",
                ""TableName"": ""test_table""
            }",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        // Act
        var result = await _sut.DetectSchemaAsync(connector, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("ProjectId");
    }

    private static Connector CreateTestConnector()
    {
        var config = new
        {
            ProjectId = "test-project",
            DatasetId = "test_dataset",
            TableName = "test_table"
        };

        return new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test BigQuery Connector",
            Type = "Database",
            Provider = "BigQuery",
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
