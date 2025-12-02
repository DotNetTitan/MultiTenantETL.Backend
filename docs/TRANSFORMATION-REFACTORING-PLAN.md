# Transformation & Field Mapping Refactoring Plan

## Current Problems

1. **Dual Storage**: Transformations exist as both standalone entities and embedded in field mappings
2. **Mixed Concerns**: Field mappings contain transformation references, making the data model confusing
3. **Unclear Data Flow**: Hard to understand how data flows through transformations
4. **Scattered Validation**: Validation logic exists in multiple places
5. **Schema Tracking**: Difficult to track schema changes through transformation chain

## Proposed Architecture

### Clean Hierarchy

```
Pipeline
├── Source Connector
├── Destination Connector
└── Field Mappings []
    ├── Source Fields []
    ├── Destination Field
    └── Transformations [] (ordered)
        ├── Type
        ├── Config
        └── Order
```

### Key Principles

1. **Single Source of Truth**: Transformations are ONLY stored within field mappings
2. **Clear Data Flow**: Source → Transformations (ordered) → Destination
3. **Field-Level Transformations**: Each field mapping has its own transformation chain
4. **Reusable Transformation Templates**: Standalone transformations are templates that can be instantiated
5. **Explicit Ordering**: Transformations execute in defined order within each field mapping

## Data Model Changes

### Backend (C#)

#### Pipeline Entity (Domain/Entities/Pipeline.cs)
```csharp
public class Pipeline
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    
    public Guid SourceConnectorId { get; set; }
    public Guid DestinationConnectorId { get; set; }
    
    public string Status { get; set; }
    
    // JSON: Array of FieldMapping objects
    public string FieldMappingsJson { get; set; }
    
    public string? ScheduleJson { get; set; }
    public bool IsScheduled { get; set; }
    public bool IsActive { get; set; }
    
    // Audit fields...
}
```

#### Transformation Entity (Domain/Entities/Transformation.cs)
**Purpose**: Template library for reusable transformations

```csharp
public class Transformation
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public string Type { get; set; } // Filter, Map, Trim, etc.
    public string ConfigJson { get; set; } // Template configuration
    public bool IsTemplate { get; set; } = true; // Mark as template
    
    // Audit fields...
}
```

#### Field Mapping Model (Application/Pipelines/Models/FieldMapping.cs)
```csharp
public record FieldMapping
{
    public string Id { get; init; }
    public List<string> SourceFields { get; init; } = new();
    public string DestinationField { get; init; }
    public List<FieldTransformation> Transformations { get; init; } = new();
    public int Order { get; init; }
}

public record FieldTransformation
{
    public string Id { get; init; }
    public string Type { get; init; } // Filter, Map, Trim, CaseConvert, Substring, Replace, Script
    public JsonElement Config { get; init; } // Type-specific configuration
    public int Order { get; init; }
    public bool IsEnabled { get; init; } = true;
}
```

### Frontend (Vue)

#### Field Mapping Structure
```javascript
{
  id: 'mapping-uuid',
  sourceFields: ['field1', 'field2'], // Can combine multiple sources
  destinationField: 'target_field',
  transformations: [
    {
      id: 'trans-uuid',
      type: 'Trim',
      config: { /* type-specific */ },
      order: 1,
      isEnabled: true
    },
    {
      id: 'trans-uuid-2',
      type: 'CaseConvert',
      config: { caseType: 'uppercase' },
      order: 2,
      isEnabled: true
    }
  ],
  order: 1
}
```

## Implementation Steps

### Phase 1: Backend Refactoring

1. ✅ Update `FieldMapping` and `FieldTransformation` models
2. ✅ Remove `PipelineId` from `Transformation` entity (make it template-only)
3. ✅ Update `PipelineService` to handle embedded transformations
4. ✅ Create transformation execution engine that processes field mappings
5. ✅ Update validation logic to validate transformation chains

### Phase 2: Frontend Refactoring

1. ✅ Update `FieldMappingEditor.vue` to manage transformations per field
2. ✅ Create `TransformationChainEditor.vue` component for field-level transformations
3. ✅ Update `MappingCard.vue` to show transformation chain
4. ✅ Refactor `useTransformation.js` composable
5. ✅ Update `TransformationsView.vue` to show templates only

### Phase 3: Migration

1. ✅ Create database migration to update existing pipelines
2. ✅ Convert old transformation references to embedded transformations
3. ✅ Mark existing transformations as templates

### Phase 4: Testing & Documentation

1. ✅ Update API documentation
2. ✅ Update user documentation
3. ✅ Test transformation execution
4. ✅ Test validation logic

## Benefits

1. **Clarity**: Clear data flow from source through transformations to destination
2. **Flexibility**: Each field can have its own transformation chain
3. **Reusability**: Transformation templates can be reused across pipelines
4. **Maintainability**: Single source of truth for transformation configuration
5. **Validation**: Easier to validate transformation chains and schema compatibility
6. **Performance**: Transformations execute in parallel for different fields

## Migration Strategy

### Backward Compatibility

During migration, support both old and new formats:

1. Detect old format (transformation references by ID)
2. Load transformation configuration from database
3. Convert to new embedded format
4. Save updated pipeline

### Data Migration Script

```sql
-- Mark all existing transformations as templates
UPDATE transformations SET is_template = true;

-- Pipeline field mappings will be migrated via application code
-- on first load/save after deployment
```

## Example Usage

### Creating a Pipeline with Field Mappings

```javascript
const pipeline = {
  name: 'Customer Data Import',
  sourceConnectorId: 'source-uuid',
  destinationConnectorId: 'dest-uuid',
  fieldMappings: [
    {
      id: 'mapping-1',
      sourceFields: ['first_name', 'last_name'],
      destinationField: 'full_name',
      transformations: [
        {
          id: 'trans-1',
          type: 'Script',
          config: {
            scriptLanguage: 'javascript',
            script: 'return sourceFields[0] + " " + sourceFields[1];'
          },
          order: 1
        }
      ],
      order: 1
    },
    {
      id: 'mapping-2',
      sourceFields: ['email'],
      destinationField: 'email',
      transformations: [
        {
          id: 'trans-2',
          type: 'Trim',
          config: {},
          order: 1
        },
        {
          id: 'trans-3',
          type: 'CaseConvert',
          config: { caseType: 'lowercase' },
          order: 2
        }
      ],
      order: 2
    }
  ]
};
```

## Validation Rules

1. **Field Mapping Validation**:
   - At least one source field required
   - Destination field required
   - Source fields must exist in source schema
   - Destination field must exist in destination schema

2. **Transformation Chain Validation**:
   - Transformations execute in order
   - Each transformation must have valid configuration
   - Type compatibility checked between transformations
   - Script transformations validated for syntax

3. **Schema Validation**:
   - Source field types compatible with first transformation
   - Final transformation output compatible with destination field type
   - Required destination fields must be mapped

## Future Enhancements

1. **Transformation Library**: UI for browsing and selecting transformation templates
2. **Visual Pipeline Builder**: Drag-and-drop interface for building transformation chains
3. **Transformation Testing**: Test transformations with sample data before saving
4. **Performance Optimization**: Parallel execution of independent field mappings
5. **Transformation Marketplace**: Share transformation templates across tenants
