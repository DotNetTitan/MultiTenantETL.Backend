# Quick Start - Phase 4 Execution Engine

## Prerequisites

- .NET 8 SDK
- Docker Desktop (for PostgreSQL and RabbitMQ)

## Step 1: Start Infrastructure

```bash
docker-compose up -d
```

This starts:
- **PostgreSQL** on port 5432
- **RabbitMQ** on port 5672 (AMQP) and 15672 (Management UI)

Verify services are running:
```bash
docker ps
```

## Step 2: Apply Database Migrations

```bash
cd src/MultiTenantETL.API
dotnet ef database update --project ../MultiTenantETL.Infrastructure
```

## Step 3: Configure User Secrets (Optional)

If you want to use real email service or custom settings:

```bash
cd src/MultiTenantETL.API
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=multitenant_etl;Username=postgres;Password=postgres"
dotnet user-secrets set "Seeding:AdminPassword" "Admin@123"
dotnet user-secrets set "Encryption:Key" "your-32-byte-base64-key"
```

## Step 4: Start the API

```bash
cd src/MultiTenantETL.API
dotnet run
```

API will be available at:
- HTTPS: https://localhost:7288
- HTTP: http://localhost:5244
- Swagger: https://localhost:7288/swagger

## Step 5: Start the Worker (New Terminal)

```bash
cd src/MultiTenantETL.Worker
dotnet run
```

You should see:
```
info: MultiTenantETL.Worker.Worker[0]
      Pipeline Worker starting...
info: MultiTenantETL.Worker.Worker[0]
      Connected to RabbitMQ at localhost:5672
info: MultiTenantETL.Worker.Worker[0]
      Worker is now listening for execution tasks...
```

## Step 6: Test the Execution Engine

### 6.1 Login to get access token

```bash
POST https://localhost:7288/connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=password&username=admin@example.com&password=Admin@123&scope=openid email profile roles api offline_access
```

### 6.2 Create a Source Connector (CSV example)

```bash
POST https://localhost:7288/api/connectors
Authorization: Bearer {your-token}
Content-Type: application/json

{
  "name": "Test CSV Source",
  "type": "File",
  "provider": "CSV",
  "direction": "source",
  "configJson": "{\"filePath\":\"C:\\\\temp\\\\test.csv\",\"hasHeaders\":true,\"delimiter\":\",\"}"
}
```

### 6.3 Create a Destination Connector (PostgreSQL example)

```bash
POST https://localhost:7288/api/connectors
Authorization: Bearer {your-token}
Content-Type: application/json

{
  "name": "Test PostgreSQL Destination",
  "type": "Database",
  "provider": "PostgreSQL",
  "direction": "destination",
  "configJson": "{\"host\":\"localhost\",\"port\":5432,\"database\":\"multitenant_etl\",\"username\":\"postgres\",\"password\":\"postgres\",\"tableName\":\"test_output\"}"
}
```

### 6.4 Create a Pipeline

```bash
POST https://localhost:7288/api/pipelines
Authorization: Bearer {your-token}
Content-Type: application/json

{
  "name": "CSV to PostgreSQL Pipeline",
  "description": "Test pipeline for Phase 4",
  "sourceConnectorId": "{source-connector-id}",
  "destinationConnectorId": "{destination-connector-id}",
  "fieldMappingsJson": "[]",
  "isActive": true
}
```

### 6.5 Execute the Pipeline

```bash
POST https://localhost:7288/api/pipelines/{pipeline-id}/execute
Authorization: Bearer {your-token}
```

Response:
```json
{
  "id": "execution-guid",
  "pipelineId": "pipeline-guid",
  "status": "Queued",
  "startTime": "2024-11-29T...",
  "recordsProcessed": 0,
  "progressPercent": 0
}
```

### 6.6 Monitor Execution

```bash
GET https://localhost:7288/api/executions/{execution-id}
Authorization: Bearer {your-token}
```

Watch the status change: `Queued` → `Running` → `Completed`

### 6.7 View Logs

The execution response includes logs:
```json
{
  "logs": [
    {
      "timestamp": "2024-11-29T...",
      "level": "Info",
      "source": "System",
      "message": "Pipeline execution queued"
    },
    {
      "timestamp": "2024-11-29T...",
      "level": "Info",
      "source": "System",
      "message": "Pipeline execution started"
    }
  ]
}
```

## Step 7: Monitor RabbitMQ

Open browser: http://localhost:15672
- Username: `guest`
- Password: `guest`

Navigate to **Queues** tab to see:
- `pipeline-executions` queue
- `pipeline-cancellations` queue
- Message rates and consumer count

## Troubleshooting

### Worker not connecting to RabbitMQ

Check RabbitMQ is running:
```bash
docker ps | grep rabbitmq
```

Check worker logs for connection errors.

### Execution stuck in "Queued"

1. Verify worker is running and connected
2. Check RabbitMQ Management UI - queue should have 1 consumer
3. Check worker logs for errors

### Database connection errors

Verify PostgreSQL is running:
```bash
docker ps | grep postgres
```

Test connection:
```bash
psql -h localhost -U postgres -d multitenant_etl
```

### Pipeline execution fails

Check execution logs via API:
```bash
GET /api/executions/{execution-id}
```

Check worker console output for detailed error messages.

## Stopping Services

### Stop Worker
Press `Ctrl+C` in worker terminal

### Stop API
Press `Ctrl+C` in API terminal

### Stop Infrastructure
```bash
docker-compose down
```

To remove volumes (data will be lost):
```bash
docker-compose down -v
```

## Next Steps

- Create more complex pipelines with transformations
- Test cancellation: `POST /api/executions/{id}/cancel`
- Scale workers: run multiple worker instances
- Monitor execution statistics: `GET /api/executions/stats`
- Explore batch tracking: check `execution_batches` table

## Architecture Recap

```
User → API (ExecutionService)
         ↓ Publish ExecutionTask
       RabbitMQ (pipeline-executions queue)
         ↓ Consume
       Worker (Background Service)
         ↓ Execute
       PipelineOrchestrator
         ↓ Stream batches
       DataReader → Transform → DataWriter
         ↓ Update
       Database (execution_logs, execution_batches, pipeline_executions)
```

## Key Features Implemented

✅ RabbitMQ message broker integration  
✅ Asynchronous pipeline execution  
✅ Worker service with horizontal scalability  
✅ Batch-oriented streaming (1,000 rows/batch)  
✅ Real-time progress tracking  
✅ Execution logs and batch tracking  
✅ Cancellation support  
✅ Retry mechanism with DLX  
✅ Docker Compose for local development  

## Performance Tips

- Adjust `PrefetchCount` in RabbitMQ settings for concurrent executions
- Tune batch size in `ReadOptions` (default: 1,000 rows)
- Run multiple worker instances for horizontal scaling
- Monitor queue depth in RabbitMQ Management UI
