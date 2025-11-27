# Pipeline Execution Engine - Implementation Plan

## Overview

This document outlines the complete implementation plan for the missing Pipeline Execution Engine and related features in the MultiTenant ETL platform. The execution engine is the core component that actually runs ETL pipelines, processes data through transformations, and manages execution lifecycle.

## Current State Analysis

### ✅ What's Implemented
- Authentication & Authorization (OAuth 2.0 + PKCE)
- User Management (CRUD, roles, permissions)
- Tenant Management (multi-tenancy, tenant switching)
- Metadata Management (centralized configuration)
- Connectors (CRUD, configuration storage)
- Pipelines (CRUD, field mapping configuration)
- Transformations (CRUD, configuration storage)
- Audit Logging (tracking user actions)

### ❌ What's Missing
- **Pipeline Execution Engine** - No actual ETL processing
- **Transformation Processors** - No data transformation logic
- **Connector Data Operations** - Can't read/write data
- **Job Scheduling** - No automated pipeline execution
- **Dashboard Statistics** - No aggregated metrics
- **Schema Detection** - Interface exists but not implemented
- **Connection Testing** - Interface exists but not implemented

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                     Execution Orchestrator                   │
│  (Coordinates the entire ETL pipeline execution flow)       │
└─────────────────────────────────────────────────────────────┘
                              │
                ┌─────────────┴─────────────┐
                │                           │
        ┌───────▼────────┐         ┌────────▼────────┐
        │  Data Readers  │         │  Data Writers   │
        │  (Extract)     │         │  (Load)         │
        └───────┬────────┘         └────────▲────────┘
                │                           │
                │      ┌────────────────────┘
                │      │
        ┌───────▼──────▼────────┐
        │ Transformation Engine │
        │    (Transform)        │
        └───────────────────────┘
                │
        ┌───────▼────────┐
        │ Execution Log  │
        │   & Metrics    │
        └────────────────┘
```

---

## Phase 1: Core Execution Infrastructure (Week 1-2)

### 1.1 PipelineExecution Entity

**Location:** `MultiTenantETL.Domain/Entities/PipelineExecution.cs`

**Purpose:** Track individual pipeline execution instances with status, logs, and metrics.

**Properties:**
```csharp
- Id (Guid) - Primary key
- PipelineId (Guid) - Foreign key to Pipeline
- TenantId (Guid) - Foreign key to Tenant
- Status (string) - Queued, Running, Completed, Failed, Cancelled
- StartTime (DateTime) - When execution started
- EndTime (DateTime?) - When execution finished
- Duration (TimeSpan?) - Calculated duration
- RecordsProcessed (int) - Total records processed
- RecordsSucceeded (int) - Successfully processed records
- RecordsFailed (int) - Failed records
- ProgressPercent (decimal) - Current progress (0-100)
- ErrorMessage (string?) - Error details if failed
- LogsJson (string) - JSON array of log entries
- MetadataJson (string?) - Additional execution metadata
- TriggeredBy (string) - Manual, Scheduled, API
- TriggeredByUserId (Guid?) - User who triggered (if manual)
- CreatedAt (DateTime) - Audit timestamp
```

**Navigation Properties:**
```csharp
- Pipeline (Pipeline)
- Tenant (Tenant)
```

### 1.2 ExecutionLog Value Object

**Location:** `MultiTenantETL.Domain/ValueObjects/ExecutionLog.cs`

**Purpose:** Structured log entry for execution events.

**Properties:**
```csharp
- Timestamp (DateTime)
- Level (string) - Info, Warning, Error
- Message (string)
- Details (string?)
```

### 1.3 Execution DTOs

**Location:** `MultiTenantETL.Application/Executions/Models/`

**Files to Create:**
- `ExecutionResponse.cs` - Full execution details
- `ExecutionListResponse.cs` - Simplified list view
- `PagedExecutionResponse.cs` - Paginated results
- `ExecutionSearchRequest.cs` - Filter/search parameters
- `ExecutionLogDto.cs` - Log entry DTO
- `ExecutionStatsDto.cs` - Statistics summary

**ExecutionResponse Properties:**
```csharp
- Id, PipelineId, PipelineName, TenantId, TenantName
- Status, StartTime, EndTime, Duration
- RecordsProcessed, RecordsSucceeded, RecordsFailed
- ProgressPercent, ErrorMessage
- Logs (List<ExecutionLogDto>)
- TriggeredBy, TriggeredByUserEmail
- CreatedAt
```

### 1.4 IExecutionService Interface

**Location:** `MultiTenantETL.Application/Executions/IExecutionService.cs`

**Methods:**
```csharp
Task<ExecutionResponse> StartExecutionAsync(Guid pipelineId, string triggeredBy, Guid? userId);
Task<ExecutionResponse> GetByIdAsync(Guid id);
Task<PagedExecutionResponse> GetAllAsync(ExecutionSearchRequest request);
Task<ExecutionResponse> CancelExecutionAsync(Guid id);
Task<ExecutionStatsDto> GetStatsAsync(Guid? pipelineId = null);
```

### 1.5 ExecutionService Implementation

**Location:** `MultiTenantETL.Infrastructure/Services/ExecutionService.cs`

**Initial Implementation:**
- Create execution record with "Queued" status
- Queue execution to background processor
- Return execution response immediately
- Background processor updates status to "Running"
- Log execution steps
- Update progress and metrics
- Handle completion/failure
- Support cancellation

### 1.6 ExecutionsController

**Location:** `MultiTenantETL.API/Controllers/ExecutionsController.cs`

**Endpoints:**
```
POST   /api/pipelines/{id}/execute     - Start pipeline execution
GET    /api/executions                 - List executions (paginated, filtered)
GET    /api/executions/{id}            - Get execution details
POST   /api/executions/{id}/cancel     - Cancel running execution
GET    /api/executions/stats           - Get execution statistics
```

### 1.7 Database Migration

**Create Migration:**
```bash
dotnet ef migrations add AddPipelineExecution --project src/MultiTenantETL.Infrastructure --startup-project src/MultiTenantETL.API
```

**Table:** `pipeline_executions`
- Indexes on: pipeline_id, tenant_id, status, start_time
- Foreign keys to pipelines and tenants

---

## Phase 2: Data Operations (Week 2-3)

### 2.1 Data Reader Interfaces

**Location:** `MultiTenantETL.Application/Connectors/DataReaders/`

**IDataReader.cs:**
```csharp
public interface IDataReader
{
    Task<DataReadResult> ReadAsync(Connector connector, CancellationToken cancellationToken);
    Task<bool> TestConnectionAsync(Connector connector);
    Task<SchemaDetectionResult> DetectSchemaAsync(Connector connector);
}
```

**DataReadResult.cs:**
```csharp
public class DataReadResult
{
    public List<Dictionary<string, object>> Rows { get; set; }
    public int TotalRows { get; set; }
    public SchemaInfo Schema { get; set; }
}
```

### 2.2 Data Reader Implementations

**Location:** `MultiTenantETL.Infrastructure/DataReaders/`

**Implementations Needed:**
1. **SqlServerDataReader.cs** - Read from SQL Server
2. **PostgreSqlDataReader.cs** - Read from PostgreSQL
3. **MySqlDataReader.cs** - Read from MySQL
4. **CsvDataReader.cs** - Read CSV files
5. **ExcelDataReader.cs** - Read Excel files (XLSX)
6. **JsonDataReader.cs** - Read JSON files/APIs
7. **RestApiDataReader.cs** - Read from REST APIs

**Each Reader Must:**
- Parse connector ConfigJson
- Establish connection
- Execute query/read file
- Return data in standardized format
- Handle errors gracefully
- Support cancellation

### 2.3 Data Writer Interfaces

**Location:** `MultiTenantETL.Application/Connectors/DataWriters/`

**IDataWriter.cs:**
```csharp
public interface IDataWriter
{
    Task<DataWriteResult> WriteAsync(
        Connector connector, 
        List<Dictionary<string, object>> data,
        CancellationToken cancellationToken);
}
```

**DataWriteResult.cs:**
```csharp
public class DataWriteResult
{
    public int RowsWritten { get; set; }
    public int RowsFailed { get; set; }
    public List<string> Errors { get; set; }
}
```

### 2.4 Data Writer Implementations

**Location:** `MultiTenantETL.Infrastructure/DataWriters/`

**Implementations Needed:**
1. **SqlServerDataWriter.cs** - Write to SQL Server
2. **PostgreSqlDataWriter.cs** - Write to PostgreSQL
3. **MySqlDataWriter.cs** - Write to MySQL
4. **CsvDataWriter.cs** - Write CSV files
5. **ExcelDataWriter.cs** - Write Excel files
6. **JsonDataWriter.cs** - Write JSON files
7. **RestApiDataWriter.cs** - POST to REST APIs

### 2.5 Connection Testing

**Update ConnectorService to implement:**
- `TestConnectionAsync()` - Use appropriate reader's test method
- Return success/failure with detailed message
- Store test results in connector entity

### 2.6 Schema Detection

**Implement Schema Detection:**
- SQL: Query information_schema
- CSV/Excel: Read first N rows, infer types
- JSON: Parse structure, infer schema
- API: Use OpenAPI spec or sample response

**Return SchemaInfo:**
```csharp
public class SchemaInfo
{
    public List<FieldDefinition> Fields { get; set; }
    public int Version { get; set; }
    public DateTime DetectedAt { get; set; }
}

public class FieldDefinition
{
    public string Name { get; set; }
    public string DataType { get; set; }
    public bool IsNullable { get; set; }
    public int? MaxLength { get; set; }
}
```

---

## Phase 3: Transformation Engine (Week 3-4)

### 3.1 Transformation Processor Interface

**Location:** `MultiTenantETL.Application/Transformations/Processors/`

**ITransformationProcessor.cs:**
```csharp
public interface ITransformationProcessor
{
    string TransformationType { get; }
    Task<TransformationResult> ProcessAsync(
        List<Dictionary<string, object>> data,
        Transformation transformation,
        CancellationToken cancellationToken);
}
```

**TransformationResult.cs:**
```csharp
public class TransformationResult
{
    public List<Dictionary<string, object>> Data { get; set; }
    public int RowsProcessed { get; set; }
    public int RowsFiltered { get; set; }
    public List<string> Warnings { get; set; }
}
```

### 3.2 Transformation Processor Implementations

**Location:** `MultiTenantETL.Infrastructure/Transformations/Processors/`

**1. FilterProcessor.cs**
- Parse filter rules from ConfigJson
- Apply conditions (equals, contains, greater than, less than, etc.)
- Filter rows based on field values
- Support multiple conditions with AND/OR logic

**2. MapProcessor.cs**
- Parse mapping rules from ConfigJson
- Transform field values using key-value mappings
- Support default values for unmapped keys

**3. TrimProcessor.cs**
- Parse field list from ConfigJson
- Trim whitespace from specified text fields
- Support trim start, trim end, or both

**4. CaseConvertProcessor.cs**
- Parse configuration (fields, case type)
- Convert to uppercase, lowercase, title case, or camelCase

**5. SubstringProcessor.cs**
- Parse configuration (field, start, length)
- Extract substring from text fields

**6. ReplaceProcessor.cs**
- Parse configuration (field, find, replace, useRegex)
- Find and replace text or patterns
- Support regex patterns

**7. ScriptProcessor.cs**
- Parse script code and language from ConfigJson
- Execute JavaScript using **Jint** library
- Execute C# using **Roslyn** scripting
- Sandbox execution for security
- Timeout protection
- Error handling

### 3.3 Transformation Orchestrator

**Location:** `MultiTenantETL.Infrastructure/Transformations/TransformationOrchestrator.cs`

**Purpose:** Apply multiple transformations in sequence

**Method:**
```csharp
public async Task<List<Dictionary<string, object>>> ApplyTransformationsAsync(
    List<Dictionary<string, object>> data,
    List<Transformation> transformations,
    IProgress<TransformationProgress> progress,
    CancellationToken cancellationToken)
```

**Logic:**
- Loop through transformations in order
- Get appropriate processor for each type
- Apply transformation to data
- Track progress and metrics
- Handle errors (fail fast or continue)

---

## Phase 4: Pipeline Orchestration (Week 4-5)

### 4.1 Pipeline Orchestrator

**Location:** `MultiTenantETL.Infrastructure/Pipelines/PipelineOrchestrator.cs`

**Purpose:** Coordinate the complete ETL pipeline execution

**Main Method:**
```csharp
public async Task ExecutePipelineAsync(
    Guid executionId,
    CancellationToken cancellationToken)
```

**Execution Flow:**
1. Load execution record
2. Load pipeline configuration
3. Load source and destination connectors
4. Load transformations from field mappings
5. **Extract Phase:**
   - Get appropriate data reader
   - Read data from source
   - Log extraction metrics
6. **Transform Phase:**
   - Apply field mappings
   - Execute transformations in sequence
   - Log transformation metrics
7. **Load Phase:**
   - Get appropriate data writer
   - Write data to destination
   - Log load metrics
8. Update execution status and metrics
9. Handle errors and rollback if needed

### 4.2 Background Job Processing

**Option A: Hangfire (Recommended)**

**Install NuGet:**
```bash
dotnet add package Hangfire.AspNetCore
dotnet add package Hangfire.PostgreSql
```

**Configure in Program.cs:**
```csharp
services.AddHangfire(config => config
    .UsePostgreSqlStorage(connectionString));
services.AddHangfireServer();
```

**Queue Execution:**
```csharp
BackgroundJob.Enqueue<PipelineOrchestrator>(
    x => x.ExecutePipelineAsync(executionId, CancellationToken.None));
```

**Option B: Quartz.NET**

**Install NuGet:**
```bash
dotnet add package Quartz
dotnet add package Quartz.Extensions.Hosting
```

**Configure Jobs:**
- Create `PipelineExecutionJob`
- Schedule recurring jobs for scheduled pipelines
- Support cron expressions

### 4.3 Scheduling System

**Create SchedulerService:**
- Load all active scheduled pipelines on startup
- Register cron jobs with Quartz/Hangfire
- Update schedules when pipelines change
- Handle timezone conversions

**Schedule Configuration (from Pipeline.ScheduleJson):**
```json
{
  "frequency": "daily",
  "time": "02:00",
  "timezone": "UTC",
  "cronExpression": "0 2 * * *",
  "enabled": true
}
```

### 4.4 Execution Cancellation

**Implement Cancellation:**
- Use CancellationTokenSource
- Check token at each phase (extract, transform, load)
- Update execution status to "Cancelled"
- Clean up resources
- Log cancellation event

### 4.5 Error Handling & Retry

**Error Handling Strategy:**
- Catch exceptions at each phase
- Log detailed error information
- Update execution status to "Failed"
- Store error message in execution record
- Send notification (optional)

**Retry Logic:**
- Configure retry policy (max attempts, backoff)
- Retry transient failures (network, timeout)
- Don't retry validation errors
- Log retry attempts

---

## Phase 5: Monitoring & Statistics (Week 5)

### 5.1 Dashboard Statistics Endpoint

**Location:** `MultiTenantETL.API/Controllers/DashboardController.cs`

**Endpoint:**
```
GET /api/dashboard/stats
```

**Response:**
```json
{
  "totalPipelines": 15,
  "activePipelines": 8,
  "connectors": 12,
  "recentExecutions": 45,
  "executionStats": {
    "last24Hours": {
      "total": 24,
      "completed": 20,
      "failed": 3,
      "running": 1
    }
  }
}
```

### 5.2 Recent Executions Endpoint

**Endpoint:**
```
GET /api/dashboard/recent-executions?limit=10
```

**Returns:** Last N executions with basic info

### 5.3 Execution Metrics Aggregation

**Create DashboardService:**
- Query execution statistics
- Aggregate by status, time period
- Calculate success rates
- Identify trending issues

### 5.4 Real-time Updates (Optional)

**Using SignalR:**
- Install SignalR NuGet package
- Create ExecutionHub
- Broadcast execution status changes
- Update frontend in real-time

---

## Implementation Checklist

### Phase 1: Core Execution ✅
- [ ] Create PipelineExecution entity
- [ ] Create ExecutionLog value object
- [ ] Create execution DTOs
- [ ] Create IExecutionService interface
- [ ] Implement ExecutionService
- [ ] Create ExecutionsController
- [ ] Create database migration
- [ ] Test basic execution flow

### Phase 2: Data Operations ✅
- [ ] Create IDataReader interface
- [ ] Implement SqlServerDataReader
- [ ] Implement PostgreSqlDataReader
- [ ] Implement MySqlDataReader
- [ ] Implement CsvDataReader
- [ ] Implement ExcelDataReader
- [ ] Implement JsonDataReader
- [ ] Implement RestApiDataReader
- [ ] Create IDataWriter interface
- [ ] Implement all data writers
- [ ] Implement connection testing
- [ ] Implement schema detection

### Phase 3: Transformation Engine ✅
- [ ] Create ITransformationProcessor interface
- [ ] Implement FilterProcessor
- [ ] Implement MapProcessor
- [ ] Implement TrimProcessor
- [ ] Implement CaseConvertProcessor
- [ ] Implement SubstringProcessor
- [ ] Implement ReplaceProcessor
- [ ] Implement ScriptProcessor (JavaScript)
- [ ] Implement ScriptProcessor (C#)
- [ ] Create TransformationOrchestrator
- [ ] Test all transformations

### Phase 4: Orchestration ✅
- [ ] Create PipelineOrchestrator
- [ ] Integrate Hangfire/Quartz
- [ ] Implement background job processing
- [ ] Create SchedulerService
- [ ] Implement execution cancellation
- [ ] Add error handling and retry logic
- [ ] Test end-to-end pipeline execution

### Phase 5: Monitoring ✅
- [ ] Create DashboardController
- [ ] Implement statistics aggregation
- [ ] Create DashboardService
- [ ] Add recent executions endpoint
- [ ] (Optional) Add SignalR for real-time updates
- [ ] Test dashboard integration

---

## NuGet Packages Required

```xml
<!-- Background Jobs -->
<PackageReference Include="Hangfire.AspNetCore" Version="1.8.9" />
<PackageReference Include="Hangfire.PostgreSql" Version="1.20.8" />

<!-- OR Quartz.NET -->
<PackageReference Include="Quartz" Version="3.8.0" />
<PackageReference Include="Quartz.Extensions.Hosting" Version="3.8.0" />

<!-- Database Drivers -->
<PackageReference Include="System.Data.SqlClient" Version="4.8.6" />
<PackageReference Include="Npgsql" Version="8.0.1" />
<PackageReference Include="MySqlConnector" Version="2.3.5" />

<!-- File Processing -->
<PackageReference Include="CsvHelper" Version="30.0.1" />
<PackageReference Include="EPPlus" Version="7.0.5" />
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />

<!-- Script Execution -->
<PackageReference Include="Jint" Version="3.0.0" />
<PackageReference Include="Microsoft.CodeAnalysis.CSharp.Scripting" Version="4.8.0" />

<!-- Real-time (Optional) -->
<PackageReference Include="Microsoft.AspNetCore.SignalR" Version="1.1.0" />
```

---

## Testing Strategy

### Unit Tests
- Test each transformation processor independently
- Test data readers with mock connections
- Test data writers with mock destinations
- Test orchestrator logic with mock dependencies

### Integration Tests
- Test actual database connections
- Test file reading/writing
- Test API calls
- Test end-to-end pipeline execution

### Performance Tests
- Test with large datasets (100K+ rows)
- Measure transformation performance
- Test concurrent executions
- Monitor memory usage

---

## Security Considerations

### Script Execution Sandbox
- Limit script execution time (timeout)
- Restrict access to system resources
- Validate script syntax before execution
- Log all script executions

### Connection Security
- Encrypt connection strings
- Use connection pooling
- Validate SSL certificates
- Implement connection timeouts

### Data Protection
- Don't log sensitive data
- Sanitize error messages
- Implement data masking for PII
- Audit all data access

---

## Performance Optimization

### Batch Processing
- Process data in batches (1000-5000 rows)
- Use bulk insert operations
- Stream large files instead of loading into memory

### Caching
- Cache connector configurations
- Cache transformation definitions
- Cache schema information

### Parallel Processing
- Process independent transformations in parallel
- Use async/await throughout
- Implement cancellation tokens

---

## Monitoring & Observability

### Logging
- Log execution start/end
- Log each phase (extract, transform, load)
- Log errors with stack traces
- Log performance metrics

### Metrics
- Track execution duration
- Track records processed per second
- Track success/failure rates
- Track resource usage

### Alerts
- Alert on execution failures
- Alert on performance degradation
- Alert on resource exhaustion

---

## Next Steps

1. **Review this document** with the team
2. **Prioritize phases** based on business needs
3. **Set up development environment** with required packages
4. **Create feature branches** for each phase
5. **Start with Phase 1** - Core Execution Infrastructure
6. **Iterate and test** each phase before moving to the next

---

## Questions to Answer Before Starting

1. Which background job processor? (Hangfire vs Quartz)
2. Should we support real-time execution updates? (SignalR)
3. What's the maximum dataset size we need to support?
4. Do we need transaction support for rollback?
5. Should failed executions auto-retry?
6. What notification channels for execution failures? (Email, Slack, etc.)
7. Do we need execution history retention policy?
8. Should we support pipeline versioning?

---

## Estimated Timeline

- **Phase 1:** 1-2 weeks (Core Execution)
- **Phase 2:** 1-2 weeks (Data Operations)
- **Phase 3:** 1-2 weeks (Transformation Engine)
- **Phase 4:** 1-2 weeks (Orchestration)
- **Phase 5:** 1 week (Monitoring)

**Total:** 5-9 weeks for complete implementation

---

## Success Criteria

✅ Users can execute pipelines manually from UI
✅ Pipelines can be scheduled to run automatically
✅ Executions are tracked with detailed logs
✅ Data flows from source → transformations → destination
✅ All 7 transformation types work correctly
✅ Dashboard shows real-time statistics
✅ Executions can be cancelled mid-flight
✅ Failed executions provide clear error messages
✅ System handles large datasets efficiently
✅ All operations are tenant-isolated

---

**Document Version:** 1.0  
**Last Updated:** 2025-11-27  
**Author:** Kiro AI Assistant  
**Status:** Ready for Implementation
