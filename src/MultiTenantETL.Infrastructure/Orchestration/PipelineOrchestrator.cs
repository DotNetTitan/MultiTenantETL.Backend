using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Application.DataAccess;
using MultiTenantETL.Application.Orchestration;
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
    private readonly ILogger<PipelineOrchestrator> _logger;

    public PipelineOrchestrator(
        ApplicationDbContext context,
        IDataReaderFactory readerFactory,
        IDataWriterFactory writerFactory,
        IFieldMappingService fieldMappingService,
        ILogger<PipelineOrchestrator> logger)
    {
        _context = context;
        _readerFactory = readerFactory;
        _writerFactory = writerFactory;
        _fieldMappingService = fieldMappingService;
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
                await LogAndFailExecution(execution, validationError, cancellationToken);
                return;
            }

            var pipeline = execution.Pipeline!;
            await StartExecutionAsync(execution, cancellationToken);

            var result = await ProcessBatchesAsync(execution, pipeline, cancellationToken);
            
            if (result.WasCancelled)
            {
                return; // Already handled in ProcessBatchesAsync
            }

            await CompleteExecutionAsync(execution, result, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error executing pipeline for execution {ExecutionId}", executionId);
            
            if (execution != null)
            {
                await LogAndFailExecution(execution, $"Fatal error: {ex.Message}", cancellationToken);
            }
        }
    }

    #region Execution Lifecycle

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
        await AddLogEntry(execution, "Info", "System", "Pipeline execution started", cancellationToken);
    }

    private async Task CompleteExecutionAsync(
        PipelineExecution execution, 
        BatchProcessingResult result, 
        CancellationToken cancellationToken)
    {
        execution.Status = ExecutionStatus.Completed;
        execution.EndTime = DateTimeOffset.UtcNow;
        execution.Duration = execution.EndTime.Value - execution.StartTime;
        execution.ProgressPercent = 100;

        await _context.SaveChangesAsync(cancellationToken);
        await AddLogEntry(execution, "Info", "System", 
            $"Pipeline execution completed: {result.TotalSucceeded} succeeded, {result.TotalFailed} failed", 
            cancellationToken);

        _logger.LogInformation(
            "Execution {ExecutionId} completed successfully: {TotalProcessed} records processed",
            execution.Id, result.TotalProcessed);
    }

    #endregion

    #region Batch Processing

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
                await LogAndCancelExecution(execution, "Execution cancelled by user", cancellationToken);
                result.WasCancelled = true;
                return result;
            }

            result.BatchIndex++;
            
            var executionBatch = await CreateBatchTrackingRecordAsync(execution, batch, result.BatchIndex, cancellationToken);

            try
            {
                var batchResult = await ProcessSingleBatchAsync(
                    execution, pipeline, writer, batch, writeOptions, result.BatchIndex, cancellationToken);

                UpdateBatchSuccess(executionBatch, batchResult);
                result.AddSuccess(batch.RowCount, batchResult.RowsWritten, batchResult.RowsFailed);

                await UpdateExecutionProgressAsync(execution, result, cancellationToken);

                _logger.LogInformation(
                    "Batch {BatchIndex} completed for execution {ExecutionId}: {Succeeded} succeeded, {Failed} failed",
                    result.BatchIndex, execution.Id, batchResult.RowsWritten, batchResult.RowsFailed);
            }
            catch (Exception batchEx)
            {
                _logger.LogError(batchEx, "Error processing batch {BatchIndex} for execution {ExecutionId}", 
                    result.BatchIndex, execution.Id);
                
                UpdateBatchFailure(executionBatch, batch.RowCount);
                result.AddFailure(batch.RowCount);

                await _context.SaveChangesAsync(cancellationToken);
                await AddLogEntry(execution, "Error", "Batch", 
                    $"Batch {result.BatchIndex} failed: {batchEx.Message}", cancellationToken);
                
                // Continue with next batch (fail-safe mode)
            }
        }

        return result;
    }

    private async Task<ExecutionBatch> CreateBatchTrackingRecordAsync(
        PipelineExecution execution,
        ReadBatch batch,
        int batchIndex,
        CancellationToken cancellationToken)
    {
        var executionBatch = new ExecutionBatch
        {
            Id = Guid.NewGuid(),
            ExecutionId = execution.Id,
            TenantId = execution.TenantId,
            BatchIndex = batchIndex,
            RowsCount = batch.RowCount,
            RowsSucceeded = 0,
            RowsFailed = 0,
            Status = BatchStatus.Processing,
            StartedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        };
        
        _context.ExecutionBatches.Add(executionBatch);
        await _context.SaveChangesAsync(cancellationToken);
        
        return executionBatch;
    }

    private async Task<DataWriteResult> ProcessSingleBatchAsync(
        PipelineExecution execution,
        Pipeline pipeline,
        IDataWriter writer,
        ReadBatch batch,
        WriteOptions writeOptions,
        int batchIndex,
        CancellationToken cancellationToken)
    {
        // Apply field mappings (includes embedded transformations)
        var mappedBatch = _fieldMappingService.ApplyFieldMappings(batch, pipeline.FieldMappingsJson);
        
        await AddLogEntry(execution, "Info", "FieldMapping",
            $"Batch {batchIndex}: Applied field mappings with transformations, " +
            $"{batch.RowCount} → {mappedBatch.RowCount} rows",
            cancellationToken);
        
        // Write batch to destination
        return await writer.WriteBatchAsync(
            pipeline.DestinationConnector!,
            mappedBatch,
            writeOptions,
            cancellationToken);
    }

    private static void UpdateBatchSuccess(ExecutionBatch executionBatch, DataWriteResult writeResult)
    {
        executionBatch.Status = BatchStatus.Completed;
        executionBatch.RowsSucceeded = writeResult.RowsWritten;
        executionBatch.RowsFailed = writeResult.RowsFailed;
        executionBatch.EndedAt = DateTimeOffset.UtcNow;
    }

    private static void UpdateBatchFailure(ExecutionBatch executionBatch, int rowCount)
    {
        executionBatch.Status = BatchStatus.Failed;
        executionBatch.RowsFailed = rowCount;
        executionBatch.EndedAt = DateTimeOffset.UtcNow;
    }

    private async Task UpdateExecutionProgressAsync(
        PipelineExecution execution,
        BatchProcessingResult result,
        CancellationToken cancellationToken)
    {
        execution.RecordsProcessed = result.TotalProcessed;
        execution.RecordsSucceeded = result.TotalSucceeded;
        execution.RecordsFailed = result.TotalFailed;
        execution.BatchCount = result.BatchIndex;
        
        await _context.SaveChangesAsync(cancellationToken);
    }

    #endregion

    #region Logging and Status Updates

    private async Task AddLogEntry(
        PipelineExecution execution, 
        string level, 
        string source, 
        string message,
        CancellationToken cancellationToken)
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
    }

    private async Task LogAndFailExecution(
        PipelineExecution execution, 
        string errorMessage,
        CancellationToken cancellationToken)
    {
        execution.Status = ExecutionStatus.Failed;
        execution.EndTime = DateTimeOffset.UtcNow;
        execution.Duration = execution.EndTime.Value - execution.StartTime;
        execution.ErrorMessage = errorMessage;

        await _context.SaveChangesAsync(cancellationToken);
        await AddLogEntry(execution, "Error", "System", errorMessage, cancellationToken);
    }

    private async Task LogAndCancelExecution(
        PipelineExecution execution,
        string message,
        CancellationToken cancellationToken)
    {
        execution.Status = ExecutionStatus.Cancelled;
        execution.EndTime = DateTimeOffset.UtcNow;
        execution.Duration = execution.EndTime.Value - execution.StartTime;

        await _context.SaveChangesAsync(cancellationToken);
        await AddLogEntry(execution, "Warning", "System", message, cancellationToken);
    }

    #endregion

    #region Configuration Parsing

    /// <summary>
    /// Extracts write options from the destination connector's configuration.
    /// Supports database connectors with writeConfig containing operation and primaryKeys.
    /// </summary>
    private WriteOptions ExtractWriteOptions(Connector destinationConnector)
    {
        var options = new WriteOptions();

        if (string.IsNullOrEmpty(destinationConnector.ConfigJson))
        {
            return options;
        }

        try
        {
            using var doc = JsonDocument.Parse(destinationConnector.ConfigJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("writeConfig", out var writeConfig))
            {
                ParseWriteConfig(writeConfig, options);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse destination connector config for write options");
        }

        return options;
    }

    private void ParseWriteConfig(JsonElement writeConfig, WriteOptions options)
    {
        // Parse operation type
        if (writeConfig.TryGetProperty("operation", out var operation))
        {
            var operationValue = operation.GetString()?.ToUpperInvariant();
            options.UseUpsert = operationValue == "UPSERT";
            
            _logger.LogDebug("Destination connector operation: {Operation}, UseUpsert: {UseUpsert}", 
                operationValue, options.UseUpsert);
        }

        // Parse primary keys for upsert
        if (writeConfig.TryGetProperty("primaryKeys", out var primaryKeys) && 
            primaryKeys.ValueKind == JsonValueKind.Array)
        {
            options.UpsertKeys = primaryKeys.EnumerateArray()
                .Select(k => k.GetString())
                .Where(k => !string.IsNullOrEmpty(k))
                .Cast<string>()
                .ToList();
            
            _logger.LogDebug("Destination connector primary keys: {PrimaryKeys}", 
                string.Join(", ", options.UpsertKeys));
        }

        // Parse truncate option
        if (writeConfig.TryGetProperty("truncateBeforeLoad", out var truncate))
        {
            options.TruncateBeforeLoad = truncate.GetBoolean();
        }
    }

    #endregion

    #region Helper Types

    /// <summary>
    /// Tracks the cumulative results of batch processing.
    /// </summary>
    private sealed class BatchProcessingResult
    {
        public int BatchIndex { get; set; }
        public long TotalProcessed { get; set; }
        public long TotalSucceeded { get; set; }
        public long TotalFailed { get; set; }
        public bool WasCancelled { get; set; }

        public void AddSuccess(int rowCount, long rowsWritten, long rowsFailed)
        {
            TotalProcessed += rowCount;
            TotalSucceeded += rowsWritten;
            TotalFailed += rowsFailed;
        }

        public void AddFailure(int rowCount)
        {
            TotalFailed += rowCount;
        }
    }

    #endregion
}
