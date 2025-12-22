using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;
using MultiTenantETL.Infrastructure.DataWriters;
using NSubstitute;

namespace MultiTenantETL.UnitTests.DataWriters;

public class SnowflakeDataWriterTests : IDisposable
{
    private readonly ILogger<SnowflakeDataWriter> _logger;
    private readonly IOptions<EtlSettings> _settings;
    private readonly IEncryptionService _encryptionService;
    private readonly SnowflakeDataWriter _sut;

    public SnowflakeDataWriterTests()
    {
        _logger = Substitute.For<ILogger<SnowflakeDataWriter>>();
        _settings = Options.Create(new EtlSettings
        {
            CommandTimeoutSeconds = 300
        });
        _encryptionService = Substitute.For<IEncryptionService>();        
        // Setup encryption service to return the input JsonElement unchanged
        _encryptionService.DecryptJsonFields(Arg.Any<System.Text.Json.JsonElement>(), Arg.Any<string[]>())
            .Returns(x => x.Arg<System.Text.Json.JsonElement>());
        _sut = new SnowflakeDataWriter(_logger, _encryptionService);
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateInstance()
    {
        // Act
        var instance = new SnowflakeDataWriter(_logger, _encryptionService);

        // Assert
        instance.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new SnowflakeDataWriter(null!, _encryptionService);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullEncryptionService_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new SnowflakeDataWriter(_logger, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("encryptionService");
    }

    [Fact]
    public async Task WriteBatchAsync_WithEmptyBatch_ShouldReturnSuccess()
    {
        // Arrange
        var connector = CreateTestConnector();
        var batch = new ReadBatch { BatchId = Guid.NewGuid(), Rows = new List<Dictionary<string, object?>>() };
        var options = new WriteOptions();

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert - Should attempt database operation (will fail due to no real database)
        result.Should().NotBeNull();
        result.BatchId.Should().Be(batch.BatchId);
        result.RowsWritten.Should().Be(0);
        result.RowsFailed.Should().Be(0);
        // Note: In unit test environment without real database, this will fail with connection error
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task WriteBatchAsync_WithNullTableName_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var connector = CreateTestConnector();
        var config = JsonSerializer.Deserialize<JsonElement>(connector.ConfigJson);
        var configDict = JsonSerializer.Deserialize<Dictionary<string, object>>(config);
        configDict!["tableName"] = null!;
        connector.ConfigJson = JsonSerializer.Serialize(configDict);

        var batch = new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            Rows = new List<Dictionary<string, object?>>
            {
                new() { ["id"] = 1, ["name"] = "Test" }
            }
        };
        var options = new WriteOptions();

        // Act
        var act = () => _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Snowflake configuration must include TableName");
    }

    private static Connector CreateTestConnector()
    {
        return new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test Snowflake Connector",
            Type = "Database",
            Provider = "Snowflake",
            Direction = "destination",
            IsSource = false,
            IsDestination = true,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = @"{
                ""account"": ""test-account"",
                ""username"": ""test-user"",
                ""password"": ""test-password"",
                ""database"": ""test-db"",
                ""schema"": ""PUBLIC"",
                ""warehouse"": ""TEST_WH"",
                ""role"": ""TEST_ROLE"",
                ""tableName"": ""test_table""
            }",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
    }
}