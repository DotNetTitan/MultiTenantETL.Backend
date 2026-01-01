using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;
using MultiTenantETL.Infrastructure.DataWriters;
using MultiTenantETL.IntegrationTests.TestUtilities;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace MultiTenantETL.IntegrationTests.DataWriters;

public class PostgreSqlDataWriterTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;
    private PostgreSqlDataWriter _writer = null!;
    private string _connectionString = null!;

    public PostgreSqlDataWriterTests()
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
            .CreateLogger<PostgreSqlDataWriter>();
        
        _writer = new PostgreSqlDataWriter(logger, new StubEncryptionService());

        // Create test table
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        
        await using var command = new NpgsqlCommand(@"
            CREATE TABLE test_users (
                id INTEGER PRIMARY KEY,
                email VARCHAR(255) UNIQUE NOT NULL,
                name VARCHAR(255),
                age INTEGER,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            )", connection);
        
        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        await _writer.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task WriteBatchAsync_ShouldInsertRows_WhenDataIsValid()
    {
        // Arrange
        var connector = CreateConnector();
        var batch = CreateTestBatch(5);
        var options = new WriteOptions { TruncateBeforeLoad = false };

        // Act
        var result = await _writer.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        result.RowsWritten.Should().Be(5);
        result.RowsFailed.Should().Be(0);
        result.Errors.Should().BeEmpty();
        result.RowErrors.Should().BeEmpty();

        var count = await GetRowCount();
        count.Should().Be(5);
    }

    [Fact]
    public async Task WriteBatchAsync_ShouldTruncateTable_WhenTruncateBeforeLoadIsTrue()
    {
        // Arrange
        var connector = CreateConnector();
        var batch1 = CreateTestBatch(3);
        var batch2 = CreateTestBatch(2, startId: 10);
        
        await _writer.WriteBatchAsync(connector, batch1, new WriteOptions(), CancellationToken.None);

        // Act
        var result = await _writer.WriteBatchAsync(connector, batch2, 
            new WriteOptions { TruncateBeforeLoad = true }, CancellationToken.None);

        // Assert
        result.RowsWritten.Should().Be(2);
        var count = await GetRowCount();
        count.Should().Be(2); // Only batch2 rows
    }

    [Fact]
    public async Task WriteBatchAsync_WithUpsert_ShouldInsertNewRows()
    {
        // Arrange
        var connector = CreateConnector();
        var batch = CreateTestBatch(3);
        var options = new WriteOptions
        {
            UseUpsert = true,
            UpsertKeys = new List<string> { "id" }
        };

        // Act
        var result = await _writer.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        result.RowsWritten.Should().Be(3);
        result.RowsFailed.Should().Be(0);
        
        var count = await GetRowCount();
        count.Should().Be(3);
    }

    [Fact]
    public async Task WriteBatchAsync_WithUpsert_ShouldUpdateExistingRows()
    {
        // Arrange
        var connector = CreateConnector();
        
        // Insert initial data
        var initialBatch = new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            Rows = new List<Dictionary<string, object?>>
            {
                new() { ["id"] = 1, ["email"] = "user1@test.com", ["name"] = "User One", ["age"] = 25 }
            },
            RowCount = 1
        };
        
        await _writer.WriteBatchAsync(connector, initialBatch, new WriteOptions(), CancellationToken.None);

        // Update with different name and age
        var updateBatch = new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            Rows = new List<Dictionary<string, object?>>
            {
                new() { ["id"] = 1, ["email"] = "user1@test.com", ["name"] = "Updated Name", ["age"] = 30 }
            },
            RowCount = 1
        };

        var options = new WriteOptions
        {
            UseUpsert = true,
            UpsertKeys = new List<string> { "id" }
        };

        // Act
        var result = await _writer.WriteBatchAsync(connector, updateBatch, options, CancellationToken.None);

        // Assert
        result.RowsWritten.Should().Be(1);
        result.RowsFailed.Should().Be(0);
        
        var count = await GetRowCount();
        count.Should().Be(1); // Still only 1 row

        var updatedName = await GetUserName(1);
        updatedName.Should().Be("Updated Name");
    }

    [Fact]
    public async Task WriteBatchAsync_WithUpsert_ShouldHandleMixedInsertAndUpdate()
    {
        // Arrange
        var connector = CreateConnector();
        
        // Insert initial row
        var initialBatch = CreateTestBatch(1);
        await _writer.WriteBatchAsync(connector, initialBatch, new WriteOptions(), CancellationToken.None);

        // Batch with update (id=1) and insert (id=2, id=3)
        var mixedBatch = new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            Rows = new List<Dictionary<string, object?>>
            {
                new() { ["id"] = 1, ["email"] = "user1@test.com", ["name"] = "Updated", ["age"] = 99 },
                new() { ["id"] = 2, ["email"] = "user2@test.com", ["name"] = "User Two", ["age"] = 22 },
                new() { ["id"] = 3, ["email"] = "user3@test.com", ["name"] = "User Three", ["age"] = 33 }
            },
            RowCount = 3
        };

        var options = new WriteOptions
        {
            UseUpsert = true,
            UpsertKeys = new List<string> { "id" }
        };

        // Act
        var result = await _writer.WriteBatchAsync(connector, mixedBatch, options, CancellationToken.None);

        // Assert
        result.RowsWritten.Should().Be(3);
        result.RowsFailed.Should().Be(0);
        
        var count = await GetRowCount();
        count.Should().Be(3);

        var updatedAge = await GetUserAge(1);
        updatedAge.Should().Be(99);
    }

    [Fact]
    public async Task WriteBatchAsync_WithUpsert_ShouldTrackPerRowErrors()
    {
        // Arrange
        var connector = CreateConnector();
        
        var batch = new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            Rows = new List<Dictionary<string, object?>>
            {
                new() { ["id"] = 1, ["email"] = "valid@test.com", ["name"] = "Valid", ["age"] = 25 },
                new() { ["id"] = 2, ["email"] = "invalid", ["name"] = "Invalid Email", ["age"] = 30 }, // Will fail unique constraint if we insert twice
                new() { ["id"] = 3, ["email"] = "another@test.com", ["name"] = "Another", ["age"] = 35 }
            },
            RowCount = 3
        };

        var options = new WriteOptions
        {
            UseUpsert = true,
            UpsertKeys = new List<string> { "id" }
        };

        // First insert should succeed
        await _writer.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Create a batch with duplicate email (violates unique constraint)
        var duplicateBatch = new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            Rows = new List<Dictionary<string, object?>>
            {
                new() { ["id"] = 4, ["email"] = "valid@test.com", ["name"] = "Duplicate Email", ["age"] = 40 } // Duplicate email
            },
            RowCount = 1
        };

        // Act
        var result = await _writer.WriteBatchAsync(connector, duplicateBatch, options, CancellationToken.None);

        // Assert
        result.RowsFailed.Should().Be(1);
        result.RowErrors.Should().HaveCount(1);
        result.RowErrors[0].RowIndex.Should().Be(0);
        result.RowErrors[0].ErrorCode.Should().Be("23505"); // PostgreSQL unique violation
    }

    [Fact]
    public async Task WriteBatchAsync_ShouldHandleLargeBatch()
    {
        // Arrange
        var connector = CreateConnector();
        var batch = CreateTestBatch(10000); // 10k rows
        var options = new WriteOptions();

        // Act
        var result = await _writer.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        result.RowsWritten.Should().Be(10000);
        result.RowsFailed.Should().Be(0);
        
        var count = await GetRowCount();
        count.Should().Be(10000);
    }

    [Fact]
    public async Task WriteBatchAsync_WithUpsert_ShouldContinueAfterRowFailure()
    {
        // Arrange
        var connector = CreateConnector();
        
        // Insert some initial data
        var initialBatch = new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            Rows = new List<Dictionary<string, object?>>
            {
                new() { ["id"] = 1, ["email"] = "existing@test.com", ["name"] = "Existing", ["age"] = 25 }
            },
            RowCount = 1
        };
        
        await _writer.WriteBatchAsync(connector, initialBatch, new WriteOptions(), CancellationToken.None);

        // Create a batch with mixed success and failure
        var mixedBatch = new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            Rows = new List<Dictionary<string, object?>>
            {
                new() { ["id"] = 10, ["email"] = "valid1@test.com", ["name"] = "Valid 1", ["age"] = 30 },  // Should succeed
                new() { ["id"] = 11, ["email"] = "existing@test.com", ["name"] = "Duplicate", ["age"] = 35 },  // Should fail (duplicate email)
                new() { ["id"] = 12, ["email"] = "valid2@test.com", ["name"] = "Valid 2", ["age"] = 40 }   // Should succeed (but will fail if transaction is aborted)
            },
            RowCount = 3
        };

        var options = new WriteOptions
        {
            UseUpsert = true,
            UpsertKeys = new List<string> { "id" }
        };

        // Act
        var result = await _writer.WriteBatchAsync(connector, mixedBatch, options, CancellationToken.None);

        // Assert
        result.RowsWritten.Should().Be(2);  // Row 0 and Row 2 should succeed
        result.RowsFailed.Should().Be(1);   // Row 1 should fail
        result.RowErrors.Should().HaveCount(1);
        result.RowErrors[0].RowIndex.Should().Be(1);
        result.RowErrors[0].ErrorCode.Should().Be("23505"); // PostgreSQL unique violation
        
        // Verify that the successful rows were actually inserted
        var count = await GetRowCount();
        count.Should().Be(3); // Initial row + 2 successful rows from mixed batch
        
        // Verify the specific rows exist
        var validRow1 = await GetUserName(10);
        validRow1.Should().Be("Valid 1");
        
        var validRow2 = await GetUserName(12);
        validRow2.Should().Be("Valid 2");
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
            Direction = "destination",
            IsSource = false,
            IsDestination = true,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                ConnectionString = _connectionString,
                TableName = "test_users"
            }),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
    }

    private ReadBatch CreateTestBatch(int rowCount, int startId = 1)
    {
        var rows = new List<Dictionary<string, object?>>();
        
        for (int i = 0; i < rowCount; i++)
        {
            var id = startId + i;
            rows.Add(new Dictionary<string, object?>
            {
                ["id"] = id,
                ["email"] = $"user{id}@test.com",
                ["name"] = $"User {id}",
                ["age"] = 20 + (i % 50)
            });
        }

        return new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            Rows = rows,
            RowCount = rowCount
        };
    }

    private async Task<int> GetRowCount()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        
        await using var command = new NpgsqlCommand("SELECT COUNT(*) FROM test_users", connection);
        var result = await command.ExecuteScalarAsync();
        
        return Convert.ToInt32(result);
    }

    private async Task<string?> GetUserName(int id)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        
        await using var command = new NpgsqlCommand("SELECT name FROM test_users WHERE id = @id", connection);
        command.Parameters.AddWithValue("@id", id);
        
        return await command.ExecuteScalarAsync() as string;
    }

    private async Task<int?> GetUserAge(int id)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        
        await using var command = new NpgsqlCommand("SELECT age FROM test_users WHERE id = @id", connection);
        command.Parameters.AddWithValue("@id", id);
        
        var result = await command.ExecuteScalarAsync();
        return result as int?;
    }
}
