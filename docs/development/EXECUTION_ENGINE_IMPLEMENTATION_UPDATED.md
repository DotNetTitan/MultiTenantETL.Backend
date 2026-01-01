# Pipeline Execution Engine - Implementation Plan (Production, Azure + Azure Service Bus)

> **Note:** This document was originally written for RabbitMQ. The implementation has been updated to use Azure Service Bus instead. References to RabbitMQ in this document should be interpreted as Azure Service Bus.

## Overview

This document is the production-ready implementation plan for the Pipeline Execution Engine and related features in the MultiTenant ETL platform. It is tailored for a high-scale deployment (millions of users, very large datasets) on Azure using Azure Service Bus as the message broker. It includes architecture, schema changes, streaming/batching guidance, security (secrets, sandboxing), operational recommendations (Docker, AKS), observability, and an updated checklist & timeline.

Key decisions applied in this version
- Broker: Azure Service Bus (durable, task-queue semantics)
- Cloud: Azure (AKS, ACR, Azure Blob Storage, Azure Database for PostgreSQL, Azure Key Vault)
- Delivery semantics: at-least-once delivery with idempotency/upsert strategies (recommended for production; exactly-once is complex and sink-dependent)
- Execution logs retention: 365 days (default; configurable per tenant)

---

## High-level Architecture (production)

- API: REST API (management / control)
- Orchestrator: lightweight coordinator that records executions and publishes ExecutionTask messages to RabbitMQ
- RabbitMQ: durable task queue for distributing work to worker pool
- Worker Pool: stateless worker containers running in AKS that perform Extract → Transform → Load in batched streaming mode
- Storage:
  - Metadata DB: Azure Database for PostgreSQL (recommended)
  - Object Storage: Azure Blob Storage for large artifacts and archival
  - Logs & Metrics: Azure Monitor / Log Analytics (or ELK / OpenSearch if preferred)
  - Secrets: Azure Key Vault
- Infra: Container images in Azure Container Registry (ACR), runtime in Azure Kubernetes Service (AKS)
- Observability: Prometheus + Grafana (or Azure Monitor), OpenTelemetry traces, centralized logging

Logical flow
API -> Orchestrator -> RabbitMQ -> Worker(s) -> Metadata DB / Blob / Log store

---

## Goals & Non-Goals

Goals
- Reliable, multi-tenant isolated pipeline executions
- Scalable horizontal workers and autoscaling (AKS + HPA)
- Memory-bounded streaming using batches
- Secure secret management and sandboxed script execution
- Full observability (metrics, traces, logs)
- Operational readiness on Azure

Non-goals
- Implement exactly-once semantics across heterogeneous sinks (not recommended by default)
- Replace RabbitMQ with Azure Service Bus (user-specified RabbitMQ)

---

## Phase 1 — Core Execution Infrastructure (Revised)

### New / Revised DB Entities

1. pipeline_executions (revised)
- id (uuid)
- pipeline_id (uuid)
- tenant_id (uuid)
- status (enum) — Queued / Running / Completed / Failed / Cancelled
- start_time (timestamptz / DateTimeOffset UTC)
- end_time (timestamptz)
- duration (interval)
- records_processed (bigint)
- records_succeeded (bigint)
- records_failed (bigint)
- progress_percent (decimal)
- error_message (text) — short summary
- summary_json (jsonb) — compact summary (not full logs)
- triggered_by (string)
- triggered_by_user_id (uuid)
- batch_count (int)
- created_at (timestamptz)

Notes:
- Use bigint for counters to support very large datasets.
- Use timestamptz (DateTimeOffset) for times.

2. execution_logs (new)
- id (uuid)
- execution_id (uuid) FK
- timestamp (timestamptz)
- level (string) Info/Warning/Error/Debug
- source (string)
- message (text)
- details (jsonb / text)
- batch_id (uuid) nullable
- tenant_id (uuid)
- created_at (timestamptz)

Notes:
- Use partitions (by month) or time-based retention TTL to support 365-day retention and avoid unbounded growth.
- Archive old partitions to Blob Storage if required.

3. execution_batches (recommended)
- id (uuid)
- execution_id (uuid)
- batch_index (int)
- rows_count (int)
- rows_succeeded (int)
- rows_failed (int)
- status (Queued / Processing / Completed / Failed)
- started_at, ended_at
- checkpoint_info (jsonb) - for resumability

Benefits:
- Fine-grained checkpointing & resumption
- Easier per-batch retries and observability

### DTOs & Interfaces
- ExecutionResponse, ExecutionListResponse, PagedExecutionResponse, ExecutionSearchRequest, ExecutionLogDto, ExecutionStatsDto.
- IExecutionService.StartExecutionAsync shall create execution row and publish to RabbitMQ.

### Enqueueing & Orchestrator
- Orchestrator writes PipelineExecution (status Queued) and publishes ExecutionTask to RabbitMQ with:
  - execution_id
  - pipeline_id
  - tenant_id
  - connector secret references (secret ids, not actual secrets)
  - execution options (dry-run, batch size, parallelism)
- Worker consumes ExecutionTask, fetches secrets from Azure Key Vault, marks execution Running, and begins streaming batches.

---

## Phase 2 — Data Readers & Writers (Streaming-first)

Design principle: streaming-first, batch-oriented processing to cap memory usage.

### IDataReader (production)
- IAsyncEnumerable<ReadBatch> ReadAsync(Connector connector, ReadOptions options, CancellationToken ct)
- Task<bool> TestConnectionAsync(Connector connector, CancellationToken ct)
- Task<SchemaDetectionResult> DetectSchemaAsync(Connector connector, CancellationToken ct)

ReadBatch:
- batch_id (uuid)
- rows (List<IDictionary<string, object>> or IList<ExpandoObject>)
- row_count
- metadata (json)

Notes:
- Readers should support server-side cursors (DB) or incremental pagination (APIs) and stream data via IAsyncEnumerable to avoid large memory usage.
- For large-listed files (CSV/XLSX), stream read from Blob Storage without fully materializing.

### IDataWriter (production)
- Task<DataWriteResult> WriteBatchAsync(Connector connector, ReadBatch batch, WriteOptions options, CancellationToken ct)

DataWriteResult:
- batch_id
- rows_written
- rows_failed
- errors (list)
- upsert_keys (optional)

Writer expectations:
- Prefer bulk APIs (SqlBulkCopy, Npgsql COPY) for throughput.
- Offer upsert/merge semantics where possible to enable idempotency.
- Provide clear error granularities (per-row failures).

### Implementations (priority)
- SQL Server, PostgreSQL (Azure DB), MySQL (if needed): streaming readers & bulk writers
- CSV, Excel, JSON, REST API: streaming readers/writers
- For REST-based sinks, support batch POSTs and backoff/retry

### Schema Detection
- SQL: information_schema or sample query
- CSV/Excel: sample N rows
- JSON: sample parsing, infer structure
- API: sample response or OpenAPI if available
- Persist schema version and fingerprint.

---

## Phase 3 — Transformation Engine (Batch-oriented & Safe)

### Interface (batch)
- Task<TransformationResult> ProcessBatchAsync(ReadBatch batch, Transformation transformation, CancellationToken ct)

TransformationResult:
- batch_id
- transformed_rows
- rows_processed
- rows_filtered
- warnings

### Processors
- FilterProcessor (compiled predicates or expression language)
- MapProcessor (field mapping)
- TrimProcessor, CaseConvertProcessor, SubstringProcessor, ReplaceProcessor
- ScriptProcessor:
  - Production approach: containerized sandbox execution per-batch or WASM runtime; do not run untrusted scripts in-process
  - Provide script execution service (ephemeral container/job invoked with the batch payload or a subset) with resource/time caps
  - If in-process scripting required only for trusted admin scripts, use Jint/Roslyn with strict allowlists, timeouts, and monitoring

### Orchestrator
- TransformationOrchestrator applies transformations per-batch, optionally enabling limited intra-batch parallelism.
- Support pipeline policy: fail-fast vs continue-on-error.

---

## Phase 4 — Pipeline Worker & Broker (RabbitMQ)

### RabbitMQ usage pattern
- Durable exchanges & queues; persistent messages
- Use separate queues per worker-type or per-tenant-shard (operational decision)
- Configure prefetch/prefetch count to limit memory use per worker
- Use dead-letter exchanges for failed messages
- Use acknowledgement model: acknowledge only after safe checkpoint (after write and batch metadata persisted)

### Worker responsibilities
- Consumer receives ExecutionTask
- Fetch secrets from Key Vault
- Load pipeline config and connectors
- Stream read batches from readers
- For each batch:
  - Apply transformations
  - Write batch to sink
  - Update execution_batches and pipeline_executions counters
  - Emit execution_logs and metrics
  - Acknowledge batch/task only after persistence and checkpoint
- Upon completion, set execution to Completed and produce final metrics

### Retry & DLQ
- Implement exponential backoff and max attempts for transient errors
- Permanent errors go to DLQ for manual inspection
- Use idempotency keys for retry safety

---

## Phase 5 — Scheduling & Cron

- SchedulerService reads pipeline.schedule_json and creates scheduled messages for RabbitMQ or uses Kubernetes CronJobs as appropriate.
- For cloud-native scheduling prefer a dedicated scheduler process that enqueues ExecutionTask messages at the correct times (handles timezones, DST).
- Use cron expressions and/or calendar scheduler; store schedule history and missed-run policies.
- Support manual trigger and API-based deferral.

---

## Phase 6 — Security, Secrets & Sandboxing

### Secrets
- Store secrets in Azure Key Vault.
- Connectors hold secret references (key vault secret URIs) in ConfigJson, not raw secrets.
- Worker fetches secrets at runtime using managed identity (AKS MSI) with least privilege.
- Support secret rotation: use Key Vault versioning and short-lived credentials where possible.

### Script sandboxing
Options (recommended order):
1. Containerized script-runner (AKS Jobs, ephemeral pods) with strict resource limits; run scripts inside minimal runtime image with no network/FS unless explicitly allowed.
2. WASM runtime for supported languages (fast, secure sandbox).
3. Out-of-process script microservice with restricted API.
4. In-process Jint/Roslyn only for trusted scripts and admins.

Enforce:
- CPU/memory/time limits
- No direct secret access unless granted via secure API (and audit logged)
- Audit and log script execution details (size limits, runtime, errors)

### Network & Data Security
- TLS everywhere
- Validate certificates for external DBs/APIs
- Define network policies in AKS, use private endpoints for Azure DB and Key Vault
- Encrypt data at rest (Azure managed encryption) and in transit
- Mask PII in logs and APIs

---

## Phase 7 — Observability & Monitoring

### Logging
- Structured logs (Serilog or similar) to Log Analytics / ELK
- Store full logs in Log Analytics and keep short summaries in pipeline_executions
- Log retention default: 365 days (configurable per tenant)
- Archive older logs to Blob Storage if cost-sensitive

### Metrics
- Prometheus metrics export for:
  - execution durations, bytes/rows processed, error rates, worker health, queue depth
- Use Azure Monitor integrations if preferred

### Tracing
- OpenTelemetry traces for end-to-end visibility across orchestrator → broker → worker
- Correlate traces with ExecutionId and BatchId

### Alerts & Dashboards
- Alert on long-running executions, high failure rates, queue depth, worker starvation
- Dashboards for per-tenant throughput, SLA compliance, resource usage

---

## Phase 8 — Performance & Scalability

### Streaming & Batching
- Default batch size: 1,000 rows (configurable 500–5,000 depending on row size)
- Use IAsyncEnumerable and process batch-by-batch (extract → transform → load) to keep memory bounded
- Persist batch checkpoints to execution_batches for resumability

### Bulk & Throughput
- Use native bulk-load APIs for DB targets
- For API targets, prefer bulk endpoints and parallel HTTP batches with rate limiting
- Tune RabbitMQ prefetch and worker replica counts based on throughput

### Quotas & Multi-tenancy
- Per-tenant concurrency quotas and resource limits
- Rate limit queue publishing per tenant or shard to avoid noisy neighbor

### Load Testing
- Test with realistic workloads: single pipelines with 10M+ rows and many concurrent pipelines
- Perform chaos engineering (node failures, network partitions) and measure recovery

---

## Transactional Guarantees & Idempotency

- Default: at-least-once delivery with idempotency and upsert strategies in writers
- Provide idempotency keys generated from source identity (file hash + offset, primary key values) to allow writers to deduplicate
- Exactly-once semantics: possible only when sinks support transactional semantics and the entire pipeline can be coordinated — expensive and not default
- Design writers to be idempotent or maintain dedup store (Redis / DB) when necessary

---

## Deployment & Operational Patterns (Azure specifics)

### Containerization
- Build images for API, worker, script-runner
- Push to Azure Container Registry (ACR)
- Use multi-stage Dockerfiles, small base images, and vulnerability scanning

### Local dev
- Provide docker-compose for local development:
  - Postgres (test), RabbitMQ, API, Worker (single), optional Key Vault emulator or env secrets
- Use Testcontainers/containers for integration tests in CI

### AKS (production)
- Deploy API + Worker as Deployments with HPA
- Use Managed Identity for AKS to access Key Vault and Azure Database for PostgreSQL
- Deploy RabbitMQ in-cluster via Helm (rabbitmq-ha) or use CloudAMQP / managed RabbitMQ offering; prefer managed RabbitMQ provider if available to reduce ops
- Use Azure Blob Storage for artifacts and log archival
- Use Azure Monitor / Log Analytics or external stack for logs/metrics
- Use PodSecurityPolicies / OPA/Gatekeeper, NetworkPolicies, and SecretsStore CSI driver to mount Key Vault secrets as needed

### RabbitMQ on Azure
- Options:
  - Run RabbitMQ cluster in AKS (Helm chart) — full control, more ops
  - Use CloudAMQP or RabbitMQ in Azure Marketplace as managed/hosted option
  - If operations cost is a concern consider Azure Service Bus as a fallback (requires abstraction to swap broker types)

---

## Testing Strategy (Production)

- Unit tests: processors, readers/writers, orchestrator with mocks
- Integration tests: run docker-compose with Postgres + RabbitMQ and sample pipelines
- Contract tests: connectors and external systems
- Performance/load tests: realistic large-file and high-concurrency scenarios
- Chaos tests: broker restarts, node terminations, network failures
- Security tests: dependency scans, script-runner escape attempts, secret leakage tests

---

## NuGet / Infra Packages (high-level)

- RabbitMQ.Client
- Confluent.Kafka (if Kafka becomes required)
- Npgsql, System.Data.SqlClient, MySqlConnector
- CsvHelper, EPPlus, Newtonsoft.Json/System.Text.Json
- OpenTelemetry packages, Prometheus exporters, Serilog + sinks
- Jint / Roslyn only for trusted/in-process scripting; otherwise use external execution

---

## Implementation Checklist & Timeline (Production-ready)

Estimated team: 2–4 engineers working in parallel. Timeline is approximate.

Phase 1: Core Execution (2–3 weeks)
- [ ] Create pipeline_executions table (bigint counters, timestamptz)
- [ ] Create execution_logs table (partitioned) and execution_batches table
- [ ] Execution DTOs and IExecutionService with EnqueueExecution
- [ ] ExecutionsController endpoints
- [ ] Unit tests for core flows

Phase 2: Data Readers & Writers (3–5 weeks)
- [ ] Implement streaming IDataReader & IDataWriter interfaces
- [ ] SQL/Postgres streaming readers and bulk writers
- [ ] File (CSV/Excel/JSON) and REST readers/writers
- [ ] Schema detection and TestConnection implementations
- [ ] Integration tests (Testcontainers/docker-compose)

Phase 3: Transformation Engine (3–4 weeks)
- [ ] Implement batch transformation processors
- [ ] Implement TransformationOrchestrator and policies
- [ ] Script execution sandbox/prototype (container-based or WASM)
- [ ] Unit & integration tests

Phase 4: Broker & Orchestration (3–5 weeks)
- [ ] RabbitMQ integration for enqueue/consume
- [ ] Implement worker container and PipelineOrchestrator
- [ ] SchedulerService and Cron handling
- [ ] Cancellation, retries, DLQ
- [ ] End-to-end integration tests in docker-compose

Phase 5: Security & Secrets (2–3 weeks)
- [ ] Azure Key Vault integration using Managed Identity
- [ ] Remove plaintext secrets from DB; use secret references
- [ ] Finalize script sandbox strategy and harden

Phase 6: Observability & Hardening (3–5 weeks)
- [ ] Centralized logging, metrics, tracing
- [ ] Dashboards and alerts
- [ ] Retention & archival policies (365 days default)
- [ ] Load & chaos testing, runbooks

Phase 7: Production rollout & scale tuning (3–6 weeks)
- [ ] AKS manifests & Helm charts
- [ ] Autoscaling tuning, resource limits, pod disruption policies
- [ ] Final performance tuning and operational runbooks

Total estimated: ~16–27 weeks depending on parallelization and team size. Production at massive scale may require additional weeks for tuning and operational maturity.

---

## Success Criteria (production)

- Pipelines execute reliably at scale with SLAs
- Executors scale horizontally and autoscale on load
- Streaming/batching prevents OOMs for large datasets
- Execution logs are available for 365 days and searchable
- Scripts run in sandboxed environment with audit trails
- Data transfer uses idempotency/upsert; retries safe under at-least-once semantics
- Secrets never stored in plaintext and are rotated via Key Vault

---

## Operational Notes & Best Practices

- Use AKS Managed Identity for secure Key Vault access and avoid injecting secrets in plaintext.
- Prefer managed RabbitMQ (CloudAMQP or vendor) if you want to reduce broker ops; otherwise deploy HA RabbitMQ on AKS with persistent volumes and backups.
- Archive old partitions of execution_logs to Blob Storage and remove old DB partitions to control cost.
- Use per-tenant quotas and rate-limiting to prevent noisy neighbors.
- Regularly run load tests as part of CI or a periodic performance pipeline.

---

## Next Immediate Steps (I can produce these artifacts for you)

1. Provide Dockerfile + docker-compose for API + worker + RabbitMQ + Postgres (local dev).
2. Provide AKS Kubernetes manifests and a Helm chart outline (API, worker, RabbitMQ helm values, Key Vault integration).
3. Generate EF Core migration SQL for new tables: pipeline_executions, execution_logs, execution_batches.
4. Provide example RabbitMQ message contract (ExecutionTask) and worker consumer skeleton.
5. Provide a sample script-runner job manifest and resource limits.

Tell me which artifacts to generate first and I will produce them (Dockerfiles + docker-compose are a good starting point).

---

## Decisions & Config Defaults (applied)
- Broker: RabbitMQ (durable queues, prefetch tuned per worker)
- Cloud: Azure (AKS, ACR, Azure Database for PostgreSQL, Azure Blob Storage, Azure Key Vault)
- Delivery semantics: at-least-once with idempotency/upsert
- Execution logs retention: 365 days (default)
- Batch size default: 1,000 rows (configurable)
- Default retry policy: Exponential backoff, 5 max attempts (configurable)
- Script execution policy: containerized sandbox with strict limits (recommended)

---

## Appendix — Useful Snippets & Patterns (to be added to repo)
- Dockerfile templates for API and worker
- docker-compose.yml for local development
- Example RabbitMQ ExecutionTask schema (JSON)
- Example EF Core migration SQL for new tables
- Example IAsyncEnumerable-read pattern for streaming readers
- Example worker consumer skeleton with prefetch and ack behavior
- Helm values snippet for RabbitMQ HA and worker deployment
(If you want, I will generate these files next.)

---

Document Version: 3.0  
Last Updated: 2025-11-28  
Author: GitHub Copilot Chat Assistant  
Status: Ready for Implementation