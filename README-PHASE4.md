# Phase 4 Implementation - Pipeline Execution Engine

## Overview

Phase 4 implements the core execution engine with RabbitMQ message broker integration, enabling asynchronous pipeline execution with horizontal scalability.

## What's Been Implemented

### 1. Message Broker Integration (RabbitMQ)

**New Components:**
- `RabbitMqSettings` - Configuration for RabbitMQ connection and queues
- `IMessagePublisher` - Interface for publishing messages
- `RabbitMqPublisher` - RabbitMQ implementation with durable queues and DLX
- `ExecutionTask` - Message contract for pipeline execution tasks

**Features:**
- Durable queues with persistent messages
- Dead Letter Exchange (DLX) for failed messages
- Automatic connection recovery
- Prefetch count configuration for worker load control
- Retry mechanism with max attempts

### 2. Pipeline Orchestrator

**New Component:**
- `PipelineOrchestrator` - Coordinates Extract → Transform → Load flow

**Features:**
- Streams data in batches (default 1,000 rows)
- Creates `ExecutionBatch` records for fine-grained tracking
- Updates execution progress in real-time
- Handles batch-level errors with fail-safe mode
- Logs all execution events to `execution_logs` table
- Supports cancellation via CancellationToken

### 3. Worker Service

**New Project:** `MultiTenantETL.Worker` (.NET 8)

**Features:**
- Background service that consumes execution tasks from RabbitMQ
- Manages multiple concurrent executions (configurable via PrefetchCount)
- Handles cancellation requests
- Automatic retry with exponential backoff
- Graceful shutdown with execution cleanup

### 4. Updated Services

**ExecutionService:**
- Now publishes `ExecutionTask` to RabbitMQ instead of TODO comment
- Publishes cancellation requests to dedicated queue

## Architecture

```
API (ExecutionService)
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

## Configuration

### RabbitMQ Settings (appsettings.json)

```json
{
  "RabbitMq": {
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest",
    "VirtualHost": "/",
    "ExecutionQueueName": "pipeline-executions",
    "CancellationQueueName": "pipeline-cancellations",
    "DeadLetterExchange": "pipeline-dlx",
    "PrefetchCount": 1,
    "MaxRetryAttempts": 5
  }
}
```

### Connection String

Both API and Worker need the same PostgreSQL connection string:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=multitenant_etl;Username=postgres;Password=postgres"
  }
}
```

## Running Locally

### 1. Start Infrastructure (Docker Compose)

```bash
docker-compose up -d
```

This starts:
- PostgreSQL on port 5432
- RabbitMQ on port 5672 (AMQP) and 15672 (Management UI)

### 2. Run Database Migrations

```bash
cd src/MultiTenantETL.API
dotnet ef database update --project ../MultiTenantETL.Infrastructure
```

### 3. Start the API

```bash
cd src/MultiTenantETL.API
dotnet run
```

API will be available at:
- HTTPS: https://localhost:7288
- HTTP: http://localhost:5244

### 4. Start the Worker

In a separate terminal:

```bash
cd src/MultiTenantETL.Worker
dotnet run
```

Worker will connect to RabbitMQ and start listening for execution tasks.

### 5. Access RabbitMQ Management UI

Open browser: http://localhost:15672
- Username: `guest`
- Password: `guest`

You can monitor queues, messages, and connections here.

## Testing the Execution Engine

### 1. Create a Pipeline (via API)

```bash
POST /api/pipelines
{
  "name": "Test Pipeline",
  "sourceConnectorId": "<source-connector-guid>",
  "destinationConnectorId": "<destination-connector-guid>",
  "fieldMappingsJson": "[]",
  "isActive": true
}
```

### 2. Start Execution

```bash
POST /api/pipelines/{pipelineId}/execute
```

This will:
1. Create a `PipelineExecution` record (status: Queued)
2. Publish `ExecutionTask` to RabbitMQ
3. Worker picks up the task
4. `PipelineOrchestrator` executes the pipeline
5. Status updates to Running → Completed/Failed

### 3. Monitor Execution

```bash
GET /api/executions/{executionId}
```

Returns:
- Status, progress, records processed
- Batch count
- Logs from `execution_logs` table

### 4. Cancel Execution

```bash
POST /api/executions/{executionId}/cancel
```

This publishes a cancellation request to RabbitMQ, and the worker cancels the running execution.

## Queues and Exchanges

### Queues Created

1. **pipeline-executions** (durable)
   - Receives execution tasks
   - Has DLX configured for failed messages
   - Worker consumes from this queue

2. **pipeline-cancellations** (durable)
   - Receives cancellation requests
   - Worker monitors this queue

3. **pipeline-dlx** (Dead Letter Exchange)
   - Receives messages that failed max retry attempts
   - Manual inspection required

## Monitoring

### Database Tables

- `pipeline_executions` - Execution metadata and progress
- `execution_batches` - Per-batch tracking for resumability
- `execution_logs` - Detailed logs (partitioned for 365-day retention)

### RabbitMQ Metrics

- Queue depth (should be low if workers are keeping up)
- Message rate (in/out)
- Consumer count (number of active workers)
- Unacked messages (executions in progress)

### Logs

Worker logs show:
- Connection status
- Task received/completed
- Batch progress
- Errors and retries

## Scaling

### Horizontal Scaling

Run multiple worker instances:

```bash
# Terminal 1
cd src/MultiTenantETL.Worker
dotnet run

# Terminal 2
cd src/MultiTenantETL.Worker
dotnet run

# Terminal 3
cd src/MultiTenantETL.Worker
dotnet run
```

RabbitMQ will distribute tasks across workers using round-robin.

### Vertical Scaling

Increase `PrefetchCount` in RabbitMQ settings to allow each worker to process multiple executions concurrently:

```json
{
  "RabbitMq": {
    "PrefetchCount": 5
  }
}
```

## Error Handling

### Transient Errors

- Worker retries up to `MaxRetryAttempts` (default: 5)
- Exponential backoff between retries
- Message requeued automatically

### Permanent Errors

- After max retries, message sent to DLX
- Execution marked as Failed in database
- Manual intervention required

### Batch Errors

- Individual batch failures don't stop the pipeline
- Failed batches logged and counted
- Pipeline continues with next batch (fail-safe mode)

## Next Steps (Phase 5+)

- [ ] Implement scheduler service for cron-based execution
- [ ] Add Azure Key Vault integration for secrets
- [ ] Implement transformation application in orchestrator
- [ ] Add Prometheus metrics export
- [ ] Add OpenTelemetry tracing
- [ ] Implement script sandboxing
- [ ] Add AKS deployment manifests

## Troubleshooting

### Worker not receiving messages

1. Check RabbitMQ connection in worker logs
2. Verify queue exists in RabbitMQ Management UI
3. Check firewall/network connectivity

### Executions stuck in Queued

1. Verify worker is running
2. Check RabbitMQ queue has consumers
3. Check worker logs for errors

### Database connection errors

1. Verify PostgreSQL is running: `docker ps`
2. Check connection string in appsettings.json
3. Verify migrations applied: `dotnet ef database update`

## Dependencies Added

### Infrastructure Project
- `RabbitMQ.Client` 6.8.1

### Worker Project
- `Microsoft.Extensions.Hosting` 8.0.1
- `Microsoft.EntityFrameworkCore.Design` 8.0.22
- `Npgsql.EntityFrameworkCore.PostgreSQL` 8.0.11
- `RabbitMQ.Client` 6.8.1
- Project references to Application and Infrastructure

## Files Created/Modified

### New Files
- `src/MultiTenantETL.Application/Messaging/IMessagePublisher.cs`
- `src/MultiTenantETL.Application/Messaging/ExecutionTask.cs`
- `src/MultiTenantETL.Application/Orchestration/IPipelineOrchestrator.cs`
- `src/MultiTenantETL.Infrastructure/Configuration/RabbitMqSettings.cs`
- `src/MultiTenantETL.Infrastructure/Messaging/RabbitMqPublisher.cs`
- `src/MultiTenantETL.Infrastructure/Orchestration/PipelineOrchestrator.cs`
- `src/MultiTenantETL.Worker/Worker.cs` (updated)
- `src/MultiTenantETL.Worker/Program.cs` (updated)
- `docker-compose.yml`

### Modified Files
- `src/MultiTenantETL.Infrastructure/Services/ExecutionService.cs`
- `src/MultiTenantETL.Infrastructure/MultiTenantETL.Infrastructure.csproj`
- `src/MultiTenantETL.Worker/MultiTenantETL.Worker.csproj`
- `src/MultiTenantETL.Worker/appsettings.json`
- `src/MultiTenantETL.API/appsettings.json`
- `src/MultiTenantETL.API/Program.cs`
