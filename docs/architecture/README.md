# Architecture Documentation

Technical architecture documentation for the MultiTenant ETL platform.

## Core Systems

### Pipeline Execution
- **[Execution Engine Implementation](./EXECUTION_ENGINE_IMPLEMENTATION.md)** - Complete implementation plan for the pipeline execution engine, including data operations, transformation processing, and orchestration.

### Transformation Engine
- **[Transformation Engine](./PHASE3_TRANSFORMATION_ENGINE.md)** - Phase 3 implementation of the transformation engine with batch-oriented processing and JavaScript scripting support.
- **[Transformation Architecture](./TRANSFORMATION-ARCHITECTURE-FINAL.md)** - Final architecture design for the transformation system.

### Data Connectivity
- **[Data Reader/Writer Factories](./DATA_READER_WRITER_FACTORIES.md)** - Factory pattern implementation for creating appropriate data readers and writers based on connector configuration.
- **[Connectors](./CONNECTORS.md)** - Implementation summary for database, file, and API connectors.

### Security & Compliance
- **[Audit Logging](./AUDIT_LOGGING.md)** - Comprehensive audit logging system for tracking user actions and system events across the platform.

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
```

## Quick Links

- [Main Documentation Index](../README.md)
- [Authentication Documentation](../auth/)
- [Setup Guides](../guides/)
