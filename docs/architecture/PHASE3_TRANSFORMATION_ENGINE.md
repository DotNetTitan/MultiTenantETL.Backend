# Phase 3: Transformation Engine - Implementation Summary

## Overview

Complete implementation of the transformation engine with batch-oriented processing, multiple processor types, and JavaScript scripting support using Jint.

## Architecture

### Core Components

1. **ITransformationProcessor** - Base interface for all transformation processors
2. **ITransformationOrchestrator** - Orchestrates multiple transformations in sequence
3. **TransformationResult** - Result model with metrics and error tracking
4. **TransformationPolicy** - Error handling policies (FailFast, ContinueOnError, ContinueWithWarnings)

### Transformation Flow

```
ReadBatch → Transformation 1 → Transformation 2 → ... → Transformation N → TransformedBatch
              (Order: 1)         (Order: 2)              (Order: N)
```

Each transformation:
- Receives a batch of rows
- Processes according to its type and configuration
- Returns transformed rows with metrics
- Errors are tracked per-row with details

## Implemented Processors

### 1. FilterProcessor
**Type:** `Filter`

**Purpose:** Filter rows based on conditions

**Supported Operators:**
- `equals`, `not_equals`
- `contains`, `starts_with`, `ends_with`
- `greater_than`, `less_than`, `greater_than_or_equal`, `less_than_or_equal`
- `is_null`, `is_not_null`
- `in`, `not_in` (with array of values)

**Configuration Example:**
```json
{
  "field": "age",
  "operator": "greater_than",
  "value": "18"
}
```

**Use Cases:**
- Filter out invalid records
- Keep only active users
- Remove test data

### 2. MapProcessor
**Type:** `Map`

**Purpose:** Map field names and values

**Mapping Types:**
- `rename` - Rename source field to destination
- `copy` - Copy field to new name (keep both)
- `constant` - Set field to constant value
- `keep` - Pass through unchanged
- `remove` - Exclude field from output

**Configuration Example:**
```json
{
  "mappings": [
    { "type": "rename", "source": "first_name", "destination": "firstName" },
    { "type": "constant", "destination": "status", "value": "active" },
    { "type": "remove", "source": "internal_id" }
  ],
  "unmappedFieldStrategy": "keep"
}
```

**Use Cases:**
- Rename fields for target system
- Add constant fields (tenant_id, created_date)
- Remove sensitive fields

### 3. StringProcessor
**Type:** `String`

**Purpose:** Perform string operations on fields

**Supported Operations:**
- `trim`, `trim_start`, `trim_end`
- `upper`, `lower`, `title_case`
- `substring` (with start and length)
- `replace` (simple string replacement)
- `regex_replace` (regex pattern replacement)
- `pad_left`, `pad_right`
- `remove_whitespace`, `normalize_whitespace`

**Configuration Example:**
```json
{
  "fields": ["name", "email"],
  "operation": "trim"
}
```

**Use Cases:**
- Clean whitespace from inputs
- Normalize casing
- Extract substrings
- Format data consistently

### 4. ScriptProcessor
**Type:** `Script`

**Purpose:** Execute custom JavaScript code on rows

**JavaScript Engine:** Jint 4.4.2 (sandboxed JavaScript interpreter)

**Security Features:**
- Execution timeout (default: 5 seconds)
- Recursion depth limit (default: 100)
- Statement count limit (default: 10,000)
- No access to .NET CLR
- Strict mode enabled

**Available Variables:**
- `row` - Current row object
- `rowIndex` - Index of current row (0-based)
- `log(message)` - Log function (if detailed logging enabled)

**Return Values:**
- **Object** - Transformed row
- **Boolean** - true to keep row, false to filter out
- **null/undefined** - Filter out row

**Configuration Example:**
```json
{
  "script": "row.fullName = row.firstName + ' ' + row.lastName; return row;",
  "timeoutMs": 5000,
  "maxRecursionDepth": 100,
  "maxStatements": 10000,
  "includeErrorRows": false
}
```

**Use Cases:**
- Complex field calculations
- Conditional transformations
- Custom business logic
- Data enrichment

**Example Scripts:**

```javascript
// Calculate full name
row.fullName = row.firstName + ' ' + row.lastName;
return row;

// Filter based on complex condition
if (row.age >= 18 && row.country === 'US') {
  return row;
}
return null; // Filter out

// Transform and enrich
row.email = row.email.toLowerCase();
row.domain = row.email.split('@')[1];
row.processedAt = new Date().toISOString();
return row;

// Conditional transformation
if (row.status === 'pending') {
  row.status = 'active';
  row.activatedAt = new Date().toISOString();
}
return row;
```

## Transformation Orchestrator

### Features

- **Sequential Execution** - Transformations execute in order
- **Policy-Based Error Handling** - Three policies supported
- **Metrics Tracking** - Per-step and total metrics
- **Cancellation Support** - Respects cancellation tokens

### Transformation Policies

#### 1. FailFast
- Stop on first error
- Return immediately
- Best for: Critical pipelines where data quality is paramount

#### 2. ContinueOnError
- Skip rows with errors
- Continue processing remaining rows
- Best for: Pipelines where some data loss is acceptable

#### 3. ContinueWithWarnings
- Keep rows with errors
- Log warnings
- Best for: Debugging and data quality analysis

### Orchestration Result

```csharp
public class TransformationOrchestrationResult
{
    public ReadBatch TransformedBatch { get; set; }
    public List<TransformationStepResult> StepResults { get; set; }
    public int InitialRowCount { get; set; }
    public int FinalRowCount { get; set; }
    public int TotalRowsFiltered { get; set; }
    public int TotalRowsWithErrors { get; set; }
    public TimeSpan TotalExecutionTime { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
```

## Entity Updates

### Transformation Entity

Added fields:
- `PipelineId` (Guid) - Links transformation to pipeline
- `Order` (int) - Execution order (lower numbers first)
- `IsEnabled` (bool) - Enable/disable transformation
- `Pipeline` navigation property

### ITransformationService

Added method:
- `GetByPipelineIdAsync(Guid pipelineId, CancellationToken ct)` - Get all enabled transformations for a pipeline, ordered by Order field

## Dependency Injection

All processors and orchestrator registered in `Program.cs`:

```csharp
// Transformation Orchestrator
builder.Services.AddScoped<ITransformationOrchestrator, TransformationOrchestrator>();

// Transformation Processors
builder.Services.AddScoped<ITransformationProcessor, FilterProcessor>();
builder.Services.AddScoped<ITransformationProcessor, MapProcessor>();
builder.Services.AddScoped<ITransformationProcessor, StringProcessor>();
builder.Services.AddScoped<ITransformationProcessor, ScriptProcessor>();
```

## Usage Example

### Creating Transformations

```csharp
// 1. Filter: Keep only adults
var filter = new Transformation
{
    PipelineId = pipelineId,
    Name = "Filter Adults",
    Type = "Filter",
    Order = 1,
    ConfigJson = JsonSerializer.Serialize(new
    {
        field = "age",
        @operator = "greater_than_or_equal",
        value = "18"
    })
};

// 2. Map: Rename fields
var map = new Transformation
{
    PipelineId = pipelineId,
    Name = "Rename Fields",
    Type = "Map",
    Order = 2,
    ConfigJson = JsonSerializer.Serialize(new
    {
        mappings = new[]
        {
            new { type = "rename", source = "first_name", destination = "firstName" },
            new { type = "rename", source = "last_name", destination = "lastName" }
        },
        unmappedFieldStrategy = "keep"
    })
};

// 3. String: Clean email
var stringOp = new Transformation
{
    PipelineId = pipelineId,
    Name = "Clean Email",
    Type = "String",
    Order = 3,
    ConfigJson = JsonSerializer.Serialize(new
    {
        fields = new[] { "email" },
        operation = "lower"
    })
};

// 4. Script: Calculate full name
var script = new Transformation
{
    PipelineId = pipelineId,
    Name = "Calculate Full Name",
    Type = "Script",
    Order = 4,
    ConfigJson = JsonSerializer.Serialize(new
    {
        script = "row.fullName = row.firstName + ' ' + row.lastName; return row;",
        timeoutMs = 5000
    })
};
```

### Applying Transformations

```csharp
var orchestrator = serviceProvider.GetRequiredService<ITransformationOrchestrator>();

var result = await orchestrator.ApplyTransformationsAsync(
    batch,
    pipelineId,
    TransformationPolicy.ContinueOnError,
    cancellationToken);

if (result.Success)
{
    Console.WriteLine($"Transformed {result.InitialRowCount} → {result.FinalRowCount} rows");
    Console.WriteLine($"Filtered: {result.TotalRowsFiltered}, Errors: {result.TotalRowsWithErrors}");
    Console.WriteLine($"Execution time: {result.TotalExecutionTime.TotalMilliseconds}ms");
    
    // Use transformed batch
    var transformedBatch = result.TransformedBatch;
}
```

## Performance Considerations

### Memory Usage
- Transformations process batches, not entire datasets
- Each transformation creates a new batch (immutable pattern)
- Memory usage: O(batch_size × transformation_count)

### Execution Time
- Sequential execution (no parallelism between transformations)
- ScriptProcessor is slowest (JavaScript interpretation)
- Filter/Map/String processors are fast (native C#)

### Optimization Tips
1. **Order matters** - Put filters early to reduce rows processed
2. **Batch size** - Larger batches = better throughput, more memory
3. **Script complexity** - Keep scripts simple for better performance
4. **Combine operations** - Use one script instead of multiple simple transformations

## Security

### ScriptProcessor Security

**Sandboxing:**
- No access to .NET CLR or file system
- No network access
- No reflection or dynamic code loading

**Resource Limits:**
- Execution timeout prevents infinite loops
- Recursion limit prevents stack overflow
- Statement limit prevents excessive computation

**Best Practices:**
- Only allow trusted users to create script transformations
- Audit all script transformations
- Monitor execution times
- Set conservative limits in production

## Error Handling

### Per-Row Errors

Errors tracked with:
- Row index
- Error message
- Error code
- Field name (if applicable)
- Original row data (for debugging)

### Batch-Level Errors

Transformation failures logged with:
- Transformation name and type
- Exception details
- Policy applied

### Error Recovery

- **FailFast** - Stop immediately, return original batch
- **ContinueOnError** - Skip error rows, continue with valid rows
- **ContinueWithWarnings** - Keep error rows, log warnings

## Testing Recommendations

### Unit Tests
- Test each processor independently
- Test with valid and invalid configurations
- Test error conditions
- Test edge cases (empty batches, null values)

### Integration Tests
- Test orchestrator with multiple transformations
- Test all three policies
- Test cancellation
- Test with realistic data volumes

### Performance Tests
- Measure throughput (rows/second)
- Test with large batches (10k+ rows)
- Test script execution limits
- Profile memory usage

## Future Enhancements

### Potential Additions
- [ ] **AggregateProcessor** - Group by and aggregate functions
- [ ] **JoinProcessor** - Join with lookup data
- [ ] **ValidationProcessor** - Data quality rules
- [ ] **SplitProcessor** - Split one row into multiple
- [ ] **PivotProcessor** - Pivot/unpivot operations
- [ ] **DateProcessor** - Date parsing and formatting
- [ ] **TypeConversionProcessor** - Type casting and conversion
- [ ] **Parallel execution** - Process independent transformations in parallel
- [ ] **Caching** - Cache lookup data for performance
- [ ] **Dry-run mode** - Preview transformations without applying

## Files Created

### Application Layer
- `src/MultiTenantETL.Application/Transformations/ITransformationProcessor.cs`
- `src/MultiTenantETL.Application/Transformations/ITransformationOrchestrator.cs`

### Infrastructure Layer
- `src/MultiTenantETL.Infrastructure/Transformations/Processors/FilterProcessor.cs`
- `src/MultiTenantETL.Infrastructure/Transformations/Processors/MapProcessor.cs`
- `src/MultiTenantETL.Infrastructure/Transformations/Processors/StringProcessor.cs`
- `src/MultiTenantETL.Infrastructure/Transformations/Processors/ScriptProcessor.cs`
- `src/MultiTenantETL.Infrastructure/Transformations/TransformationOrchestrator.cs`

### Domain Layer
- Updated `src/MultiTenantETL.Domain/Entities/Transformation.cs` (added PipelineId, Order, IsEnabled)

### Service Layer
- Updated `src/MultiTenantETL.Application/Transformations/ITransformationService.cs` (added GetByPipelineIdAsync)
- Updated `src/MultiTenantETL.Infrastructure/Services/TransformationService.cs` (implemented GetByPipelineIdAsync)

### Dependencies
- Added **Jint 4.4.2** - JavaScript interpreter for ScriptProcessor

## Success Criteria

✅ **Four processor types implemented** - Filter, Map, String, Script  
✅ **Orchestrator with policies** - FailFast, ContinueOnError, ContinueWithWarnings  
✅ **Per-row error tracking** - Detailed error information  
✅ **JavaScript scripting** - Sandboxed with security limits  
✅ **Batch-oriented processing** - Memory-bounded operations  
✅ **Sequential execution** - Ordered transformation pipeline  
✅ **Metrics and observability** - Execution time, row counts, errors  
✅ **Dependency injection** - All components registered  

---

**Document Version:** 1.0  
**Last Updated:** 2025-11-28  
**Status:** Phase 3 Complete - Ready for Testing
