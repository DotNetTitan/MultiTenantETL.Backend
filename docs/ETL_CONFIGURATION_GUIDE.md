# ETL Configuration Guide

**Last Updated:** November 28, 2025

---

## Overview

ETL operations are now configurable through `appsettings.json` instead of hardcoded values. This allows you to tune performance based on your hardware and data characteristics.

---

## Configuration Section

Add this to your `appsettings.json`:

```json
{
  "Etl": {
    "DefaultBatchSize": 5000,
    "MySqlBatchSize": 1000,
    "FileBatchSize": 5000,
    "DefaultTimeoutSeconds": 300,
    "MaxConcurrentBatches": 1,
    "FileStreamBufferSize": 8192,
    "EnableDetailedLogging": false,
    "MaxRowsPerExecution": 0
  }
}
```

---

## Configuration Properties

### DefaultBatchSize
- **Type:** Integer
- **Default:** 5000
- **Description:** Batch size for SQL Server and PostgreSQL operations
- **Recommendations:**
  - **Small rows** (<10 columns): 10,000
  - **Medium rows** (10-50 columns): 5,000
  - **Large rows** (>50 columns): 1,000-2,000
  - **Wide rows** (>100 columns): 500-1,000

### MySqlBatchSize
- **Type:** Integer
- **Default:** 1,000
- **Description:** Batch size for MySQL operations (lower due to parameter limits)
- **Recommendations:**
  - **Small rows**: 2,000
  - **Medium rows**: 1,000
  - **Large rows**: 500
  - **Note:** MySQL has a max_allowed_packet limit

### FileBatchSize
- **Type:** Integer
- **Default:** 5,000
- **Description:** Batch size for file operations (CSV, JSON, NDJSON)
- **Recommendations:**
  - **Fast SSD**: 10,000
  - **Standard SSD**: 5,000
  - **HDD**: 2,000

### DefaultTimeoutSeconds
- **Type:** Integer
- **Default:** 300 (5 minutes)
- **Description:** Timeout for database operations
- **Recommendations:**
  - **Small datasets** (<1M rows): 60 seconds
  - **Medium datasets** (1-10M rows): 300 seconds
  - **Large datasets** (>10M rows): 600-1800 seconds

### MaxConcurrentBatches
- **Type:** Integer
- **Default:** 1
- **Description:** Maximum number of batches to process concurrently
- **Recommendations:**
  - **Single-core**: 1
  - **Quad-core**: 2-4
  - **8+ cores**: 4-8
  - **Note:** Currently not implemented (Phase 4 feature)

### FileStreamBufferSize
- **Type:** Integer
- **Default:** 8192 (8 KB)
- **Description:** Buffer size for file streaming operations
- **Recommendations:**
  - **Small files** (<100MB): 8192
  - **Large files** (>1GB): 65536 (64 KB)
  - **Very large files** (>10GB): 131072 (128 KB)

### EnableDetailedLogging
- **Type:** Boolean
- **Default:** false
- **Description:** Enable detailed logging for ETL operations
- **Recommendations:**
  - **Development**: true
  - **Production**: false (performance impact)
  - **Troubleshooting**: true

### MaxRowsPerExecution
- **Type:** Integer
- **Default:** 0 (unlimited)
- **Description:** Maximum rows to process in a single execution
- **Recommendations:**
  - **Testing**: 1,000-10,000
  - **Production**: 0 (unlimited)
  - **Memory-constrained**: Set based on available RAM

---

## Environment-Specific Configuration

### Development (appsettings.Development.json)
```json
{
  "Etl": {
    "DefaultBatchSize": 1000,
    "MySqlBatchSize": 500,
    "FileBatchSize": 1000,
    "DefaultTimeoutSeconds": 60,
    "EnableDetailedLogging": true,
    "MaxRowsPerExecution": 10000
  }
}
```

### Production (appsettings.json)
```json
{
  "Etl": {
    "DefaultBatchSize": 10000,
    "MySqlBatchSize": 2000,
    "FileBatchSize": 10000,
    "DefaultTimeoutSeconds": 600,
    "FileStreamBufferSize": 65536,
    "EnableDetailedLogging": false,
    "MaxRowsPerExecution": 0
  }
}
```

### High-Performance (appsettings.Production.json)
```json
{
  "Etl": {
    "DefaultBatchSize": 20000,
    "MySqlBatchSize": 5000,
    "FileBatchSize": 20000,
    "DefaultTimeoutSeconds": 1800,
    "MaxConcurrentBatches": 4,
    "FileStreamBufferSize": 131072,
    "EnableDetailedLogging": false,
    "MaxRowsPerExecution": 0
  }
}
```

---

## Per-Connector Configuration Override

You can override batch sizes per connector in the connector's ConfigJson:

### SQL Server Example
```json
{
  "connectionString": "Server=localhost;Database=mydb;...",
  "query": "SELECT * FROM large_table",
  "batchSize": 10000,
  "timeoutSeconds": 600
}
```

### MySQL Example
```json
{
  "connectionString": "Server=localhost;Database=mydb;...",
  "tableName": "destination_table",
  "batchSize": 2000,
  "timeoutSeconds": 300
}
```

**Note:** If `batchSize` or `timeoutSeconds` is set to 0 or omitted, the global settings from `appsettings.json` will be used.

---

## Performance Tuning Guide

### Scenario 1: Processing 10M Rows (Medium Width)

**Hardware:** 8-core CPU, 32GB RAM, SSD

**Recommended Settings:**
```json
{
  "Etl": {
    "DefaultBatchSize": 10000,
    "MySqlBatchSize": 2000,
    "FileBatchSize": 10000,
    "DefaultTimeoutSeconds": 600,
    "FileStreamBufferSize": 65536
  }
}
```

**Expected Performance:**
- SQL Server → PostgreSQL: 2-5 minutes
- CSV → MySQL: 5-10 minutes

---

### Scenario 2: Processing 100M Rows (Narrow Width)

**Hardware:** 16-core CPU, 64GB RAM, NVMe SSD

**Recommended Settings:**
```json
{
  "Etl": {
    "DefaultBatchSize": 20000,
    "MySqlBatchSize": 5000,
    "FileBatchSize": 20000,
    "DefaultTimeoutSeconds": 1800,
    "FileStreamBufferSize": 131072
  }
}
```

**Expected Performance:**
- SQL Server → PostgreSQL: 10-30 minutes
- CSV → MySQL: 30-60 minutes

---

### Scenario 3: Processing Wide Tables (>100 Columns)

**Hardware:** 8-core CPU, 32GB RAM, SSD

**Recommended Settings:**
```json
{
  "Etl": {
    "DefaultBatchSize": 1000,
    "MySqlBatchSize": 500,
    "FileBatchSize": 1000,
    "DefaultTimeoutSeconds": 600,
    "FileStreamBufferSize": 65536
  }
}
```

**Rationale:** Wide rows consume more memory per batch, so reduce batch size.

---

## Monitoring & Optimization

### Signs You Need to Adjust Batch Size

**Batch Size Too Large:**
- Out of memory errors
- High memory usage (>80%)
- Slow performance due to swapping
- Database timeouts

**Batch Size Too Small:**
- Low throughput (<10K rows/sec)
- High CPU usage with low memory usage
- Excessive network round trips

### Optimal Batch Size Formula

```
Optimal Batch Size = Available Memory (MB) / (Row Size (KB) × 2)
```

**Example:**
- Available Memory: 4GB (4096 MB)
- Row Size: 2 KB
- Optimal Batch Size: 4096 / (2 × 2) = 1024 rows

---

## Implementation Status

### ✅ Implemented (Phase 2)
- SQL Server Reader
- SQL Server Writer
- PostgreSQL Reader
- Configuration class (`EtlSettings`)
- Configuration registration in DI

### ⏳ Pending Updates
- PostgreSQL Writer
- MySQL Reader
- MySQL Writer
- CSV Reader
- CSV Writer
- JSON Reader
- JSON Writer
- NDJSON Reader
- NDJSON Writer

**Note:** All readers/writers will be updated to use `EtlSettings` in the next iteration.

---

## Migration Guide

### Before (Hardcoded)
```csharp
private const int DefaultBatchSize = 5000;
var batch = new List<Dictionary<string, object?>>(5000);
```

### After (Configurable)
```csharp
private readonly EtlSettings _settings;

public SqlServerDataReader(IOptions<EtlSettings> settings)
{
    _settings = settings.Value;
}

var effectiveBatchSize = batchSize > 0 ? batchSize : _settings.DefaultBatchSize;
var batch = new List<Dictionary<string, object?>>(effectiveBatchSize);
```

---

## Best Practices

1. **Start with defaults** - Test with default settings first
2. **Monitor performance** - Track throughput and memory usage
3. **Tune incrementally** - Adjust one setting at a time
4. **Test with production data** - Use realistic dataset sizes
5. **Document changes** - Keep track of what works for your use case
6. **Use environment-specific configs** - Different settings for dev/prod

---

**Document Version:** 1.0  
**Status:** Phase 2 - Partial Implementation
