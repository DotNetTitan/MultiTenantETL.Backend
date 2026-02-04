# Mock Data

This folder contains all mock data used throughout the application for development and testing.

## Files

- **connectors.js** - Mock data for connectors (18 connectors covering all types, providers, and formats)
- **pipelines.js** - Mock data for pipelines and executions (5 pipelines with different statuses and schedules)
- **users.js** - Mock data for users (5 users with different roles)
- **tenants.js** - Mock data for tenants (3 tenants with active/inactive status)
- **index.js** - Central export file for all mock data

## Usage

Import mock data in service files:

```javascript
import { mockConnectors } from '@/mocks/connectors'
import { mockPipelines, mockExecutions } from '@/mocks/pipelines'
import { mockUsers } from '@/mocks/users'
import { mockTenants } from '@/mocks/tenants'
```

Or import from the central index:

```javascript
import { mockConnectors, mockPipelines } from '@/mocks'
```

## Services Using Mock Data

- **connectorService.js** - Uses `mockConnectors`
- **pipelineService.js** - Uses `mockPipelines` and `mockExecutions`
- **userService.js** - Uses `mockUsers`
- **tenantService.js** - Uses `mockTenants`
- **dashboardService.js** - Uses `mockPipelines`, `mockExecutions`, and `mockConnectors` to calculate stats

## Coverage

### Connectors (18 total)
- **Database**: SQL Server, PostgreSQL, MySQL, Oracle
- **File Storage**: Local, FTP, SFTP, S3, Azure Blob, Google Cloud Storage
- **File Formats**: CSV, JSON, Excel, XML, Parquet
- **API Types**: REST, GraphQL, SOAP
- **Auth Types**: None, Basic, Bearer, OAuth2, API Key
- **Write Operations**: INSERT, UPDATE, UPSERT, BULK_INSERT

### Pipelines (5 total)
- **Statuses**: Idle, Running, Failed
- **Schedules**: Daily, Weekly, Monthly, Manual
- **Field Mappings**: Complete with inline transformations (3-5 mappings per pipeline)
- **Timezones**: UTC, America/New_York, America/Los_Angeles
- **Executions**: 3 executions with detailed logs (1 Completed, 1 Running, 1 Failed)

### Users (5 total)
- **Roles**: Admin, Manager, User
- **Status**: Active, Inactive

### Tenants (3 total)
- **Status**: Active, Inactive

## Pipeline Details

### Pipeline 1: Sales Data ETL
- **Source**: SQL Server - Sales → **Destination**: Data Warehouse
- **Field Mappings**: 4 (OrderId→SaleId, CustomerId→ProductId, OrderDate→SaleDate, TotalAmount→Amount)
- **Inline Transformations**: Equals Filter on TotalAmount
- **Schedule**: Daily at 02:00 UTC

### Pipeline 2: Customer Import
- **Source**: SFTP - Customer Files → **Destination**: Customer Database
- **Field Mappings**: 5 (customer_id, first_name, last_name, email, phone)
- **Inline Transformations**: Trim on names, Case Convert on email
- **Schedule**: Weekly at 04:30 EST

### Pipeline 3: Product Sync
- **Source**: ERP API → **Destination**: E-commerce Platform
- **Field Mappings**: 4 (id→product_id, product_code→sku, quantity→stock_quantity, price→unit_price)
- **Inline Transformations**: Uppercase Product Codes
- **Schedule**: Manual (not scheduled)
- **Status**: Currently Running

### Pipeline 4: Analytics Export
- **Source**: Analytics DB → **Destination**: Reporting System
- **Field Mappings**: 4 (metric_id→id, metric_name→name, metric_value→value, recorded_at→timestamp)
- **Inline Transformations**: None
- **Schedule**: Daily at 01:00 UTC
- **Status**: Failed (connection timeout)

### Pipeline 5: Monthly Financial Report
- **Source**: Oracle ERP Database → **Destination**: AWS S3 Bucket
- **Field Mappings**: 3 (ItemId→item_id, ItemName→item_name, Quantity→quantity)
- **Inline Transformations**: Substring on ItemName
- **Schedule**: Monthly on 1st at 00:00 PST

## Maintenance

When adding new mock data:
1. Add the data to the appropriate file
2. Ensure IDs are unique and consistent across related entities
3. Include all required fields for the entity type
4. For pipelines, ensure field mappings reference actual source/destination fields
5. For executions, include pipelineName and detailed logs
6. Update this README if adding new categories or types
