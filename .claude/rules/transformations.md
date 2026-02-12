# Transformations Implementation Guide

## Supported Transformation Types

### String Transformations
| Type | Purpose | Config | Example |
|------|---------|--------|---------|
| **trim** | Remove whitespace | None | `"  hello  "` → `"hello"` |
| **uppercase** | Convert to uppercase | None | `"hello"` → `"HELLO"` |
| **lowercase** | Convert to lowercase | None | `"HELLO"` → `"hello"` |
| **substring** | Extract substring | `startIndex`, `length` | `"hello"` → `"ell"` (start:1, len:3) |
| **replace** | Find and replace | `findValue`, `replaceWith` | `"hello world"` → `"hello there"` |
| **pad** | Pad string | `padChar`, `totalWidth`, `direction` | `"42"` → `"00042"` (left pad) |

### Field Transformations
| Type | Purpose | Config | Example |
|------|---------|--------|---------|
| **concat** | Concatenate fields | `separator`, `fields[]` | firstName + " " + lastName |
| **map** | Value mapping/lookup | `mappings[]` (key→value) | `"CA"` → `"California"` |

### Filter Transformations
| Type | Purpose | Config | Example |
|------|---------|--------|---------|
| **filter** | Include/exclude rows | `operator`, `value` | Only rows where status = "active" |

**Filter Operators:**
- `equals`: Exact match
- `notEquals`: Not equal
- `contains`: Contains substring
- `notContains`: Does not contain
- `startsWith`: Starts with
- `endsWith`: Ends with
- `regex`: Regular expression match
- `greaterThan`, `lessThan`: Numeric comparison

### Script Transformations
| Type | Purpose | Config | Example |
|------|---------|--------|---------|
| **script** | Custom JavaScript | `scriptCode` | `return value.toUpperCase().substring(0, 3);` |

## Field Mapping Configuration Structure

**Stored in:** `Pipeline.FieldMappingsJson` as JSON array

```json
[
  {
    "id": "mapping-1",
    "sourceField": "customer_name",
    "targetField": "CustomerName",
    "transformations": [
      {
        "id": "trim-1",
        "type": "trim",
        "order": 1,
        "isEnabled": true,
        "config": {}
      },
      {
        "id": "uppercase-1",
        "type": "uppercase",
        "order": 2,
        "isEnabled": true,
        "config": {}
      }
    ]
  },
  {
    "id": "mapping-2",
    "sourceField": "state_code",
    "targetField": "StateName",
    "transformations": [
      {
        "id": "map-1",
        "type": "map",
        "order": 1,
        "isEnabled": true,
        "config": {
          "mappings": [
            { "key": "CA", "value": "California" },
            { "key": "NY", "value": "New York" },
            { "key": "TX", "value": "Texas" }
          ],
          "defaultValue": "Unknown"
        }
      }
    ]
  }
]
```

## Adding a New Transformation Type

### Step 1: Create Processor Class
**Location:** `Infrastructure/Transformations/Processors/{Type}Processor.cs`

```csharp
public class NewTypeProcessor : ITransformationProcessor
{
    public string TransformationType => "newType";

    public object? ProcessTransformation(
        object? value,
        TransformationConfig transformation,
        Dictionary<string, object?>? rowData = null)
    {
        if (value == null) return null;

        // Parse configuration
        var config = transformation.GetConfig<NewTypeConfig>();

        // Validate input type
        if (value is not string stringValue)
        {
            throw new InvalidOperationException(
                $"NewType transformation requires string input, got {value.GetType().Name}");
        }

        // Apply transformation logic
        var result = ApplyNewTypeLogic(stringValue, config);

        return result;
    }

    private string ApplyNewTypeLogic(string input, NewTypeConfig config)
    {
        // Transformation implementation
        return input; // Replace with actual logic
    }
}
```

### Step 2: Create Configuration Model
**Location:** `Application/Transformations/Models/NewTypeConfig.cs`

```csharp
public class NewTypeConfig
{
    public string Parameter1 { get; set; } = string.Empty;
    public int Parameter2 { get; set; }
    public bool Parameter3 { get; set; }
}
```

### Step 3: Register in Main Processor
**Location:** `Infrastructure/Transformations/FieldTransformationProcessor.cs`

```csharp
private readonly Dictionary<string, ITransformationProcessor> _processors;

public FieldTransformationProcessor(IServiceProvider serviceProvider)
{
    _processors = new Dictionary<string, ITransformationProcessor>
    {
        ["trim"] = new TrimProcessor(),
        ["uppercase"] = new UppercaseProcessor(),
        ["newType"] = serviceProvider.GetRequiredService<NewTypeProcessor>(),
        // ... other processors
    };
}
```

### Step 4: Add Validation Rules
**Location:** `Application/Transformations/Validators/NewTypeConfigValidator.cs`

```csharp
public class NewTypeConfigValidator : AbstractValidator<NewTypeConfig>
{
    public NewTypeConfigValidator()
    {
        RuleFor(x => x.Parameter1)
            .NotEmpty()
            .WithMessage("Parameter1 is required");

        RuleFor(x => x.Parameter2)
            .GreaterThan(0)
            .WithMessage("Parameter2 must be positive");
    }
}
```

### Step 5: Register in DI
**Location:** `Infrastructure/DependencyInjection.cs`

```csharp
services.AddTransient<NewTypeProcessor>();
services.AddTransient<IValidator<NewTypeConfig>, NewTypeConfigValidator>();
```

## Transformation Execution Flow

```
1. Load Field Mappings from Pipeline.FieldMappingsJson
   └─ Parse JSON to FieldMappingConfig[] objects

2. For Each Source Record:
   ├─ For Each Field Mapping:
   │  ├─ Extract source field value(s)
   │  ├─ Apply transformations in order (sort by Order property)
   │  │  └─ Only apply if IsEnabled = true
   │  └─ Assign to target field
   └─ Return transformed record

3. Apply Filter Transformations (Row-Level)
   └─ Exclude rows that don't match filter criteria

4. Write Batch to Destination
```

## Field Mapping Service Implementation

**Location:** `Infrastructure/Orchestration/FieldMappingService.cs`

**Key Methods:**

```csharp
// Maps entire batch of records
public async Task<WriteBatch> MapBatchAsync(
    ReadBatch sourceBatch,
    List<FieldMappingConfig> mappings,
    CancellationToken cancellationToken = default)
{
    var transformedRecords = new List<Dictionary<string, object?>>();

    foreach (var sourceRecord in sourceBatch.Records)
    {
        var transformedRecord = MapRecord(sourceRecord, mappings);

        // Apply filter transformations
        if (!ShouldIncludeRecord(transformedRecord, mappings))
            continue;

        transformedRecords.Add(transformedRecord);
    }

    return new WriteBatch
    {
        FieldNames = mappings.Select(m => m.TargetField).ToList(),
        Records = transformedRecords,
        BatchNumber = sourceBatch.BatchNumber
    };
}

// Maps a single record through all field mappings
private Dictionary<string, object?> MapRecord(
    Dictionary<string, object?> sourceRecord,
    List<FieldMappingConfig> mappings)
{
    var targetRecord = new Dictionary<string, object?>();

    foreach (var mapping in mappings)
    {
        var sourceValue = sourceRecord.GetValueOrDefault(mapping.SourceField);
        var transformedValue = ApplyTransformations(sourceValue, mapping, sourceRecord);
        targetRecord[mapping.TargetField] = transformedValue;
    }

    return targetRecord;
}

// Applies ordered transformations to a single field
private object? ApplyTransformations(
    object? value,
    FieldMappingConfig mapping,
    Dictionary<string, object?> rowData)
{
    var currentValue = value;

    foreach (var transformation in mapping.Transformations
        .Where(t => t.IsEnabled)
        .OrderBy(t => t.Order))
    {
        currentValue = _transformationProcessor.ApplyTransformation(
            currentValue,
            transformation,
            rowData);
    }

    return currentValue;
}
```

## Transformation Examples

### Example 1: Clean and Normalize Name
```json
{
  "sourceField": "customer_name",
  "targetField": "CustomerName",
  "transformations": [
    { "type": "trim", "order": 1, "isEnabled": true },
    { "type": "uppercase", "order": 2, "isEnabled": true }
  ]
}
```
**Input:** `"  john doe  "` → **Output:** `"JOHN DOE"`

### Example 2: Format Phone Number
```json
{
  "sourceField": "phone",
  "targetField": "PhoneFormatted",
  "transformations": [
    {
      "type": "replace",
      "order": 1,
      "config": { "findValue": "-", "replaceWith": "" }
    },
    {
      "type": "script",
      "order": 2,
      "config": {
        "scriptCode": "return '(' + value.substring(0,3) + ') ' + value.substring(3,6) + '-' + value.substring(6);"
      }
    }
  ]
}
```
**Input:** `"555-123-4567"` → **Output:** `"(555) 123-4567"`

### Example 3: Concatenate Fields
```json
{
  "sourceFields": ["firstName", "lastName"],
  "targetField": "FullName",
  "transformations": [
    {
      "type": "concat",
      "order": 1,
      "config": {
        "separator": " ",
        "fields": ["firstName", "lastName"]
      }
    }
  ]
}
```
**Input:** `firstName: "John"`, `lastName: "Doe"` → **Output:** `"John Doe"`

### Example 4: Filter Active Records Only
```json
{
  "sourceField": "status",
  "transformations": [
    {
      "type": "filter",
      "order": 1,
      "config": {
        "operator": "equals",
        "value": "active"
      }
    }
  ]
}
```
**Result:** Only rows with `status = "active"` are included

### Example 5: State Code to Name Mapping
```json
{
  "sourceField": "state",
  "targetField": "stateName",
  "transformations": [
    {
      "type": "map",
      "order": 1,
      "config": {
        "mappings": [
          { "key": "CA", "value": "California" },
          { "key": "NY", "value": "New York" },
          { "key": "TX", "value": "Texas" }
        ],
        "defaultValue": "Unknown State"
      }
    }
  ]
}
```
**Input:** `"CA"` → **Output:** `"California"`

## Script Transformation (JavaScript)

**Engine:** Jint 4.4.2 (JavaScript interpreter for .NET)

**Available Context:**
- `value` - Current field value
- `row` - Entire source row as object (e.g., `row.firstName`)
- JavaScript standard library functions

**Example Scripts:**

```javascript
// Extract first 3 characters and uppercase
return value.toUpperCase().substring(0, 3);

// Calculate age from birthdate
var birthYear = new Date(row.birthdate).getFullYear();
var currentYear = new Date().getFullYear();
return currentYear - birthYear;

// Conditional formatting
if (value > 1000) {
  return "High";
} else if (value > 100) {
  return "Medium";
} else {
  return "Low";
}

// Complex string manipulation
return value
  .toLowerCase()
  .replace(/[^a-z0-9]/g, '-')
  .replace(/-+/g, '-')
  .replace(/^-|-$/g, '');
```

**Security Considerations:**
- Scripts run in sandboxed Jint environment
- No access to file system, network, or .NET APIs
- Timeout configured to prevent infinite loops
- Memory limits enforced

## Performance Optimization

**Best Practices:**
1. **Order transformations efficiently** - Put filters early to reduce processing
2. **Disable unused transformations** - Set `isEnabled: false` instead of deleting
3. **Avoid script transformations when possible** - Native transformations are faster
4. **Use specific operators** - `equals` is faster than `regex`
5. **Batch size tuning** - Default 1,000 rows balances memory vs. throughput

**Transformation Cost (fastest to slowest):**
1. trim, uppercase, lowercase (string operations)
2. substring, replace, pad (indexed operations)
3. concat, map (multi-value operations)
4. filter (conditional logic)
5. script (JavaScript interpretation)
