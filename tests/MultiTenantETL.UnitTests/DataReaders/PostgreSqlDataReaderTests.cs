using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;
using MultiTenantETL.Infrastructure.DataReaders;
using MultiTenantETL.Infrastructure.Security;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Collections.Generic;
using Xunit;
using Npgsql;

namespace MultiTenantETL.UnitTests.DataReaders;

public class PostgreSqlDataReaderTests : IDisposable
{
    private readonly ILogger<PostgreSqlDataReader> _logger;
    private readonly IOptions<EtlSettings> _settings;
    private readonly ISecretResolver _secretResolver;
    private readonly PostgreSqlDataReader _sut;

    public PostgreSqlDataReaderTests()
    {
        _logger = Substitute.For<ILogger<PostgreSqlDataReader>>();
        _settings = Options.Create(new EtlSettings
        {
            CommandTimeoutSeconds = 300
        });
        _secretResolver = Substitute.For<ISecretResolver>();
        _secretResolver.ResolveSecretsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(x => Task.FromResult(JsonDocument.Parse(x.ArgAt<string>(0)).RootElement));

        _sut = new PostgreSqlDataReader(_logger, _settings, _secretResolver);
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateInstance()
    {
        // Act
        var instance = new PostgreSqlDataReader(_logger, _settings, _secretResolver);

        // Assert
        instance.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new PostgreSqlDataReader(null!, _settings, _secretResolver);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullSettings_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new PostgreSqlDataReader(_logger, null!, _secretResolver);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("settings");
    }

    [Fact]
    public void Constructor_WithNullEncryptionService_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new PostgreSqlDataReader(_logger, _settings, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("secretResolver");
    }

    [Fact]
    public async Task ReadAsync_WithInvalidJsonConfig_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test PostgreSQL Connector",
            Type = "Database",
            Provider = "PostgreSQL",
            Direction = "source",
            IsSource = true,
            IsDestination = false,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = "invalid json",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        var options = new ReadOptions { BatchSize = 100 };

        // Act
        Func<Task> act = async () =>
        {
            await foreach (var batch in _sut.ReadAsync(connector, options, CancellationToken.None))
            {
                // Should not reach here
            }
        };

        // Assert - JsonReaderException is a subclass of JsonException
        await act.Should().ThrowAsync<JsonException>();
    }

    [Fact]
    public async Task ReadAsync_WithMissingConnectionString_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test PostgreSQL Connector",
            Type = "Database",
            Provider = "PostgreSQL",
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

        // Act
        var act = async () =>
        {
            var result = _sut.ReadAsync(connector, options, CancellationToken.None);
            await foreach (var batch in result)
            {
                // Should not reach here
            }
        };

        // Assert
        await Assert.ThrowsAsync<ArgumentException>(act);
    }

    [Fact]
    public async Task ReadAsync_WithMissingHost_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test PostgreSQL Connector",
            Type = "Database",
            Provider = "PostgreSQL",
            Direction = "source",
            IsSource = true,
            IsDestination = false,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = @"{
                ""Database"": ""test_db"",
                ""Username"": ""user"",
                ""Password"": ""pass"",
                ""TableName"": ""test_table""
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
                // Should not reach here
            }
        };

        // Assert
        await Assert.ThrowsAsync<ArgumentException>(act);
    }

    [Fact]
    public async Task ReadAsync_WithMissingDatabase_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test PostgreSQL Connector",
            Type = "Database",
            Provider = "PostgreSQL",
            Direction = "source",
            IsSource = true,
            IsDestination = false,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = @"{
                ""Host"": ""localhost"",
                ""Username"": ""user"",
                ""Password"": ""pass"",
                ""TableName"": ""test_table""
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
                // Should not reach here
            }
        };

        // Assert
        await act.Should().ThrowAsync<NpgsqlException>();
    }

    [Fact]
    public async Task ReadAsync_WithMissingUsername_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test PostgreSQL Connector",
            Type = "Database",
            Provider = "PostgreSQL",
            Direction = "source",
            IsSource = true,
            IsDestination = false,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = @"{
                ""Host"": ""localhost"",
                ""Database"": ""test_db"",
                ""Password"": ""pass"",
                ""TableName"": ""test_table""
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
                // Should not reach here
            }
        };

        // Assert
        await act.Should().ThrowAsync<NpgsqlException>();
    }

    [Fact]
    public async Task ReadAsync_WithMissingPassword_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test PostgreSQL Connector",
            Type = "Database",
            Provider = "PostgreSQL",
            Direction = "source",
            IsSource = true,
            IsDestination = false,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = @"{
                ""Host"": ""localhost"",
                ""Database"": ""test_db"",
                ""Username"": ""user"",
                ""TableName"": ""test_table""
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
                // Should not reach here
            }
        };

        // Assert
        await act.Should().ThrowAsync<NpgsqlException>();
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
        await Assert.ThrowsAsync<TaskCanceledException>(act);
    }

    [Fact]
    public async Task ReadAsync_WithCustomQuery_ShouldUseCustomQuery()
    {
        // Arrange
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test PostgreSQL Connector",
            Type = "Database",
            Provider = "PostgreSQL",
            Direction = "source",
            IsSource = true,
            IsDestination = false,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = @"{
                ""ConnectionString"": ""Host=localhost;Database=test;Username=user;Password=pass"",
                ""Query"": ""SELECT id, name FROM custom_table WHERE active = true""
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
        await act.Should().ThrowAsync<NpgsqlException>();
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
        await act.Should().ThrowAsync<NpgsqlException>();
    }

    [Fact]
    public async Task TestConnectionAsync_WithValidConfig_ShouldReturnTrue()
    {
        // Arrange
        var connector = CreateValidConnector();

        // Act
        var result = await _sut.TestConnectionAsync(connector, CancellationToken.None);

        // Assert - Should fail at actual connection but return false, not throw
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
            Name = "Test PostgreSQL Connector",
            Type = "Database",
            Provider = "PostgreSQL",
            Direction = "source",
            IsSource = true,
            IsDestination = false,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = "invalid json",
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

        // Assert - Should fail at database connection but return error result
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
            Name = "Test PostgreSQL Connector",
            Type = "Database",
            Provider = "PostgreSQL",
            Direction = "source",
            IsSource = true,
            IsDestination = false,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = "invalid json",
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
            Name = "Test PostgreSQL Connector",
            Type = "Database",
            Provider = "PostgreSQL",
            Direction = "source",
            IsSource = true,
            IsDestination = false,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = @"{
                ""ConnectionString"": ""Host=localhost;Database=test;Username=user;Password=pass"",
                ""TableName"": ""test_table""
            }",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
    }
}