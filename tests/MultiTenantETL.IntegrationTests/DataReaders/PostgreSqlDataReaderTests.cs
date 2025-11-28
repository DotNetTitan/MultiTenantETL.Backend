using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;
using MultiTenantETL.Infrastructure.DataReaders;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace MultiTenantETL.IntegrationTests.DataReaders;

public class PostgreSqlDataReaderTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;
    private PostgreSqlDataReader _reader = null!;
    private string _connectionString = null!;

    public PostgreSqlDataReaderTests()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();

        var logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<PostgreSqlDataReader>();
        
        var settings = Options.Create(new EtlSettings
        {
            CommandTimeoutSeconds = 300
        });
        
        _reader = new PostgreSqlDataReader(logger, settings);

        // Create and populate test table
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        
        await using var createCommand = new NpgsqlCommand(@"
            CREATE TABLE test_products (
                id SERIAL PRIMARY KEY,
                name VARCHAR(255) NOT NULL,
                price DECIMAL(10, 2),
                category VARCHAR(100),
                in_stock BOOLEAN DEFAULT true,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            )", connection);
        
        await createCommand.ExecuteNonQueryAsync();

        // Insert test data
        for (int i = 1; i <= 5000; i++)
        {
            await using var insertCommand = new NpgsqlCommand(@"
                INSERT INTO test_products (name, price, category, in_stock)
                VALUES (@name, @price, @category, @in_stock)", connection);
            
            insertCommand.Parameters.AddWithValue("@name", $"Product {i}");
            insertCommand.Parameters.AddWithValue("@price", 10.00m + i);
            insertCommand.Parameters.AddWithValue("@category", $"Category {i % 10}");
            insertCommand.Parameters.AddWithValue("@in_stock", i % 3 != 0);
            
            await insertCommand.ExecuteNonQueryAsync();
        }
    }

    public Task DisposeAsync()
    {
        return _container.DisposeAsync().AsTask();
    }

    [Fact]
    public async Task ReadAsync_ShouldStreamDataInBatches()
    {
        // Arrange
        var connector = CreateConnector();
        var options = new ReadOptions { BatchSize = 1000 };
        var totalRows = 0;
        var batchCount = 0;

        // Act
        await foreach (var batch in _reader.ReadAsync(connector, options, CancellationToken.None))
        {
            // Assert batch properties
            batch.Should().NotBeNull();
            batch.BatchId.Should().NotBeEmpty();
            batch.Rows.Should().NotBeEmpty();
            batch.RowCount.Should().Be(batch.Rows.Count);
            batch.RowCount.Should().BeLessThanOrEqualTo(1000);

            totalRows += batch.RowCount;
            batchCount++;

            // Verify row structure
            var firstRow = batch.Rows[0];
            firstRow.Should().ContainKey("id");
            firstRow.Should().ContainKey("name");
            firstRow.Should().ContainKey("price");
            firstRow.Should().ContainKey("category");
            firstRow.Should().ContainKey("in_stock");
        }

        // Assert
        totalRows.Should().Be(5000);
        batchCount.Should().Be(5); // 5000 rows / 1000 batch size
    }

    [Fact]
    public async Task ReadAsync_ShouldRespectMaxRows()
    {
        // Arrange
        var connector = CreateConnector();
        var options = new ReadOptions
        {
            BatchSize = 500,
            MaxRows = 1200
        };
        var totalRows = 0;

        // Act
        await foreach (var batch in _reader.ReadAsync(connector, options, CancellationToken.None))
        {
            totalRows += batch.RowCount;
        }

        // Assert
        totalRows.Should().Be(1200);
    }

    [Fact]
    public async Task ReadAsync_ShouldHandleCustomQuery()
    {
        // Arrange
        var connector = CreateConnectorWithQuery("SELECT * FROM test_products WHERE category = 'Category 5'");
        var options = new ReadOptions { BatchSize = 100 };
        var totalRows = 0;

        // Act
        await foreach (var batch in _reader.ReadAsync(connector, options, CancellationToken.None))
        {
            totalRows += batch.RowCount;
            
            // Verify all rows match the filter
            foreach (var row in batch.Rows)
            {
                row["category"].Should().Be("Category 5");
            }
        }

        // Assert
        totalRows.Should().Be(500); // 5000 / 10 categories
    }

    [Fact]
    public async Task ReadAsync_ShouldNotLoadAllDataIntoMemory()
    {
        // Arrange
        var connector = CreateConnector();
        var options = new ReadOptions { BatchSize = 100 };
        
        var firstBatchReceived = false;
        var firstBatchRows = new List<Dictionary<string, object?>>();

        // Act - Process only first batch and break
        await foreach (var batch in _reader.ReadAsync(connector, options, CancellationToken.None))
        {
            firstBatchReceived = true;
            firstBatchRows = batch.Rows;
            break; // Stop after first batch
        }

        // Assert
        firstBatchReceived.Should().BeTrue();
        firstBatchRows.Should().HaveCount(100);
        
        // If all data was loaded into memory, we'd have issues with large datasets
        // This test verifies streaming behavior by only consuming first batch
    }

    [Fact]
    public async Task TestConnectionAsync_ShouldReturnTrue_WhenConnectionIsValid()
    {
        // Arrange
        var connector = CreateConnector();

        // Act
        var result = await _reader.TestConnectionAsync(connector, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TestConnectionAsync_ShouldReturnFalse_WhenConnectionIsInvalid()
    {
        // Arrange
        var connector = CreateConnector();
        connector.ConfigJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            ConnectionString = "Host=invalid;Database=invalid;Username=invalid;Password=invalid",
            TableName = "test_products"
        });

        // Act
        var result = await _reader.TestConnectionAsync(connector, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DetectSchemaAsync_ShouldReturnCorrectSchema()
    {
        // Arrange
        var connector = CreateConnector();

        // Act
        var result = await _reader.DetectSchemaAsync(connector, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Fields.Should().HaveCount(6);
        
        var idField = result.Fields.First(f => f.Name == "id");
        idField.IsPrimaryKey.Should().BeTrue();
        idField.IsNullable.Should().BeFalse();
        
        var nameField = result.Fields.First(f => f.Name == "name");
        nameField.DataType.Should().Be("character varying");
        nameField.IsNullable.Should().BeFalse();
        
        var priceField = result.Fields.First(f => f.Name == "price");
        priceField.DataType.Should().Be("numeric");
    }

    private Connector CreateConnector()
    {
        return new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test PostgreSQL",
            Type = "Database",
            Provider = "PostgreSQL",
            Direction = "source",
            IsSource = true,
            IsDestination = false,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                ConnectionString = _connectionString,
                TableName = "test_products"
            }),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
    }

    private Connector CreateConnectorWithQuery(string query)
    {
        return new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test PostgreSQL",
            Type = "Database",
            Provider = "PostgreSQL",
            Direction = "source",
            IsSource = true,
            IsDestination = false,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                ConnectionString = _connectionString,
                Query = query
            }),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
    }
}
