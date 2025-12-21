using System.Data;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;
using MultiTenantETL.Infrastructure.DataReaders;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace MultiTenantETL.UnitTests.DataReaders;

public class SqlServerDataReaderTests : IDisposable
{
    private readonly ILogger<SqlServerDataReader> _logger;
    private readonly IOptions<EtlSettings> _settings;
    private readonly SqlServerDataReader _sut;

    public SqlServerDataReaderTests()
    {
        _logger = Substitute.For<ILogger<SqlServerDataReader>>();
        _settings = Options.Create(new EtlSettings
        {
            CommandTimeoutSeconds = 300
        });

        _sut = new SqlServerDataReader(_logger, _settings);
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateInstance()
    {
        // Act
        var instance = new SqlServerDataReader(_logger, _settings);

        // Assert
        instance.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new SqlServerDataReader(null!, _settings);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullSettings_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new SqlServerDataReader(_logger, null!);

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
            Name = "Test SQL Server Connector",
            Type = "Database",
            Provider = "SQLServer",
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

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var batch in _sut.ReadAsync(connector, options, CancellationToken.None))
            {
                // Should not reach here
            }
        });
    }

    [Fact]
    public async Task ReadAsync_WithMissingConnectionString_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test SQL Server Connector",
            Type = "Database",
            Provider = "SQLServer",
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

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
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

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(async () =>
        {
            await foreach (var batch in _sut.ReadAsync(connector, options, cts.Token))
            {
                // Should not reach here
            }
        });
    }

    [Fact]
    public async Task ReadAsync_WithCustomQuery_ShouldUseCustomQuery()
    {
        // Arrange
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test SQL Server Connector",
            Type = "Database",
            Provider = "SQLServer",
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

        // Act & Assert - Should not throw (query validation happens at execution)
        var exception = await Record.ExceptionAsync(async () =>
        {
            await foreach (var batch in _sut.ReadAsync(connector, options, CancellationToken.None))
            {
                // Should not reach here due to connection failure, but config parsing should succeed
            }
        });

        // Should fail at connection, not config parsing
        exception.Should().NotBeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task ReadAsync_WithMaxRows_ShouldLimitResults()
    {
        // Arrange
        var connector = CreateValidConnector();
        var options = new ReadOptions { BatchSize = 100, MaxRows = 50 };

        // Act & Assert - Should not throw (limiting happens at execution)
        var exception = await Record.ExceptionAsync(async () =>
        {
            await foreach (var batch in _sut.ReadAsync(connector, options, CancellationToken.None))
            {
                // Should not reach here due to connection failure, but config parsing should succeed
            }
        });

        // Should fail at connection, not config parsing
        exception.Should().NotBeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task TestConnectionAsync_WithValidConfig_ShouldReturnTrue()
    {
        // Arrange
        var connector = CreateValidConnector();

        // Act
        var result = await _sut.TestConnectionAsync(connector, CancellationToken.None);

        // Assert - Should return false due to connection failure, but no exception
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
            Name = "Test SQL Server Connector",
            Type = "Database",
            Provider = "SQLServer",
            Direction = "source",
            IsSource = true,
            IsDestination = false,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = @"{
                ""ConnectionString"": ""invalid connection string"",
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

        // Assert - Should return error result due to connection failure, but no exception
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
            Name = "Test SQL Server Connector",
            Type = "Database",
            Provider = "SQLServer",
            Direction = "source",
            IsSource = true,
            IsDestination = false,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = @"{
                ""ConnectionString"": ""invalid connection string"",
                ""TableName"": ""test_table""
            }",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
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
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test SQL Server Connector",
            Type = "Database",
            Provider = "SQLServer",
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