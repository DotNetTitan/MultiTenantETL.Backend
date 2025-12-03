using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Application.DataAccess;
using MultiTenantETL.Application.Orchestration;
using MultiTenantETL.Application.Transformations;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Domain.Enums;
using MultiTenantETL.Infrastructure.Persistence;
using static MultiTenantETL.Application.Transformations.TransformationPolicy;

namespace MultiTenantETL.Infrastructure.Orchestration;

public class PipelineOrchestrator : IPipelineOrchestrator
{
    private readonly ApplicationDbContext _context;
    private readonly IDataReaderFactory _readerFactory;
    private readonly IDataWriterFactory _writerFactory;
    private readonly ITransformationOrchestrator _transformationOrchestrator;
    private readonly IFieldMappingService _fieldMappingService;
    private readonly ILogger<PipelineOrchestrator> _logger;

    public PipelineOrchestrator(
        ApplicationDbContext context,
        IDataReaderFactory readerFactory,
        IDataWriterFactory writerFactory,
        ITransformationOrchestrator transformationOrchestrator,
        IFieldMappingService fieldMappingService,
        ILogger<PipelineOrchestrator> logger)
    {
        _context = context;
        _readerFactory = readerFactory;
        _writerFactory = writerFactory;
        _transformationOrchestrator = transformationOrchestrator;
        _fieldMappingService = fieldMappingService;
        _logger = logger;
    }

    public async Task ExecutePipelineAsync(Guid executionId, CancellationToken cancellationToken = default)
    {
        PipelineExecution? execution = null;
        
        try
        {
            // Load execution with pipeline and connectors
            execution = await _context.PipelineExecutions
                .Include(e => e.Pipeline)
                    .ThenInclude(p => p!.SourceConnector)
                .Include(e => e.Pipeline)
                    .ThenInclude(p => p!.DestinationConnector)
                .FirstOrDefaultAsync(e => e.Id == executionId, cancellationToken);

            if (execution == null)
            {
                _logger.LogError("Execution {ExecutionId} not found", executionId);
                return;
            }

            var pipeline = execution.Pipeline;
            if (pipeline == null)
            {
                await LogAndFailExecution(execution, "Pipeline not found", cancellationToken);
                return;
            }

            // Update status to Running
            execution.Status = ExecutionStatus.Running;
            execution.StartTime = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            await AddLogEntry(execution, "Info", "System", "Pipeline execution started", cancellationToken);

            // Create reader and writer
            var reader = _readerFactory.CreateReader(pipeline.SourceConnector!);
            var writer = _writerFactory.CreateWriter(pipeline.DestinationConnector!);

            var readOptions = new ReadOptions { BatchSize = 1000 };
            var writeOptions = ExtractWriteOptions(pipeline.DestinationConnector!);
            
            long totalProcessed = 0;
            long totalSucceeded = 0;
            long totalFailed = 0;
            int batchIndex = 0;

            // Stream data in batches
            await foreach (var batch in reader.ReadAsync(pipeline.SourceConnector!, readOptions, cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    await LogAndCancelExecution(execution, "Execution cancelled by user", cancellationToken);
                    return;
                }

                batchIndex++;
                
                // Create batch tracking record
                var executionBatch = new ExecutionBatch
                {
                    Id = Guid.NewGuid(),
                    ExecutionId = executionId,
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

                try
                {
                    // Apply field mappings (includes embedded transformations)
                    // Transformations are now applied per-field within the field mapping process
                    var mappedBatch = _fieldMappingService.ApplyFieldMappings(batch, pipeline.FieldMappingsJson);
                    
                    await AddLogEntry(execution, "Info", "FieldMapping",
                        $"Batch {batchIndex}: Applied field mappings with transformations, " +
                        $"{batch.RowCount} → {mappedBatch.RowCount} rows",
                        cancellationToken);
                    
                    // Write batch
                    var writeResult = await writer.WriteBatchAsync(
                        pipeline.DestinationConnector!,
                        mappedBatch,
                        writeOptions,
                        cancellationToken);

                    // Update batch status
                    executionBatch.Status = BatchStatus.Completed;
                    executionBatch.RowsSucceeded = writeResult.RowsWritten;
                    executionBatch.RowsFailed = writeResult.RowsFailed;
                    executionBatch.EndedAt = DateTimeOffset.UtcNow;

                    totalProcessed += batch.RowCount;
                    totalSucceeded += writeResult.RowsWritten;
                    totalFailed += writeResult.RowsFailed;

                    // Update execution progress
                    execution.RecordsProcessed = totalProcessed;
                    execution.RecordsSucceeded = totalSucceeded;
                    execution.RecordsFailed = totalFailed;
                    execution.BatchCount = batchIndex;
                    
                    await _context.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation(
                        "Batch {BatchIndex} completed for execution {ExecutionId}: {Succeeded} succeeded, {Failed} failed",
                        batchIndex, executionId, writeResult.RowsWritten, writeResult.RowsFailed);
                }
                catch (Exception batchEx)
                {
                    _logger.LogError(batchEx, "Error processing batch {BatchIndex} for execution {ExecutionId}", 
                        batchIndex, executionId);
                    
                    executionBatch.Status = BatchStatus.Failed;
                    executionBatch.RowsFailed = batch.RowCount;
                    executionBatch.EndedAt = DateTimeOffset.UtcNow;
                    
                    totalFailed += batch.RowCount;
                    execution.RecordsFailed = totalFailed;
                    
                    await _context.SaveChangesAsync(cancellationToken);
                    await AddLogEntry(execution, "Error", "Batch", $"Batch {batchIndex} failed: {batchEx.Message}", cancellationToken);
                    
                    // Continue with next batch (fail-safe mode)
                }
            }

            // Complete execution
            execution.Status = ExecutionStatus.Completed;
            execution.EndTime = DateTimeOffset.UtcNow;
            execution.Duration = execution.EndTime.Value - execution.StartTime;
            execution.ProgressPercent = 100;

            await _context.SaveChangesAsync(cancellationToken);
            await AddLogEntry(execution, "Info", "System", 
                $"Pipeline execution completed: {totalSucceeded} succeeded, {totalFailed} failed", 
                cancellationToken);

            _logger.LogInformation(
                "Execution {ExecutionId} completed successfully: {TotalProcessed} records processed",
                executionId, totalProcessed);
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

            // Check for writeConfig section (database connectors)
            if (root.TryGetProperty("writeConfig", out var writeConfig))
            {
                // Check operation type
                if (writeConfig.TryGetProperty("operation", out var operation))
                {
                    var operationValue = operation.GetString()?.ToUpperInvariant();
                    options.UseUpsert = operationValue == "UPSERT";
                    
                    _logger.LogDebug("Destination connector operation: {Operation}, UseUpsert: {UseUpsert}", 
                        operationValue, options.UseUpsert);
                }

                // Get primary keys for upsert
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

                // Check for truncate option
                if (writeConfig.TryGetProperty("truncateBeforeLoad", out var truncate))
                {
                    options.TruncateBeforeLoad = truncate.GetBoolean();
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse destination connector config for write options");
        }

        return options;
    }
}
