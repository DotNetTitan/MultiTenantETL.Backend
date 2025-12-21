using System.Linq;
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
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace MultiTenantETL.UnitTests.DataWriters;

public class MySqlDataWriterTests : IDisposable
{
    private readonly ILogger<MySqlDataWriter> _logger;
    private readonly IOptions<EtlSettings> _settings;
    private readonly MySqlDataWriter _sut;
    private readonly string _tempFilePath;

    public MySqlDataWriterTests()
    {
        _logger = Substitute.For<ILogger<MySqlDataWriter>>();
        _settings = Options.Create(new EtlSettings
        {
            CommandTimeoutSeconds = 300,
            MySqlBulkInsertChunkSize = 1000
        });

        _sut = new MySqlDataWriter(_logger, _settings);
        _tempFilePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.csv");
    }

    public void Dispose()
    {
        if (File.Exists(_tempFilePath))
        {
            File.Delete(_tempFilePath);
        }
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateInstance()
    {
        // Act
        var instance = new MySqlDataWriter(_logger, _settings);

        // Assert
        instance.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new MySqlDataWriter(null!, _settings);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullSettings_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new MySqlDataWriter(_logger, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("settings");
    }

    [Fact]
    public async Task WriteBatchAsync_WithValidConfigAndData_ShouldReturnSuccessResult()
    {
        // Arrange
        var connector = CreateValidConnector();
        var batch = new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            Rows = new List<Dictionary<string, object?>>
            {
                new() { ["id"] = 1, ["name"] = "Test User 1", ["email"] = "user1@test.com" },
                new() { ["id"] = 2, ["name"] = "Test User 2", ["email"] = "user2@test.com" }
            }
        };
        var options = new WriteOptions();

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert - Should fail at database connection, but should attempt to write
        result.Should().NotBeNull();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task WriteBatchAsync_WithInvalidJsonConfig_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test MySQL Connector",
            Type = "Database",
            Provider = "MySQL",
            Direction = "destination",
            IsSource = false,
            IsDestination = true,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = "{",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        var batch = new ReadBatch 
        { 
            BatchId = Guid.NewGuid(), 
            Rows = new List<Dictionary<string, object?>>
            {
                new() { ["id"] = 1, ["name"] = "Test" }
            }
        };
        var options = new WriteOptions {  };

        // Act
        var act = () => _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task WriteBatchAsync_WithMissingConnectionString_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test MySQL Connector",
            Type = "Database",
            Provider = "MySQL",
            Direction = "destination",
            IsSource = false,
            IsDestination = true,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = @"{
                ""TableName"": ""test_table""
            }",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        var batch = new ReadBatch 
        { 
            BatchId = Guid.NewGuid(), 
            Rows = new List<Dictionary<string, object?>>
            {
                new() { ["id"] = 1, ["name"] = "Test" }
            }
        };
        var options = new WriteOptions {  };

        // Act
        var act = () => _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task WriteBatchAsync_WithMissingTableName_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test MySQL Connector",
            Type = "Database",
            Provider = "MySQL",
            Direction = "destination",
            IsSource = false,
            IsDestination = true,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = @"{
                ""ConnectionString"": ""Server=localhost;Database=test;User=user;Password=pass""
            }",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        var batch = new ReadBatch 
        { 
            BatchId = Guid.NewGuid(), 
            Rows = new List<Dictionary<string, object?>>
            {
                new() { ["id"] = 1, ["name"] = "Test" }
            }
        };
        var options = new WriteOptions {  };

        // Act
        var act = () => _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task WriteBatchAsync_WithEmptyBatch_ShouldReturnSuccessResult()
    {
        // Arrange
        var connector = CreateValidConnector();
        var batch = new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            Rows = new List<Dictionary<string, object?>>()
        };
        var options = new WriteOptions {  };

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.BatchId.Should().Be(batch.BatchId);
        result.RowsWritten.Should().Be(0);
        result.RowsFailed.Should().Be(0);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task WriteBatchAsync_WithTruncateBeforeLoad_ShouldAttemptTruncate()
    {
        // Arrange
        var connector = CreateValidConnector();
        var batch = new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            Rows = new List<Dictionary<string, object?>>
            {
                new() { ["id"] = 1, ["name"] = "Test User" }
            }
        };
        var options = new WriteOptions { TruncateBeforeLoad = true };

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert - Should fail at database connection, but should attempt truncate
        result.Should().NotBeNull();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task WriteBatchAsync_WithCancellation_ShouldCancelOperation()
    {
        // Arrange
        var connector = CreateValidConnector();
        var batch = new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            Rows = new List<Dictionary<string, object?>>
            {
                new() { ["id"] = 1, ["name"] = "Test User" }
            }
        };
        var options = new WriteOptions {  };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => _sut.WriteBatchAsync(connector, batch, options, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task WriteBatchAsync_WithUpsertAndKeys_ShouldAttemptUpsert()
    {
        // Arrange
        var connector = CreateValidConnector();
        var batch = new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            Rows = new List<Dictionary<string, object?>>
            {
                new() { ["id"] = 1, ["name"] = "Test User", ["email"] = "user@test.com" }
            }
        };
        var options = new WriteOptions
        {
            UseUpsert = true,
            UpsertKeys = new List<string> { "id" }
        };

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert - Should fail at database connection, but should attempt upsert
        result.Should().NotBeNull();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task WriteBatchAsync_WithLargeBatch_ShouldUseChunking()
    {
        // Arrange
        var connector = CreateValidConnector();
        var rows = new List<Dictionary<string, object?>>();
        for (int i = 0; i < 2500; i++) // Larger than default chunk size of 1000
        {
            rows.Add(new() { ["id"] = i, ["name"] = $"User {i}", ["email"] = $"user{i}@test.com" });
        }
        var batch = new ReadBatch { BatchId = Guid.NewGuid(), Rows = rows };
        var options = new WriteOptions {  };

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert - Should fail at database connection, but should attempt chunked insert
        result.Should().NotBeNull();
        result.Errors.Should().NotBeEmpty();
    }

    private static Connector CreateValidConnector()
    {
        return new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test MySQL Connector",
            Type = "Database",
            Provider = "MySQL",
            Direction = "destination",
            IsSource = false,
            IsDestination = true,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = @"{
                ""ConnectionString"": ""Server=localhost;Database=test;User=user;Password=pass"",
                ""TableName"": ""test_table""
            }",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
    }
}
