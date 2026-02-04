# Transformations API Specification

**Resource:** Transformations  
**Base Path:** `/api/transformations`  
**Version:** 2.0  
**Last Updated:** 2025-11-19

---

## Overview

Transformations define data manipulation logic that can be applied to fields during ETL pipeline execution. They can be reused across multiple pipelines.

### Supported Transformation Types
- **Filter** - Filter rows based on conditions
- **Map** - Map values from one set to another
- **Trim** - Remove leading/trailing whitespace
- **Case Convert** - Convert text case (uppercase, lowercase, titlecase)
- **Substring** - Extract portion of a string
- **Replace** - Find and replace text patterns
- **Script** - Custom JavaScript or C# logic

> **Backend Implementation Note:**  
> The API uses display names (`"Filter"`, `"Case Convert"`), but the backend stores codes (`"filter"`, `"case_convert"`) in the `transformation_types` lookup table. See [Database Implementation](./database-implementation.md#transformation-types) for details.

---

## Endpoints

### GET /api/transformations

Gets all transformations with optional filtering and sorting.

**Query Parameters:**
- `search` (string, optional): Search in name, description
- `type` (string, optional): Filter by type - "Filter", "Map", "Trim", "Case Convert", "Substring", "Replace", "Script", "All" (default: "All")
- `sort` (string, optional): Sort field and direction
  - `name_asc` | `name_desc`
  - `createdAt_asc` | `createdAt_desc`
  - `type_asc` | `type_desc`

**Success Response (200):**
```json
[
  {
    "id": "string",
    "name": "string",
    "type": "Filter|Map|Trim|Case Convert|Substring|Replace|Script",
    "description": "string",
    "createdAt": "2025-11-19T03:44:49-05:00",
    "connectorId": "string (optional, if transformation is connector-specific)",
    "connectorName": "string (optional)",
    "usedInPipelines": ["Sales Data ETL", "Customer Import"],
    "config": {
      // Type-specific configuration (see below)
    }
  }
]
```

---

### GET /api/transformations/{id}

Gets a single transformation by ID.

**URL Parameters:**
- `id` (string, required): Transformation ID

**Success Response (200):**
Returns a single transformation object.

**Error Responses:**
- `404 Not Found`: Transformation not found

---

### POST /api/transformations

Creates a new transformation.

**Request Body:**
```json
{
  "name": "string (required, 3-100 chars)",
  "type": "Filter|Map|Trim|Case Convert|Substring|Replace|Script (required)",
  "description": "string (optional, max 500 chars)",
  "connectorId": "string (optional)",
  "config": {
    // Type-specific configuration (see below)
  }
}
```

**Success Response (201):**
Returns the created transformation object with `id` and `createdAt` populated.

**Error Responses:**
- `400 Bad Request`: Validation error
- `409 Conflict`: Transformation name already exists

---

### PUT /api/transformations/{id}

Updates an existing transformation.

**URL Parameters:**
- `id` (string, required): Transformation ID

**Request Body:**
Same as POST /api/transformations (all fields optional for partial update).

**Important Notes:**
- Updating a transformation affects all pipelines using it
- Consider cloning the transformation if you want separate versions

**Success Response (200):**
Returns the updated transformation object.

**Error Responses:**
- `404 Not Found`: Transformation not found
- `400 Bad Request`: Validation error

---

### DELETE /api/transformations/{id}

Deletes a transformation.

**URL Parameters:**
- `id` (string, required): Transformation ID

**Success Response (200):**
```json
{
  "success": true
}
```

**Error Responses:**
- `404 Not Found`: Transformation not found
- `400 Bad Request`: Transformation is in use by pipelines (provide list of pipelines)

---

### POST /api/transformations/validate

Validates a transformation configuration against an input schema.

**Request Body:**
```json
{
  "transformation": {
    // Transformation object
  },
  "inputSchema": {
    "fields": [
      {"name": "field1", "type": "string"},
      {"name": "field2", "type": "integer"}
    ]
  }
}
```

**Success Response (200):**
```json
{
  "isValid": true,
  "errors": [],
  "outputSchema": {
    "fields": [
      {"name": "field1", "type": "string"},
      {"name": "field2", "type": "integer"}
    ]
  }
}
```

**Error Response (400):**
```json
{
  "isValid": false,
  "errors": [
    {
      "field": "config.field",
      "message": "Field 'invalid_field' not found in input schema",
      "code": "TRANS_003"
    }
  ]
}
```

---

### POST /api/transformations/{id}/clone

Clones an existing transformation.

**URL Parameters:**
- `id` (string, required): Transformation ID to clone

**Request Body:**
```json
{
  "name": "string (optional, defaults to 'Copy of {original name}')"
}
```

**Success Response (201):**
Returns the cloned transformation object with new ID.

**Error Responses:**
- `404 Not Found`: Original transformation not found
- `409 Conflict`: Clone name already exists

---

## Transformation Type Configurations

### Filter Transformation

Filters rows based on a condition.

**Config Structure:**
```json
{
  "operator": "equals|notEquals|greaterThan|lessThan|greaterThanOrEqual|lessThanOrEqual|contains|startsWith|endsWith|regex",
  "valueType": "string|number|boolean|date",
  "defaultValue": "string (value to compare against)"
}
```

**Example:**
```json
{
  "name": "High Value Filter",
  "type": "Filter",
  "description": "Filter rows where amount is greater than 1000",
  "config": {
    "operator": "greaterThan",
    "valueType": "number",
    "defaultValue": "1000"
  }
}
```

**Operators:**
- `equals` - Field value equals comparison value
- `notEquals` - Field value not equals comparison value
- `greaterThan` - Field value > comparison value
- `lessThan` - Field value < comparison value
- `greaterThanOrEqual` - Field value >= comparison value
- `lessThanOrEqual` - Field value <= comparison value
- `contains` - Field value contains comparison value (string only)
- `startsWith` - Field value starts with comparison value (string only)
- `endsWith` - Field value ends with comparison value (string only)
- `regex` - Field value matches regex pattern

---

### Map Transformation

Maps values from one set to another (lookup table).

**Config Structure:**
```json
{
  "mappings": [
    {
      "from": "string (source value)",
      "to": "string (mapped value)"
    }
  ],
  "defaultValue": "string (optional, value if no match found)",
  "caseSensitive": boolean
}
```

**Example:**
```json
{
  "name": "Status Code Mapper",
  "type": "Map",
  "description": "Maps single-letter status codes to full names",
  "config": {
    "mappings": [
      {"from": "P", "to": "Pending"},
      {"from": "C", "to": "Completed"},
      {"from": "X", "to": "Cancelled"},
      {"from": "R", "to": "Refunded"}
    ],
    "defaultValue": "Unknown",
    "caseSensitive": true
  }
}
```

---

### Trim Transformation

Removes leading and trailing whitespace from text fields.

**Config Structure:**
```json
{
  "fields": ["string"] (optional, array of field names to trim, if not in context)
}
```

**Example:**
```json
{
  "name": "Trim Text Fields",
  "type": "Trim",
  "description": "Remove leading and trailing whitespace",
  "config": {
    "fields": ["Status", "CustomerName"]
  }
}
```

**Note:** When used in a pipeline field mapping, the field is implicit. The `fields` array is only used for standalone transformations.

---

### Case Convert Transformation

Converts text case.

**Config Structure:**
```json
{
  "field": "string (field name)",
  "caseType": "uppercase|lowercase|titlecase"
}
```

**Example:**
```json
{
  "name": "Uppercase Product Codes",
  "type": "Case Convert",
  "description": "Convert product codes to uppercase for consistency",
  "config": {
    "field": "product_code",
    "caseType": "uppercase"
  }
}
```

**Case Types:**
- `uppercase` - Convert to UPPERCASE
- `lowercase` - Convert to lowercase
- `titlecase` - Convert To Title Case

---

### Substring Transformation

Extracts a portion of a string.

**Config Structure:**
```json
{
  "field": "string (field name)",
  "startIndex": number (0-based),
  "length": number (optional, if omitted extracts to end)
}
```

**Example:**
```json
{
  "name": "Extract Year from Date",
  "type": "Substring",
  "description": "Extract year portion from date fields",
  "config": {
    "field": "OrderDate",
    "startIndex": 0,
    "length": 4
  }
}
```

---

### Replace Transformation

Find and replace text patterns.

**Config Structure:**
```json
{
  "field": "string (field name)",
  "findPattern": "string (text or regex pattern to find)",
  "replaceWith": "string (replacement text)",
  "useRegex": boolean,
  "caseSensitive": boolean,
  "replaceAll": boolean (replace all occurrences or just first)
}
```

**Example:**
```json
{
  "name": "Replace Null Values",
  "type": "Replace",
  "description": "Replace null or empty values with default text",
  "config": {
    "field": "phone",
    "findPattern": "^$",
    "replaceWith": "N/A",
    "useRegex": true,
    "caseSensitive": false,
    "replaceAll": true
  }
}
```

---

### Script Transformation

Custom JavaScript or C# code for complex transformations.

**Config Structure:**
```json
{
  "scriptLanguage": "javascript|csharp",
  "script": "string (code)"
}
```

**JavaScript Example:**
```json
{
  "name": "Phone Number Formatter",
  "type": "Script",
  "description": "Formats phone numbers to (XXX) XXX-XXXX pattern",
  "config": {
    "scriptLanguage": "javascript",
    "script": "// Format phone numbers to (XXX) XXX-XXXX\nif (row.phone) {\n  let digits = row.phone.replace(/\\D/g, '');\n  if (digits.length === 10) {\n    row.phone = `(${digits.substring(0, 3)}) ${digits.substring(3, 6)}-${digits.substring(6)}`;\n  }\n}\nreturn row;"
  }
}
```

**C# Example:**
```json
{
  "name": "Data Validation (C#)",
  "type": "Script",
  "description": "Validates and cleans data using C#",
  "config": {
    "scriptLanguage": "csharp",
    "script": "// Validate and clean data\nif (row.ContainsKey(\"email\") && row[\"email\"] != null)\n{\n    var email = row[\"email\"].ToString();\n    if (!email.Contains(\"@\"))\n    {\n        row[\"email_valid\"] = false;\n    }\n    else\n    {\n        row[\"email_valid\"] = true;\n        row[\"email\"] = email.ToLower().Trim();\n    }\n}\nreturn row;"
  }
}
```

**Script Context:**
- **JavaScript**: `row` object contains current record fields
- **C#**: `row` Dictionary<string, object> contains current record fields
- **Return value**: Must return modified `row` object
- **Available libraries**: 
  - JavaScript: Standard ES6+ features, no external libraries
  - C#: System.*, System.Linq, System.Text.RegularExpressions

---

## Complete Examples

### Example 1: Email Normalization Pipeline

```json
[
  {
    "name": "Trim Email",
    "type": "Trim",
    "description": "Remove whitespace from email",
    "config": {
      "fields": ["email"]
    }
  },
  {
    "name": "Lowercase Email",
    "type": "Case Convert",
    "description": "Convert email to lowercase",
    "config": {
      "field": "email",
      "caseType": "lowercase"
    }
  },
  {
    "name": "Validate Email Format",
    "type": "Script",
    "description": "Check if email contains @ symbol",
    "config": {
      "scriptLanguage": "javascript",
      "script": "if (row.email && !row.email.includes('@')) {\n  row.email_valid = false;\n} else {\n  row.email_valid = true;\n}\nreturn row;"
    }
  }
]
```

### Example 2: Customer Data Enrichment

```json
{
  "name": "Phone Number Formatter",
  "type": "Script",
  "description": "Format 10-digit phone numbers",
  "config": {
    "scriptLanguage": "javascript",
    "script": "if (row.phone) {\n  const digits = row.phone.replace(/\\D/g, '');\n  if (digits.length === 10) {\n    row.phone = `(${digits.substring(0, 3)}) ${digits.substring(3, 6)}-${digits.substring(6)}`;\n    row.phone_formatted = true;\n  } else {\n    row.phone_formatted = false;\n  }\n}\nreturn row;"
  }
}
```

### Example 3: Complex Business Logic (C#)

```json
{
  "name": "Calculate Discount Tiers",
  "type": "Script",
  "description": "Apply tiered discounts based on order total",
  "config": {
    "scriptLanguage": "csharp",
    "script": "if (row.ContainsKey(\"quantity\") && row.ContainsKey(\"price\"))\n{\n    var quantity = Convert.ToDecimal(row[\"quantity\"]);\n    var price = Convert.ToDecimal(row[\"price\"]);\n    \n    var total = quantity * price;\n    if (total > 1000) {\n        total *= 0.9m;  // 10% discount\n        row[\"discount_tier\"] = \"Gold\";\n    } else if (total > 500) {\n        total *= 0.95m;  // 5% discount\n        row[\"discount_tier\"] = \"Silver\";\n    } else {\n        row[\"discount_tier\"] = \"Bronze\";\n    }\n    \n    row[\"total_with_discount\"] = Math.Round(total, 2);\n}\nreturn row;"
  }
}
```

---

## Validation Rules

### Name
- Required
- 3-100 characters
- Must be unique per tenant

### Type
- Required
- Must be one of: Filter, Map, Trim, Case Convert, Substring, Replace, Script

### Config
- Required
- Must match structure for selected type
- Field references must be valid (if applicable)

### Script Transformations
- Script language required
- Script code required
- Must be syntactically valid
- Must return `row` object
- Security: Scripts run in sandboxed environment with timeout limits

---

## Error Codes

- `TRANS_001`: Transformation not found
- `TRANS_002`: Invalid transformation type
- `TRANS_003`: Field not found in schema
- `TRANS_004`: Script compilation error
- `TRANS_005`: Script runtime error
- `TRANS_006`: Transformation in use by pipelines
- `TRANS_007`: Invalid configuration for transformation type
- `TRANS_008`: Map transformation mapping missing
- `TRANS_009`: Invalid operator for filter transformation

---

## Performance Considerations

### Transformation Order Matters
When chaining multiple transformations, order affects performance:
1. **Filter first**: Remove unnecessary rows early
2. **Trim/Clean early**: Normalize data before complex operations
3. **Scripts last**: Run expensive custom logic after built-in transformations

### Script Performance
- **JavaScript**: Generally faster for simple transformations
- **C#**: Better for complex logic, type safety, and large datasets
- **Timeout**: Scripts have 5-second timeout per row
- **Memory**: Scripts limited to 128MB memory per execution

### Caching
- Frequently-used transformations (Maps, Filters) are cached
- Script compilation results are cached
- Cache invalidated on transformation update
