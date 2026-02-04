using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Application.DataAccess;
using MultiTenantETL.Application.Orchestration;
using MultiTenantETL.Application.Executions;
using MultiTenantETL.Application.Executions.Models;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Domain.Enums;
using MultiTenantETL.Infrastructure.Persistence;

namespace MultiTenantETL.Infrastructure.Orchestration;

public class PipelineOrchestrator : IPipelineOrchestrator
{
    private readonly ApplicationDbContext _context;
    private readonly IDataReaderFactory _readerFactory;
    private readonly IDataWriterFactory _writerFactory;
    private readonly IFieldMappingService _fieldMappingService;
    private readonly IExecutionHubService _executionHubService;
    private readonly ILogger<PipelineOrchestrator> _logger;

    public PipelineOrchestrator(
        ApplicationDbContext context,
        IDataReaderFactory readerFactory,
        IDataWriterFactory writerFactory,
        IFieldMappingService fieldMappingService,
        IExecutionHubService executionHubService,
        ILogger<PipelineOrchestrator> logger)
    {
        _context = context;
        _readerFactory = readerFactory;
        _writerFactory = writerFactory;
        _fieldMappingService = fieldMappingService;
        _executionHubService = executionHubService;
        _logger = logger;
    }

    public async Task ExecutePipelineAsync(Guid executionId, CancellationToken cancellationToken = default)
    {
        PipelineExecution? execution = null;
        
        try
        {
            execution = await LoadExecutionAsync(executionId, cancellationToken);
            if (execution == null)
            {
                _logger.LogError("Execution {ExecutionId} not found", executionId);
                return;
            }

            var validationError = ValidatePipelineConfiguration(execution);
            if (validationError != null)
            {
                await FailExecutionAsync(execution, validationError, cancellationToken);
                return;
            }

            var pipeline = execution.Pipeline!;
            await StartExecutionAsync(execution, cancellationToken);

            var result = await ProcessBatchesAsync(execution, pipeline, cancellationToken);
            
            if (!result.WasCancelled)
            {
                await CompleteExecutionAsync(execution, result, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error executing pipeline for execution {ExecutionId}", executionId);
            
            if (execution != null)
            {
                await FailExecutionAsync(execution, $"Fatal error: {ex.Message}", cancellationToken);
            }
        }
    }

    private async Task<PipelineExecution?> LoadExecutionAsync(Guid executionId, CancellationToken cancellationToken)
    {
        return await _context.PipelineExecutions
            .Include(e => e.Pipeline)
                .ThenInclude(p => p!.SourceConnector)
            .Include(e => e.Pipeline)
                .ThenInclude(p => p!.DestinationConnector)
            .FirstOrDefaultAsync(e => e.Id == executionId, cancellationToken);
    }

    private static string? ValidatePipelineConfiguration(PipelineExecution execution)
    {
        if (execution.Pipeline == null)
            return "Pipeline not found";
        if (execution.Pipeline.SourceConnector == null)
            return "Source connector not found";
        if (execution.Pipeline.DestinationConnector == null)
            return "Destination connector not found";
        return null;
    }

    private async Task StartExecutionAsync(PipelineExecution execution, CancellationToken cancellationToken)
    {
        execution.Status = ExecutionStatus.Running;
        execution.StartTime = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        await AddLogEntryAsync(execution, "Info", "System", "Pipeline execution started", cancellationToken);

        // Send status update via SignalR
        await _executionHubService.SendStatusUpdateAsync(execution.Id, execution.TenantId, new ExecutionStatusUpdate
        {
            Status = execution.Status.ToString(),
            Timestamp = DateTimeOffset.UtcNow
        }, cancellationToken);
    }

    private async Task CompleteExecutionAsync(PipelineExecution execution, BatchProcessingResult result, CancellationToken cancellationToken)
    {
        execution.Status = ExecutionStatus.Completed;
        execution.EndTime = DateTimeOffset.UtcNow;
        execution.Duration = execution.EndTime.Value - execution.StartTime;
        execution.ProgressPercent = 100;

        // Update pipeline's last run tracking fields
        if (execution.Pipeline != null)
        {
            execution.Pipeline.LastRunAt = DateTime.UtcNow;
            execution.Pipeline.LastRunStatus = "Completed";
            execution.Pipeline.LastRunRecordsProcessed = (int)result.TotalProcessed;
        }

        await _context.SaveChangesAsync(cancellationToken);
        await AddLogEntryAsync(execution, "Info", "System", 
            $"Pipeline execution completed: {result.TotalSucceeded} succeeded, {result.TotalFailed} failed", 
            cancellationToken);

        // Send completion via SignalR
        await _executionHubService.SendCompletionAsync(execution.Id, execution.TenantId, new ExecutionCompletionUpdate
        {
            Status = execution.Status.ToString(),
            StartTime = execution.StartTime,
            EndTime = execution.EndTime.Value,
            Duration = execution.Duration.Value,
            RecordsProcessed = execution.RecordsProcessed,
            RecordsSucceeded = execution.RecordsSucceeded,
            RecordsFailed = execution.RecordsFailed
        }, cancellationToken);

        _logger.LogInformation("Execution {ExecutionId} completed: {TotalProcessed} records processed",
            execution.Id, result.TotalProcessed);
    }

    private async Task FailExecutionAsync(PipelineExecution execution, string errorMessage, CancellationToken cancellationToken)
    {
        execution.Status = ExecutionStatus.Failed;
        execution.EndTime = DateTimeOffset.UtcNow;
        execution.Duration = execution.EndTime.Value - execution.StartTime;
        execution.ErrorMessage = errorMessage;

        // Update pipeline's last run tracking fields
        if (execution.Pipeline != null)
        {
            execution.Pipeline.LastRunAt = DateTime.UtcNow;
            execution.Pipeline.LastRunStatus = "Failed";
        }

        await _context.SaveChangesAsync(cancellationToken);
        await AddLogEntryAsync(execution, "Error", "System", errorMessage, cancellationToken);

        // Send completion via SignalR
        await _executionHubService.SendCompletionAsync(execution.Id, execution.TenantId, new ExecutionCompletionUpdate
        {
            Status = execution.Status.ToString(),
            StartTime = execution.StartTime,
            EndTime = execution.EndTime.Value,
            Duration = execution.Duration.Value,
            RecordsProcessed = execution.RecordsProcessed,
            RecordsSucceeded = execution.RecordsSucceeded,
            RecordsFailed = execution.RecordsFailed,
            ErrorMessage = errorMessage
        }, cancellationToken);
    }

    private async Task CancelExecutionAsync(PipelineExecution execution, CancellationToken cancellationToken)
    {
        execution.Status = ExecutionStatus.Cancelled;
        execution.EndTime = DateTimeOffset.UtcNow;
        execution.Duration = execution.EndTime.Value - execution.StartTime;

        // Update pipeline's last run tracking fields
        if (execution.Pipeline != null)
        {
            execution.Pipeline.LastRunAt = DateTime.UtcNow;
            execution.Pipeline.LastRunStatus = "Cancelled";
        }

        await _context.SaveChangesAsync(cancellationToken);
        await AddLogEntryAsync(execution, "Warning", "System", "Execution cancelled by user", cancellationToken);

        // Send completion via SignalR
        await _executionHubService.SendCompletionAsync(execution.Id, execution.TenantId, new ExecutionCompletionUpdate
        {
            Status = execution.Status.ToString(),
            StartTime = execution.StartTime,
            EndTime = execution.EndTime.Value,
            Duration = execution.Duration.Value,
            RecordsProcessed = execution.RecordsProcessed,
            RecordsSucceeded = execution.RecordsSucceeded,
            RecordsFailed = execution.RecordsFailed
        }, cancellationToken);
    }

    private async Task<BatchProcessingResult> ProcessBatchesAsync(
        PipelineExecution execution,
        Pipeline pipeline,
        CancellationToken cancellationToken)
    {
        var reader = _readerFactory.CreateReader(pipeline.SourceConnector!);
        await using var writer = _writerFactory.CreateWriter(pipeline.DestinationConnector!);

        var readOptions = new ReadOptions { BatchSize = 1000 };
        var writeOptions = ExtractWriteOptions(pipeline.DestinationConnector!);
        var result = new BatchProcessingResult();

        await foreach (var batch in reader.ReadAsync(pipeline.SourceConnector!, readOptions, cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                await CancelExecutionAsync(execution, cancellationToken);
                result.WasCancelled = true;
                return result;
            }

            result.BatchIndex++;
            await AddLogEntryAsync(execution, "Info", "DataReader", $"Batch {result.BatchIndex}: Read {batch.RowCount} rows from source", cancellationToken);
            await ProcessBatchAsync(execution, pipeline, writer, batch, writeOptions, result, cancellationToken);
        }

        return result;
    }

    private async Task ProcessBatchAsync(
        PipelineExecution execution,
        Pipeline pipeline,
        IDataWriter writer,
        ReadBatch batch,
        WriteOptions writeOptions,
        BatchProcessingResult result,
        CancellationToken cancellationToken)
    {
        var executionBatch = new ExecutionBatch
        {
            Id = Guid.NewGuid(),
            ExecutionId = execution.Id,
            TenantId = execution.TenantId,
            BatchIndex = result.BatchIndex,
            RowsCount = batch.RowCount,
            Status = BatchStatus.Processing,
            StartedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _context.ExecutionBatches.Add(executionBatch);
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            var mappedBatch = _fieldMappingService.ApplyFieldMappings(batch, pipeline.FieldMappingsJson);
            
            await AddLogEntryAsync(execution, "Info", "FieldMapping",
                $"Batch {result.BatchIndex}: Applied field mappings, {batch.RowCount} → {mappedBatch.RowCount} rows",
                cancellationToken);

            await AddLogEntryAsync(execution, "Info", "DataWriter", $"Batch {result.BatchIndex}: Writing {mappedBatch.RowCount} rows to destination", cancellationToken);
            var writeResult = await writer.WriteBatchAsync(pipeline.DestinationConnector!, mappedBatch, writeOptions, cancellationToken);

            executionBatch.Status = BatchStatus.Completed;
            executionBatch.RowsSucceeded = writeResult.RowsWritten;
            executionBatch.RowsFailed = writeResult.RowsFailed;
            executionBatch.EndedAt = DateTimeOffset.UtcNow;

            result.TotalProcessed += batch.RowCount;
            result.TotalSucceeded += writeResult.RowsWritten;
            result.TotalFailed += writeResult.RowsFailed;

            execution.RecordsProcessed = result.TotalProcessed;
            execution.RecordsSucceeded = result.TotalSucceeded;
            execution.RecordsFailed = result.TotalFailed;
            execution.BatchCount = result.BatchIndex;

            await _context.SaveChangesAsync(cancellationToken);

            await AddLogEntryAsync(execution, "Info", "Batch", 
                $"Batch {result.BatchIndex} completed: {writeResult.RowsWritten} rows written, {writeResult.RowsFailed} rows failed", 
                cancellationToken);

            // Send progress update via SignalR
            await _executionHubService.SendStatsUpdateAsync(execution.Id, execution.TenantId, new ExecutionProgressUpdate
            {
                RecordsProcessed = execution.RecordsProcessed,
                RecordsSucceeded = execution.RecordsSucceeded,
                RecordsFailed = execution.RecordsFailed,
                ProgressPercent = execution.ProgressPercent,
                BatchCount = execution.BatchCount,
                Timestamp = DateTimeOffset.UtcNow
            }, cancellationToken);

            _logger.LogInformation("Batch {BatchIndex} completed for execution {ExecutionId}: {Succeeded} succeeded, {Failed} failed",
                result.BatchIndex, execution.Id, writeResult.RowsWritten, writeResult.RowsFailed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing batch {BatchIndex} for execution {ExecutionId}", result.BatchIndex, execution.Id);

            executionBatch.Status = BatchStatus.Failed;
            executionBatch.RowsFailed = batch.RowCount;
            executionBatch.EndedAt = DateTimeOffset.UtcNow;
            result.TotalFailed += batch.RowCount;

            await _context.SaveChangesAsync(cancellationToken);
            await AddLogEntryAsync(execution, "Error", "Batch", $"Batch {result.BatchIndex} failed: {ex.Message}", cancellationToken);
        }
    }

    private async Task AddLogEntryAsync(PipelineExecution execution, string level, string source, string message, CancellationToken cancellationToken)
    {
        var logEntry = new ExecutionLogEntry
        {
            Id = Guid.NewGuid(),
            ExecutionId = execution.Id,
            TenantId = execution.TenantId,
            Timestamp = DateTimeOffset.UtcNow,
            Level = level,
            Source = source,
            Message = message,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _context.ExecutionLogs.Add(logEntry);
        await _context.SaveChangesAsync(cancellationToken);

        // Send log via SignalR
        await _executionHubService.SendLogAsync(execution.Id, execution.TenantId, new ExecutionLogDto
        {
            Timestamp = logEntry.Timestamp,
            Level = logEntry.Level,
            Source = logEntry.Source,
            Message = logEntry.Message,
            Details = logEntry.Details
        }, cancellationToken);
    }

    private WriteOptions ExtractWriteOptions(Connector connector)
    {
        var options = new WriteOptions();
        if (string.IsNullOrEmpty(connector.ConfigJson)) return options;

        try
        {
            using var doc = JsonDocument.Parse(connector.ConfigJson);
            if (doc.RootElement.TryGetProperty("writeConfig", out var writeConfig))
            {
                if (writeConfig.TryGetProperty("operation", out var op))
                    options.UseUpsert = op.GetString()?.ToUpperInvariant() == "UPSERT";

                if (writeConfig.TryGetProperty("primaryKeys", out var keys) && keys.ValueKind == JsonValueKind.Array)
                    options.UpsertKeys = keys.EnumerateArray().Select(k => k.GetString()).Where(k => !string.IsNullOrEmpty(k)).Cast<string>().ToList();

                if (writeConfig.TryGetProperty("truncateBeforeLoad", out var truncate))
                    options.TruncateBeforeLoad = truncate.GetBoolean();
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse connector config for write options");
        }

        return options;
    }

    private sealed class BatchProcessingResult
    {
        public int BatchIndex { get; set; }
        public long TotalProcessed { get; set; }
        public long TotalSucceeded { get; set; }
        public long TotalFailed { get; set; }
        public bool WasCancelled { get; set; }
    }
}
