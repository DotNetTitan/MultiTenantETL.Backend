# Metadata API Specification

**Resource:** Metadata / Configuration  
**Base Path:** `/api/metadata`  
**Version:** 2.0  
**Last Updated:** 2025-11-19

---

## Overview

The Metadata API provides centralized configuration data for dropdowns, options, validation rules, and other application-wide settings. This enables dynamic configuration without code deployments and supports multi-language translations.

---

## Endpoints

### GET /api/metadata/all

Gets all metadata at once (recommended for initial app load).

**Success Response (200):**
```json
{
  "connectorConfig": {
    // See below
  },
  "transformationTypes": [
    // See below
  ],
  "dataTypes": [
    // See below
  ],
  "scheduleFrequencies": [
    // See below
  ],
  "daysOfWeek": [
    // See below
  ]
}
```

---

### GET /api/metadata/connector-config

Gets connector configuration metadata.

**Success Response (200):**
```json
{
  "types": [
    {
      "value": "Database",
      "labelKey": "connectors.database",
      "icon": "mdi-database"
    },
    {
      "value": "API",
      "labelKey": "connectors.api",
      "icon": "mdi-api"
    },
    {
      "value": "File",
      "labelKey": "connectors.file",
      "icon": "mdi-file-document"
    }
  ],
  "providers": {
    "Database": ["SQL Server", "PostgreSQL", "MySQL", "Oracle", "SQLite"],
    "API": ["REST", "GraphQL", "SOAP"],
    "File": ["Local", "FTP", "SFTP", "S3", "Azure Blob", "Google Cloud Storage"]
  },
  "directions": [
    {
      "value": "source",
      "labelKey": "connectors.sourceOnly",
      "icon": "mdi-export"
    },
    {
      "value": "destination",
      "labelKey": "connectors.destinationOnly",
      "icon": "mdi-import"
    },
    {
      "value": "both",
      "labelKey": "connectors.both",
      "icon": "mdi-swap-horizontal"
    }
  ],
  "authTypes": [
    {"value": "None", "labelKey": "connectors.authNone"},
    {"value": "Basic", "labelKey": "connectors.authBasic"},
    {"value": "Bearer", "labelKey": "connectors.authBearer"},
    {"value": "OAuth2", "labelKey": "connectors.authOAuth2"},
    {"value": "API Key", "labelKey": "connectors.authApiKey"}
  ],
  "fileFormats": [
    {"value": "CSV", "labelKey": "common.csv", "extension": ".csv"},
    {"value": "JSON", "labelKey": "common.json", "extension": ".json"},
    {"value": "Excel", "labelKey": "common.excel", "extension": ".xlsx"},
    {"value": "XML", "labelKey": "common.xml", "extension": ".xml"},
    {"value": "Parquet", "labelKey": "common.parquet", "extension": ".parquet"}
  ],
  "writeOperations": [
    {
      "value": "INSERT",
      "labelKey": "connectors.writeOperationInsert",
      "descriptionKey": "connectors.insertDescription",
      "requiresPrimaryKey": false
    },
    {
      "value": "UPDATE",
      "labelKey": "connectors.writeOperationUpdate",
      "descriptionKey": "connectors.updateDescription",
      "requiresPrimaryKey": true
    },
    {
      "value": "UPSERT",
      "labelKey": "connectors.writeOperationUpsert",
      "descriptionKey": "connectors.upsertDescription",
      "requiresPrimaryKey": true
    },
    {
      "value": "BULK_INSERT",
      "labelKey": "connectors.writeOperationBulkInsert",
      "descriptionKey": "connectors.bulkInsertDescription",
      "requiresPrimaryKey": false
    }
  ],
  "httpMethods": [
    {"value": "GET", "labelKey": "common.httpGet", "color": "success"},
    {"value": "POST", "labelKey": "common.httpPost", "color": "primary"},
    {"value": "PUT", "labelKey": "common.httpPut", "color": "warning"},
    {"value": "PATCH", "labelKey": "common.httpPatch", "color": "info"},
    {"value": "DELETE", "labelKey": "common.httpDelete", "color": "error"}
  ]
}
```

---

### GET /api/metadata/transformation-types

Gets transformation type configuration.

**Success Response (200):**
```json
[
  {
    "value": "Filter",
    "labelKey": "transformations.filter",
    "icon": "mdi-filter",
    "categoryKey": "transformations.categoryDataQuality",
    "descriptionKey": "transformations.filterDescription"
  },
  {
    "value": "Map",
    "labelKey": "transformations.map",
    "icon": "mdi-map",
    "categoryKey": "transformations.categoryTransformation",
    "descriptionKey": "transformations.mapDescription"
  },
  {
    "value": "Script",
    "labelKey": "transformations.script",
    "icon": "mdi-code-braces",
    "categoryKey": "transformations.categoryCustom",
    "descriptionKey": "transformations.scriptDescription"
  },
  {
    "value": "Trim",
    "labelKey": "transformations.trim",
    "icon": "mdi-content-cut",
    "categoryKey": "transformations.categoryText",
    "descriptionKey": "transformations.trimDescription"
  },
  {
    "value": "Case Convert",
    "labelKey": "transformations.case",
    "icon": "mdi-format-letter-case",
    "categoryKey": "transformations.categoryText",
    "descriptionKey": "transformations.caseDescription"
  },
  {
    "value": "Substring",
    "labelKey": "transformations.substring",
    "icon": "mdi-text-box-outline",
    "categoryKey": "transformations.categoryText",
    "descriptionKey": "transformations.substringDescription"
  },
  {
    "value": "Replace",
    "labelKey": "transformations.replace",
    "icon": "mdi-find-replace",
    "categoryKey": "transformations.categoryText",
    "descriptionKey": "transformations.replaceDescription"
  }
]
```

---

### GET /api/metadata/data-types

Gets supported data types for schemas.

**Success Response (200):**
```json
[
  {"value": "string", "labelKey": "schema.dataTypes.string", "icon": "mdi-format-text"},
  {"value": "integer", "labelKey": "schema.dataTypes.integer", "icon": "mdi-numeric"},
  {"value": "bigInteger", "labelKey": "schema.dataTypes.bigInteger", "icon": "mdi-numeric"},
  {"value": "decimal", "labelKey": "schema.dataTypes.decimal", "icon": "mdi-decimal"},
  {"value": "boolean", "labelKey": "schema.dataTypes.boolean", "icon": "mdi-checkbox-marked"},
  {"value": "date", "labelKey": "schema.dataTypes.date", "icon": "mdi-calendar"},
  {"value": "dateTime", "labelKey": "schema.dataTypes.dateTime", "icon": "mdi-calendar-clock"},
  {"value": "timestamp", "labelKey": "schema.dataTypes.timestamp", "icon": "mdi-clock-outline"},
  {"value": "json", "labelKey": "schema.dataTypes.json", "icon": "mdi-code-json"},
  {"value": "textLong", "labelKey": "schema.dataTypes.textLong", "icon": "mdi-text-long"}
]
```

---

### GET /api/metadata/schedule-frequencies

Gets schedule frequency options.

**Success Response (200):**
```json
[
  {"value": "daily", "labelKey": "pipelines.daily", "icon": "mdi-calendar-today"},
  {"value": "weekly", "labelKey": "pipelines.weekly", "icon": "mdi-calendar-week"},
  {"value": "monthly", "labelKey": "pipelines.monthly", "icon": "mdi-calendar-month"},
  {"value": "custom", "labelKey": "pipelines.custom", "icon": "mdi-cog"}
]
```

---

### GET /api/metadata/days-of-week

Gets days of week configuration.

**Success Response (200):**
```json
[
  {"value": "monday", "labelKey": "pipelines.monday", "shortKey": "common.mon"},
  {"value": "tuesday", "labelKey": "pipelines.tuesday", "shortKey": "common.tue"},
  {"value": "wednesday", "labelKey": "pipelines.wednesday", "shortKey": "common.wed"},
  {"value": "thursday", "labelKey": "pipelines.thursday", "shortKey": "common.thu"},
  {"value": "friday", "labelKey": "pipelines.friday", "shortKey": "common.fri"},
  {"value": "saturday", "labelKey": "pipelines.saturday", "shortKey": "common.sat"},
  {"value": "sunday", "labelKey": "pipelines.sunday", "shortKey": "common.sun"}
]
```

---

## Translation Keys

All `labelKey`, `descriptionKey`, `categoryKey`, and `shortKey` values reference translation keys in the i18n system. The frontend uses these keys to look up localized strings.

### Example Translation Structure

```json
{
  "connectors": {
    "database": "Database",
    "api": "API",
    "file": "File",
    "authNone": "No Authentication",
    "authBasic": "Basic Authentication",
    "authBearer": "Bearer Token"
  },
  "transformations": {
    "filter": "Filter",
    "map": "Map Values",
    "script": "Custom Script",
    "categoryDataQuality": "Data Quality",
    "categoryTransformation": "Transformation",
    "categoryText": "Text Manipulation",
    "filterDescription": "Filter rows based on conditions"
  },
  "common": {
    "csv": "CSV",
    "json": "JSON",
    "mon": "Mon",
    "tue": "Tue"
  },
  "pipelines": {
    "daily": "Daily",
    "weekly": "Weekly",
    "monday": "Monday"
  }
}
```

---

## Caching

**Recommendation**: Cache metadata on the frontend for 1 hour to reduce API calls.

**Cache Control Headers:**
```
Cache-Control: public, max-age=3600
ETag: "metadata-v1-20251119"
```

**Cache Invalidation:**
- Frontend should include `If-None-Match` header with ETag
- Backend returns `304 Not Modified` if metadata unchanged
- Clear cache when receiving `200` with new ETag

---

## Future Endpoints
(To be implemented)

- `POST /api/metadata/validation-rules` - Custom validation rules
- `GET /api/metadata/timezones` - List of supported timezones
- `GET /api/metadata/countries` - Countries for address fields
- `GET /api/metadata/currencies` - Currency codes and symbols
