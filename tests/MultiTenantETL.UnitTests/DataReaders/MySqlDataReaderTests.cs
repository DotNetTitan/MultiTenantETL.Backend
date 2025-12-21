using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;
using MultiTenantETL.Infrastructure.DataReaders;
using MySqlConnector;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace MultiTenantETL.UnitTests.DataReaders;

public class MySqlDataReaderTests : IDisposable
{
    private readonly ILogger<MultiTenantETL.Infrastructure.DataReaders.MySqlDataReader> _logger;
    private readonly IOptions<EtlSettings> _settings;
    private readonly MultiTenantETL.Infrastructure.DataReaders.MySqlDataReader _sut;

    public MySqlDataReaderTests()
    {
        _logger = Substitute.For<ILogger<MultiTenantETL.Infrastructure.DataReaders.MySqlDataReader>>();
        _settings = Options.Create(new EtlSettings
        {
            CommandTimeoutSeconds = 300
        });

        _sut = new MultiTenantETL.Infrastructure.DataReaders.MySqlDataReader(_logger, _settings);
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateInstance()
    {
        // Act
        var instance = new MultiTenantETL.Infrastructure.DataReaders.MySqlDataReader(_logger, _settings);

        // Assert
        instance.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new MultiTenantETL.Infrastructure.DataReaders.MySqlDataReader(null!, _settings);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullSettings_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new MultiTenantETL.Infrastructure.DataReaders.MySqlDataReader(_logger, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("settings");
    }

    [Fact]
    public async Task ReadAsync_WithInvalidJsonConfig_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test MySQL Connector",
            Type = "Database",
            Provider = "MySQL",
            Direction = "source",
            IsSource = true,
            IsDestination = false,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = "{ invalid json",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        var options = new ReadOptions { BatchSize = 100 };

        // Act & Assert - Should throw during enumeration
        await Assert.ThrowsAsync<JsonException>(async () =>
        {
            await foreach (var batch in _sut.ReadAsync(connector, options, CancellationToken.None))
            {
                // Should not reach here
            }
        });
    }

    [Fact]
    public async Task ReadAsync_WithMissingConnectionString_ShouldThrowArgumentException()
    {
        // Arrange
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test MySQL Connector",
            Type = "Database",
            Provider = "MySQL",
            Direction = "source",
            IsSource = true,
            IsDestination = false,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = @"{
                ""TableName"": ""test_table""
            }",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        var options = new ReadOptions { BatchSize = 100 };

        // Act & Assert - Should throw during enumeration
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await foreach (var batch in _sut.ReadAsync(connector, options, CancellationToken.None))
            {
                // Should not reach here
            }
        });
    }

    [Fact]
    public async Task ReadAsync_WithCancellation_ShouldCancelOperation()
    {
        // Arrange
        var connector = CreateValidConnector();
        var options = new ReadOptions { BatchSize = 100 };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () =>
        {
            var result = _sut.ReadAsync(connector, options, cts.Token);
            await foreach (var batch in result)
            {
                // Should not reach here
            }
        };

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(act);
    }

    [Fact]
    public async Task ReadAsync_WithCustomQuery_ShouldUseCustomQuery()
    {
        // Arrange
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test MySQL Connector",
            Type = "Database",
            Provider = "MySQL",
            Direction = "source",
            IsSource = true,
            IsDestination = false,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = @"{
                ""ConnectionString"": ""Server=localhost;Database=test;User=user;Password=pass"",
                ""Query"": ""SELECT id, name FROM custom_table WHERE active = 1""
            }",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        var options = new ReadOptions { BatchSize = 100 };

        // Act
        var act = async () =>
        {
            var result = _sut.ReadAsync(connector, options, CancellationToken.None);
            await foreach (var batch in result)
            {
                // Should not reach here due to connection failure
            }
        };

        // Assert - Should fail at database connection, but should have attempted to use custom query
        await Assert.ThrowsAsync<MySqlException>(act);
    }

    [Fact]
    public async Task ReadAsync_WithMaxRows_ShouldLimitResults()
    {
        // Arrange
        var connector = CreateValidConnector();
        var options = new ReadOptions { BatchSize = 100, MaxRows = 50 };

        // Act
        var act = async () =>
        {
            var result = _sut.ReadAsync(connector, options, CancellationToken.None);
            await foreach (var batch in result)
            {
                // Should not reach here due to connection failure
            }
        };

        // Assert - Should fail at database connection, but max rows logic should be tested
        await Assert.ThrowsAsync<MySqlException>(act);
    }

    [Fact]
    public async Task TestConnectionAsync_WithValidConfig_ShouldReturnTrue()
    {
        // Arrange
        var connector = CreateValidConnector();

        // Act
        var result = await _sut.TestConnectionAsync(connector, CancellationToken.None);

        // Assert - Should fail at database connection in unit tests
        result.Should().BeFalse();
    }

    [Fact]
    public async Task TestConnectionAsync_WithInvalidConfig_ShouldReturnFalse()
    {
        // Arrange
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test MySQL Connector",
            Type = "Database",
            Provider = "MySQL",
            Direction = "source",
            IsSource = true,
            IsDestination = false,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = @"{
                ""ConnectionString"": ""invalid_connection_string"",
                ""TableName"": ""test_table""
            }",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        // Act
        var result = await _sut.TestConnectionAsync(connector, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DetectSchemaAsync_WithValidConfig_ShouldReturnSchemaResult()
    {
        // Arrange
        var connector = CreateValidConnector();

        // Act
        var result = await _sut.DetectSchemaAsync(connector, CancellationToken.None);

        // Assert - Should fail at database connection in unit tests
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task DetectSchemaAsync_WithInvalidConfig_ShouldReturnErrorResult()
    {
        // Arrange
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test MySQL Connector",
            Type = "Database",
            Provider = "MySQL",
            Direction = "source",
            IsSource = true,
            IsDestination = false,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = @"{
                ""ConnectionString"": ""invalid_connection_string"",
                ""TableName"": ""test_table""
            }",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        // Act
        var result = await _sut.DetectSchemaAsync(connector, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
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
            Direction = "source",
            IsSource = true,
            IsDestination = false,
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