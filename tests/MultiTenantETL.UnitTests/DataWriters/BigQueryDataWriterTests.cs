using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;
using MultiTenantETL.Infrastructure.DataWriters;
using NSubstitute;
using Xunit;

namespace MultiTenantETL.UnitTests.DataWriters;

public class BigQueryDataWriterTests : IAsyncDisposable
{
    private readonly ILogger<BigQueryDataWriter> _logger;
    private readonly IOptions<EtlSettings> _settings;
    private readonly BigQueryDataWriter _sut;

    public BigQueryDataWriterTests()
    {
        _logger = Substitute.For<ILogger<BigQueryDataWriter>>();
        _settings = Options.Create(new EtlSettings
        {
            CommandTimeoutSeconds = 300
        });

        _sut = new BigQueryDataWriter(_logger, _settings);
    }

    public ValueTask DisposeAsync()
    {
        return _sut.DisposeAsync();
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateInstance()
    {
        // Act
        var instance = new BigQueryDataWriter(_logger, _settings);

        // Assert
        instance.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new BigQueryDataWriter(null!, _settings);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullSettings_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new BigQueryDataWriter(_logger, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("settings");
    }

    [Fact]
    public async Task WriteBatchAsync_WithEmptyRows_ShouldReturnEmptyResult()
    {
        // Arrange
        var connector = CreateTestConnector();
        var batch = new ReadBatch { BatchId = Guid.NewGuid() };
        var options = new WriteOptions { TruncateBeforeLoad = false };

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.RowsWritten.Should().Be(0);
        result.RowsFailed.Should().Be(0);
    }

    [Fact]
    public async Task WriteBatchAsync_WithInvalidConfig_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test BigQuery Connector",
            Type = "Database",
            Provider = "BigQuery",
            Direction = "destination",
            IsSource = false,
            IsDestination = true,
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
        var batch = new ReadBatch { BatchId = Guid.NewGuid() };
        batch.Rows.Add(new Dictionary<string, object?> { ["id"] = 1 });
        var options = new WriteOptions { TruncateBeforeLoad = false };

        // Act & Assert
        var act = () => _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ProjectId*");
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
            Direction = "destination",
            IsSource = false,
            IsDestination = true,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = JsonSerializer.Serialize(config),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
    }
}
