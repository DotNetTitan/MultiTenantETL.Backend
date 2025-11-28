# MultiTenantETL Integration Tests

## Overview

This project contains integration tests for Phase 2 data readers and writers using Testcontainers to spin up real database instances.

## Prerequisites

- **.NET 8 SDK**
- **Docker Desktop** running (required for Testcontainers)
- At least **4GB RAM** available for Docker containers

## Running the Tests

### Run All Tests

```bash
dotnet test
```

### Run Specific Test Class

```bash
dotnet test --filter "FullyQualifiedName~PostgreSqlDataWriterTests"
dotnet test --filter "FullyQualifiedName~PostgreSqlDataReaderTests"
```

### Run Specific Test

```bash
dotnet test --filter "FullyQualifiedName~PostgreSqlDataWriterTests.WriteBatchAsync_WithUpsert_ShouldUpdateExistingRows"
```

### Run with Detailed Output

```bash
dotnet test --logger "console;verbosity=detailed"
```

## Test Coverage

### Data Writers

**PostgreSqlDataWriterTests** - Tests for PostgreSQL writer
- ✅ Basic insert operations
- ✅ Truncate before load
- ✅ Upsert with single key
- ✅ Upsert with updates
- ✅ Mixed insert and update
- ✅ Per-row error tracking
- ✅ Large batch handling (10k rows)

### Data Readers

**PostgreSqlDataReaderTests** - Tests for PostgreSQL reader
- ✅ Streaming data in batches
- ✅ Respecting MaxRows limit
- ✅ Custom query support
- ✅ Memory-bounded streaming
- ✅ Connection testing
- ✅ Schema detection

## Test Infrastructure

### Testcontainers

Tests use [Testcontainers](https://dotnet.testcontainers.org/) to spin up real database instances:

- **PostgreSQL 16 Alpine** - Lightweight PostgreSQL container
- **SQL Server 2022** - Microsoft SQL Server container
- **MySQL 8** - MySQL container

Containers are automatically:
- Started before each test class
- Stopped and cleaned up after tests complete
- Isolated per test class

### Test Data

Tests create their own tables and populate them with test data:
- **test_users** - For writer tests (id, email, name, age)
- **test_products** - For reader tests (id, name, price, category, in_stock)

## Performance Tests

### Large Batch Test

The `WriteBatchAsync_ShouldHandleLargeBatch` test verifies:
- Writing 10,000 rows in a single batch
- Memory stays bounded
- No performance degradation

### Streaming Test

The `ReadAsync_ShouldNotLoadAllDataIntoMemory` test verifies:
- Data is streamed, not loaded entirely into memory
- Only requested batches are processed
- Early termination doesn't load remaining data

## Troubleshooting

### Docker Not Running

```
Error: Docker is not running
```

**Solution:** Start Docker Desktop

### Port Already in Use

```
Error: Port 5432 is already allocated
```

**Solution:** Stop any local PostgreSQL/MySQL/SQL Server instances or change the test container ports

### Tests Timeout

```
Error: Test execution timed out
```

**Solution:** 
- Increase Docker memory allocation (Settings → Resources)
- Check Docker performance
- Run tests individually instead of all at once

### Container Pull Fails

```
Error: Unable to pull image
```

**Solution:**
- Check internet connection
- Verify Docker Hub access
- Try pulling image manually: `docker pull postgres:16-alpine`

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Integration Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '8.0.x'
    
    - name: Run Integration Tests
      run: dotnet test tests/MultiTenantETL.IntegrationTests
```

## Future Tests

### Completed Test Coverage

- ✅ PostgreSQL writer and reader tests (15 tests)
- ✅ CSV file reader/writer tests (19 tests)
- ✅ JSONL file writer tests (9 tests)

### Planned Test Coverage

- [ ] SQL Server writer and reader tests
- [ ] MySQL writer and reader tests
- [ ] JSON array reader/writer tests
- [ ] S3 reader/writer tests (using LocalStack)
- [ ] Azure Blob reader/writer tests (using Azurite)
- [ ] REST API reader/writer tests
- [ ] Concurrent write tests
- [ ] Transaction rollback tests
- [ ] Connection pool exhaustion tests

## Notes

- Tests are designed to be **idempotent** - can be run multiple times
- Each test class uses its own container instance
- Tests clean up after themselves
- No manual database setup required
- Tests run in parallel where possible

## Dependencies

- **xUnit** - Test framework
- **FluentAssertions** - Assertion library
- **Testcontainers.PostgreSql** - PostgreSQL container
- **Testcontainers.MsSql** - SQL Server container
- **Testcontainers.MySql** - MySQL container
- **Npgsql** - PostgreSQL .NET driver
- **Microsoft.Data.SqlClient** - SQL Server .NET driver
- **MySqlConnector** - MySQL .NET driver
