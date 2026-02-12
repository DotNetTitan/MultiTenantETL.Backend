# Data Connectors Implementation Guide

## Supported Connector Types (24 Total)

### Databases (9)
- SQL Server, PostgreSQL, MySQL, Oracle
- Snowflake, Google BigQuery, AWS Redshift
- MongoDB, Azure Cosmos DB

### File Storage (9)
- **Formats:** CSV, JSON, JSONL
- **Storage Providers:** Local, FTP, SFTP, Azure Blob, AWS S3, Google Cloud Storage

### APIs (1)
- REST API with authentication (Bearer, Basic, API Key)
- Header support, pagination, custom configurations

### Cloud Storage (5)
- Azure Blob Storage, AWS S3, Google Cloud Storage, FTP, SFTP

## Adding a New Data Reader

### Step 1: Create Reader Class
**Location:** `Infrastructure/DataReaders/{ConnectorType}DataReader.cs`

```csharp
public class NewTypeDataReader : IDataReader
{
    private readonly ILogger<NewTypeDataReader> _logger;

    public NewTypeDataReader(ILogger<NewTypeDataReader> logger)
    {
        _logger = logger;
    }

    public async IAsyncEnumerable<ReadBatch> ReadAsync(
        ConnectorConfig sourceConfig,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // 1. Parse configuration
        var connectionString = sourceConfig.ConnectionString;
        var config = sourceConfig.GetConfigValue<NewTypeConfig>("Config");

        // 2. Open connection
        await using var connection = await CreateConnectionAsync(connectionString);

        // 3. Stream data in batches
        var batchNumber = 0;
        var batchSize = sourceConfig.BatchSize ?? 1000;

        while (await HasMoreDataAsync(connection))
        {
            var records = await ReadBatchAsync(connection, batchSize, cancellationToken);

            yield return new ReadBatch
            {
                FieldNames = records.First().Keys.ToList(),
                Records = records,
                BatchNumber = ++batchNumber
            };
        }
    }
}
```

### Step 2: Register in Factory
**Location:** `Infrastructure/DataReaders/DataReaderFactory.cs`

```csharp
public IDataReader CreateReader(string connectorType)
{
    return connectorType switch
    {
        ConnectorTypes.NewType => _serviceProvider.GetRequiredService<NewTypeDataReader>(),
        // ... other readers
        _ => throw new NotSupportedException($"Connector type '{connectorType}' not supported")
    };
}
```

### Step 3: Add Connection Tester
**Location:** `Infrastructure/Services/ConnectionTesting/NewTypeConnectionTester.cs`

```csharp
public class NewTypeConnectionTester
{
    public async Task<ConnectionTestResult> TestConnectionAsync(
        string connectionString,
        Dictionary<string, object>? config)
    {
        try
        {
            await using var connection = await CreateConnectionAsync(connectionString);
            await connection.TestAsync();

            return ConnectionTestResult.Success("Connection successful");
        }
        catch (Exception ex)
        {
            return ConnectionTestResult.Failure($"Connection failed: {ex.Message}");
        }
    }
}
```

### Step 4: Add Schema Detector (Optional)
**Location:** `Infrastructure/Services/SchemaDetection/NewTypeSchemaDetector.cs`

```csharp
public async Task<List<FieldSchema>> DetectSchemaAsync(ConnectorConfig config)
{
    var fields = new List<FieldSchema>();

    // Detect fields from source
    await using var reader = _dataReaderFactory.CreateReader(config.Type);
    await foreach (var batch in reader.ReadAsync(config).Take(1))
    {
        foreach (var fieldName in batch.FieldNames)
        {
            fields.Add(new FieldSchema
            {
                Name = fieldName,
                DataType = InferDataType(batch.Records.First()[fieldName]),
                IsNullable = true
            });
        }
    }

    return fields;
}
```

## Adding a New Data Writer

### Step 1: Create Writer Class
**Location:** `Infrastructure/DataWriters/{ConnectorType}DataWriter.cs`

```csharp
public class NewTypeDataWriter : IDataWriter
{
    private Connection? _connection;
    private List<string>? _fieldNames;

    public async Task InitializeAsync(
        ConnectorConfig destinationConfig,
        List<string> fieldNames)
    {
        _fieldNames = fieldNames;

        var connectionString = destinationConfig.ConnectionString;
        _connection = await CreateConnectionAsync(connectionString);

        // Prepare destination (create table, validate, etc.)
        await PrepareDestinationAsync(_connection, fieldNames);
    }

    public async Task WriteBatchAsync(
        WriteBatch batch,
        CancellationToken cancellationToken = default)
    {
        if (_connection == null)
            throw new InvalidOperationException("Writer not initialized");

        // Bulk insert/write batch
        await BulkInsertAsync(_connection, batch.Records, cancellationToken);
    }

    public async Task CompleteAsync()
    {
        // Finalize writes (commit, close files, etc.)
        if (_connection != null)
        {
            await _connection.CommitAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
    }
}
```

### Step 2: Register in Factory
**Location:** `Infrastructure/DataWriters/DataWriterFactory.cs`

```csharp
public IDataWriter CreateWriter(string connectorType)
{
    return connectorType switch
    {
        ConnectorTypes.NewType => _serviceProvider.GetRequiredService<NewTypeDataWriter>(),
        // ... other writers
        _ => throw new NotSupportedException($"Connector type '{connectorType}' not supported")
    };
}
```

### Step 3: Register in DI
**Location:** `Infrastructure/DependencyInjection.cs`

```csharp
services.AddTransient<NewTypeDataReader>();
services.AddTransient<NewTypeDataWriter>();
services.AddTransient<NewTypeConnectionTester>();
services.AddTransient<NewTypeSchemaDetector>();
```

## Data Reader Pattern

**Key Concepts:**
- Return `IAsyncEnumerable<ReadBatch>` for memory-efficient streaming
- Default batch size: 1,000 rows (configurable in `EtlSettings`)
- Handle connection pooling for databases
- Properly dispose resources with `await using`

**Batch Structure:**
```csharp
public class ReadBatch
{
    public List<string> FieldNames { get; set; }         // Column names
    public List<Dictionary<string, object?>> Records { get; set; }  // Row data
    public int BatchNumber { get; set; }                 // Batch sequence number
}
```

## Data Writer Pattern

**Key Concepts:**
- Implement `IAsyncDisposable` for proper cleanup
- Initialize in `InitializeAsync` (create tables, open connections)
- Buffer writes for performance when possible
- Use bulk operations (e.g., `SqlBulkCopy`) for databases
- Handle transactions in `CompleteAsync`

**Writer Lifecycle:**
```csharp
await using var writer = _factory.CreateWriter(type);
await writer.InitializeAsync(config, fieldNames);  // Setup
await writer.WriteBatchAsync(batch1);              // Write batches
await writer.WriteBatchAsync(batch2);
await writer.CompleteAsync();                      // Finalize
// Dispose called automatically
```

## Adding Connector Type Constant

**Location:** `Domain/Constants/ConnectorTypes.cs`

```csharp
public static class ConnectorTypes
{
    public const string NewType = "NewType";

    // Existing types
    public const string SqlServer = "SqlServer";
    public const string PostgreSQL = "PostgreSQL";
    // ... etc
}
```

## Connector Configuration Examples

### Database Connector
```json
{
  "type": "PostgreSQL",
  "connectionString": "Host=localhost;Database=mydb;Username=user;Password=pass",
  "config": {
    "tableName": "customers",
    "query": "SELECT * FROM customers WHERE active = true",
    "commandTimeout": 300
  }
}
```

### File Connector
```json
{
  "type": "CSV",
  "connectionString": "local",
  "config": {
    "filePath": "/data/customers.csv",
    "delimiter": ",",
    "hasHeader": true,
    "encoding": "UTF-8"
  },
  "storageConfig": {
    "provider": "S3",
    "bucketName": "my-bucket",
    "accessKey": "...",
    "secretKey": "..."
  }
}
```

### REST API Connector
```json
{
  "type": "RestAPI",
  "connectionString": "https://api.example.com",
  "config": {
    "endpoint": "/customers",
    "method": "GET",
    "authentication": {
      "type": "Bearer",
      "token": "..."
    },
    "headers": {
      "Accept": "application/json"
    },
    "pagination": {
      "type": "Offset",
      "pageSize": 100
    }
  }
}
```

## Performance Considerations

**For Database Readers:**
- Use connection pooling (built into ADO.NET providers)
- Set appropriate command timeout (default: 300 seconds)
- Use `CommandBehavior.SequentialAccess` for large BLOBs
- Avoid `SELECT *` - enumerate specific columns

**For Database Writers:**
- Use bulk insert operations when available
- Batch size optimization: 1,000-10,000 rows typical
- Consider transaction size vs. memory usage
- Disable indexes during large inserts (rebuild after)

**For File Operations:**
- Stream data, don't load entire files into memory
- Use buffered streams (default buffer: 80KB)
- Parallel uploads for cloud storage when possible
- Compress large files before transfer

**For API Connectors:**
- Respect rate limits (implement backoff/retry)
- Reuse HTTP client instances
- Enable connection pooling
- Handle pagination efficiently
