# Testing Guidelines

## Testing Approach

**Frameworks:**
- **xUnit** - Primary testing framework
- **NSubstitute** - Mocking and stubbing
- **FluentAssertions** - Readable assertions
- **AutoFixture** - Test data generation

## Unit Tests

**Location:** `tests/MultiTenantETL.UnitTests/`

### Structure
```
MultiTenantETL.UnitTests/
├── Services/              # Service logic tests
├── Validators/            # FluentValidation rule tests
├── Security/              # Encryption, authorization tests
├── Transformations/       # Field transformation tests
└── Helpers/              # Test utilities
```

### Test Pattern (AAA)

```csharp
public class ConnectorServiceTests
{
    private readonly IConnectorService _service;
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly Fixture _fixture = new();

    public ConnectorServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options, _tenantProvider);

        // Mock current user service
        _currentUserService = Substitute.For<ICurrentUserService>();
        _currentUserService.CurrentTenantId.Returns(Guid.NewGuid());

        _service = new ConnectorService(_context, _currentUserService);
    }

    [Fact]
    public async Task CreateAsync_WithValidRequest_ReturnsConnectorResponse()
    {
        // Arrange
        var request = new CreateConnectorRequest
        {
            Name = "Test Connector",
            Type = ConnectorTypes.PostgreSQL,
            ConnectionString = "Host=localhost;..."
        };

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.Name.Should().Be(request.Name);
        result.Type.Should().Be(request.Type);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task CreateAsync_WithInvalidName_ThrowsValidationException(
        string invalidName)
    {
        // Arrange
        var request = new CreateConnectorRequest { Name = invalidName };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _service.CreateAsync(request));
    }

    [Fact]
    public async Task GetByIdAsync_WithDifferentTenant_ThrowsNotFoundException()
    {
        // Arrange - Create connector for different tenant
        var otherTenantId = Guid.NewGuid();
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = otherTenantId,
            Name = "Other Tenant Connector"
        };
        _context.Connectors.Add(connector);
        await _context.SaveChangesAsync();

        // Act & Assert - Current user should not see it
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.GetByIdAsync(connector.Id));
    }
}
```

### Using AutoFixture

```csharp
public class PipelineServiceTests
{
    private readonly Fixture _fixture = new();

    [Fact]
    public async Task CreateAsync_WithGeneratedData_Succeeds()
    {
        // Arrange - AutoFixture generates realistic test data
        var request = _fixture.Build<CreatePipelineRequest>()
            .With(x => x.Name, "Test Pipeline")
            .With(x => x.SourceConnectorId, Guid.NewGuid())
            .With(x => x.DestinationConnectorId, Guid.NewGuid())
            .Create();

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        result.Should().NotBeNull();
    }
}
```

### Testing Async Streams

```csharp
[Fact]
public async Task ReadAsync_ReturnsMultipleBatches()
{
    // Arrange
    var reader = new CsvDataReader(_logger);
    var config = new ConnectorConfig
    {
        Type = ConnectorTypes.CSV,
        Config = new { FilePath = "test-data.csv" }
    };

    // Act - Collect all batches from async stream
    var batches = new List<ReadBatch>();
    await foreach (var batch in reader.ReadAsync(config))
    {
        batches.Add(batch);
    }

    // Assert
    batches.Should().HaveCountGreaterThan(0);
    batches.First().Records.Should().NotBeEmpty();
}
```

## Integration Tests

**Location:** `tests/MultiTenantETL.IntegrationTests/`

### Structure
```
MultiTenantETL.IntegrationTests/
├── API/                   # Controller endpoint tests
├── DataConnectors/        # Reader/Writer tests
├── Orchestration/         # End-to-end pipeline tests
├── Fixtures/             # Shared test infrastructure
│   ├── WebApplicationFactory.cs
│   └── DatabaseFixture.cs
└── TestData/             # Sample files and data
```

### API Integration Tests

```csharp
public class ConnectorsControllerTests : IClassFixture<WebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly string _tenantId;
    private readonly string _accessToken;

    public ConnectorsControllerTests(WebApplicationFactory factory)
    {
        _client = factory.CreateClient();

        // Setup: Create tenant and authenticate
        _tenantId = await CreateTestTenantAsync();
        _accessToken = await AuthenticateAsync("admin@test.com", "Password123!");

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _accessToken);
    }

    [Fact]
    public async Task CreateConnector_WithValidData_Returns201()
    {
        // Arrange
        var request = new CreateConnectorRequest
        {
            Name = "Test Connector",
            Type = ConnectorTypes.PostgreSQL,
            ConnectionString = "Host=localhost;Database=test;..."
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/connectors", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var result = await response.Content.ReadFromJsonAsync<ConnectorResponse>();
        result.Should().NotBeNull();
        result!.Name.Should().Be(request.Name);
    }

    [Fact]
    public async Task GetConnector_FromDifferentTenant_Returns404()
    {
        // Arrange - Create connector for another tenant
        var otherTenantId = await CreateTestTenantAsync();
        var connectorId = await CreateConnectorAsync(otherTenantId, "Other Connector");

        // Act - Try to access with current tenant's token
        var response = await _client.GetAsync($"/api/connectors/{connectorId}");

        // Assert - Should not find it due to tenant isolation
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

### Data Connector Integration Tests

```csharp
public class SqlServerDataReaderTests
{
    private readonly string _connectionString;

    public SqlServerDataReaderTests()
    {
        // Use TestContainers or local test database
        _connectionString = "Server=localhost;Database=TestDb;...";
    }

    [Fact]
    public async Task ReadAsync_FromSqlServer_ReturnsExpectedData()
    {
        // Arrange - Setup test data in SQL Server
        await SeedTestDataAsync(_connectionString, 5000); // 5000 rows

        var reader = new SqlServerDataReader(_logger);
        var config = new ConnectorConfig
        {
            Type = ConnectorTypes.SqlServer,
            ConnectionString = _connectionString,
            Config = new { TableName = "Customers" }
        };

        // Act - Read all batches
        var totalRecords = 0;
        await foreach (var batch in reader.ReadAsync(config))
        {
            totalRecords += batch.Records.Count;
            batch.FieldNames.Should().Contain("Id");
            batch.FieldNames.Should().Contain("Name");
        }

        // Assert
        totalRecords.Should().Be(5000);
    }

    [Fact]
    public async Task WriteAsync_ToSqlServer_PersistsData()
    {
        // Arrange
        var writer = new SqlServerDataWriter(_logger);
        var config = new ConnectorConfig
        {
            Type = ConnectorTypes.SqlServer,
            ConnectionString = _connectionString,
            Config = new { TableName = "TestOutput" }
        };

        var fieldNames = new List<string> { "Id", "Name", "Email" };
        await writer.InitializeAsync(config, fieldNames);

        var batch = new WriteBatch
        {
            FieldNames = fieldNames,
            Records = new List<Dictionary<string, object?>>
            {
                new() { ["Id"] = 1, ["Name"] = "John", ["Email"] = "john@test.com" },
                new() { ["Id"] = 2, ["Name"] = "Jane", ["Email"] = "jane@test.com" }
            }
        };

        // Act
        await writer.WriteBatchAsync(batch);
        await writer.CompleteAsync();

        // Assert - Verify data was written
        var count = await GetRecordCountAsync(_connectionString, "TestOutput");
        count.Should().Be(2);
    }
}
```

### End-to-End Pipeline Tests

```csharp
public class PipelineOrchestrationTests
{
    [Fact]
    public async Task ExecutePipeline_CsvToPostgreSQL_CompletesSuccessfully()
    {
        // Arrange - Create source CSV and destination database
        var sourcePath = await CreateTestCsvFileAsync(1000); // 1000 rows
        var destConnectionString = "Host=localhost;Database=test_dest;...";

        var orchestrator = new PipelineOrchestrator(
            _context,
            _readerFactory,
            _writerFactory,
            _mappingService,
            _logger);

        var pipeline = new Pipeline
        {
            Name = "CSV to PostgreSQL Test",
            SourceConnectorId = sourceConnector.Id,
            DestinationConnectorId = destConnector.Id,
            FieldMappingsJson = JsonSerializer.Serialize(mappings)
        };

        var execution = new PipelineExecution
        {
            Id = Guid.NewGuid(),
            PipelineId = pipeline.Id,
            Status = ExecutionStatus.Queued
        };

        // Act
        await orchestrator.ExecutePipelineAsync(
            pipeline.Id,
            execution.Id,
            CancellationToken.None);

        // Assert
        execution.Status.Should().Be(ExecutionStatus.Completed);
        execution.RecordsProcessed.Should().Be(1000);
        execution.RecordsFailed.Should().Be(0);

        // Verify destination data
        var destCount = await GetRecordCountAsync(destConnectionString, "target_table");
        destCount.Should().Be(1000);
    }
}
```

## Test Data Management

### Test Fixtures

**Location:** `tests/MultiTenantETL.IntegrationTests/Fixtures/`

```csharp
public class DatabaseFixture : IDisposable
{
    public ApplicationDbContext Context { get; }
    public Guid TestTenantId { get; }
    public Guid TestUserId { get; }

    public DatabaseFixture()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        Context = new ApplicationDbContext(options, _tenantProvider);

        // Seed test data
        TestTenantId = Guid.NewGuid();
        TestUserId = Guid.NewGuid();

        SeedTestData();
    }

    private void SeedTestData()
    {
        var tenant = new Tenant { Id = TestTenantId, Name = "Test Tenant" };
        var user = new ApplicationUser { Id = TestUserId, Email = "test@test.com" };

        Context.Tenants.Add(tenant);
        Context.Users.Add(user);
        Context.SaveChanges();
    }

    public void Dispose()
    {
        Context.Dispose();
    }
}
```

### Sample Data Generation

```csharp
public static class TestDataGenerator
{
    public static List<Dictionary<string, object?>> GenerateCustomers(int count)
    {
        var faker = new Faker();
        var customers = new List<Dictionary<string, object?>>();

        for (int i = 0; i < count; i++)
        {
            customers.Add(new Dictionary<string, object?>
            {
                ["Id"] = i + 1,
                ["Name"] = faker.Name.FullName(),
                ["Email"] = faker.Internet.Email(),
                ["Phone"] = faker.Phone.PhoneNumber(),
                ["Country"] = faker.Address.Country()
            });
        }

        return customers;
    }

    public static async Task<string> CreateTestCsvFileAsync(int rowCount)
    {
        var path = Path.GetTempFileName() + ".csv";
        var data = GenerateCustomers(rowCount);

        await using var writer = new StreamWriter(path);
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        await csv.WriteRecordsAsync(data);

        return path;
    }
}
```

## Test Configuration

### appsettings.Test.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=multitenant_etl_test;Username=test;Password=test"
  },
  "Messaging": {
    "Provider": "RabbitMQ"
  },
  "RabbitMq": {
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft": "Warning"
    }
  }
}
```

## Running Tests

```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --verbosity detailed

# Run specific test project
dotnet test tests/MultiTenantETL.UnitTests

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run tests in parallel (faster)
dotnet test --parallel

# Run tests with filter
dotnet test --filter "FullyQualifiedName~ConnectorService"
dotnet test --filter "Category=Integration"
```

## CI/CD Testing

**GitHub Actions:** `.github/workflows/ci-cd.yml`

```yaml
- name: Run Unit Tests
  run: dotnet test tests/MultiTenantETL.UnitTests --no-build --verbosity normal

- name: Run Integration Tests
  run: dotnet test tests/MultiTenantETL.IntegrationTests --no-build --verbosity normal
  env:
    ConnectionStrings__DefaultConnection: "Host=localhost;Database=test;..."
```

## Code Coverage

**Target:** Maintain >80% code coverage

**Measure Coverage:**
```bash
dotnet test --collect:"XPlat Code Coverage"
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coverage-report
```

## Best Practices

1. **AAA Pattern** - Arrange, Act, Assert structure
2. **One assertion per test** - Keep tests focused
3. **Descriptive names** - Test method names should explain scenario
4. **Avoid test interdependence** - Tests should run independently
5. **Use fixtures for shared setup** - Reduce duplication
6. **Mock external dependencies** - Database, file system, APIs
7. **Test edge cases** - Null inputs, empty collections, boundaries
8. **Clean up test data** - Dispose resources, delete temp files
9. **Fast unit tests** - Unit tests should run in milliseconds
10. **Realistic integration tests** - Use actual databases/services when possible
