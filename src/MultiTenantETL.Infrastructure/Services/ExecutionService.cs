using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Executions;
using MultiTenantETL.Application.Executions.Models;
using MultiTenantETL.Application.Messaging;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Domain.Enums;
using MultiTenantETL.Domain.ValueObjects;
using MultiTenantETL.Infrastructure.Persistence;

namespace MultiTenantETL.Infrastructure.Services;

public class ExecutionService : IExecutionService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<ExecutionService> _logger;

    public ExecutionService(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IMessagePublisher messagePublisher,
        ILogger<ExecutionService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _messagePublisher = messagePublisher;
        _logger = logger;
    }

    public async Task<ExecutionResponse> StartExecutionAsync(
        Guid pipelineId, 
        string triggeredBy, 
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        // Get the pipeline
        var pipeline = await _context.Set<Pipeline>()
            .Include(p => p.Tenant)
            .FirstOrDefaultAsync(p => p.Id == pipelineId, cancellationToken);

        if (pipeline == null)
        {
            throw new KeyNotFoundException($"Pipeline with ID {pipelineId} not found");
        }

        // Verify tenant access
        var currentTenantId = _currentUserService.GetTenantId();
        if (pipeline.TenantId != currentTenantId)
        {
            throw new UnauthorizedAccessException("You don't have access to this pipeline");
        }

        // Check if pipeline is active
        if (!pipeline.IsActive)
        {
            throw new InvalidOperationException("Cannot execute an inactive pipeline");
        }

        // Create execution record
        var execution = new PipelineExecution
        {
            Id = Guid.NewGuid(),
            PipelineId = pipelineId,
            TenantId = pipeline.TenantId,
            Status = ExecutionStatus.Queued,
            StartTime = DateTimeOffset.UtcNow,
            RecordsProcessed = 0,
            RecordsSucceeded = 0,
            RecordsFailed = 0,
            ProgressPercent = 0,
            BatchCount = 0,
            TriggeredBy = triggeredBy,
            TriggeredByUserId = userId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.PipelineExecutions.Add(execution);
        await _context.SaveChangesAsync(cancellationToken);

        // Create initial log entry
        var logEntry = new ExecutionLogEntry
        {
            Id = Guid.NewGuid(),
            ExecutionId = execution.Id,
            TenantId = execution.TenantId,
            Timestamp = DateTimeOffset.UtcNow,
            Level = "Info",
            Source = "System",
            Message = "Pipeline execution queued",
            Details = $"Pipeline: {pipeline.Name}",
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.ExecutionLogs.Add(logEntry);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Pipeline execution {ExecutionId} created for pipeline {PipelineId} by {TriggeredBy}",
            execution.Id, pipelineId, triggeredBy);

        // Publish execution task to RabbitMQ
        var executionTask = new ExecutionTask
        {
            ExecutionId = execution.Id,
            PipelineId = pipelineId,
            TenantId = pipeline.TenantId,
            BatchSize = 1000,
            DryRun = false,
            QueuedAt = DateTimeOffset.UtcNow
        };

        await _messagePublisher.PublishExecutionTaskAsync(executionTask, cancellationToken);

        return await MapToExecutionResponse(execution, pipeline);
    }

    public async Task<ExecutionResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var execution = await _context.PipelineExecutions
            .Include(e => e.Pipeline)
            .Include(e => e.Tenant)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (execution == null)
        {
            throw new KeyNotFoundException($"Execution with ID {id} not found");
        }

        // Verify tenant access
        var currentTenantId = _currentUserService.GetTenantId();
        if (execution.TenantId != currentTenantId)
        {
            throw new UnauthorizedAccessException("You don't have access to this execution");
        }

        return await MapToExecutionResponse(execution, execution.Pipeline);
    }

    public async Task<PagedExecutionResponse> GetAllAsync(
        ExecutionSearchRequest request, 
        CancellationToken cancellationToken = default)
    {
        var currentTenantId = _currentUserService.GetTenantId();

        var query = _context.PipelineExecutions
            .Include(e => e.Pipeline)
            .Where(e => e.TenantId == currentTenantId);

        // Apply filters
        if (request.PipelineId.HasValue)
        {
            query = query.Where(e => e.PipelineId == request.PipelineId.Value);
        }

        if (!string.IsNullOrEmpty(request.Status) && request.Status != "All")
        {
            if (Enum.TryParse<ExecutionStatus>(request.Status, out var statusEnum))
            {
                query = query.Where(e => e.Status == statusEnum);
            }
        }

        if (!string.IsNullOrEmpty(request.TriggeredBy))
        {
            query = query.Where(e => e.TriggeredBy == request.TriggeredBy);
        }

        if (request.StartDate.HasValue)
        {
            query = query.Where(e => e.StartTime >= request.StartDate.Value);
        }

        if (request.EndDate.HasValue)
        {
            query = query.Where(e => e.StartTime <= request.EndDate.Value);
        }

        if (!string.IsNullOrEmpty(request.Search))
        {
            var searchLower = request.Search.ToLower();
            query = query.Where(e => 
                e.Pipeline!.Name.ToLower().Contains(searchLower) ||
                e.Id.ToString().ToLower().Contains(searchLower));
        }

        // Apply sorting
        query = ApplySorting(query, request.SortBy);

        // Get total count
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply pagination
        var executions = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var executionList = executions.Select(e => new ExecutionListResponse
        {
            Id = e.Id,
            PipelineId = e.PipelineId,
            PipelineName = e.Pipeline?.Name,
            Status = e.Status.ToString(),
            StartTime = e.StartTime,
            EndTime = e.EndTime,
            Duration = e.Duration,
            RecordsProcessed = e.RecordsProcessed,
            ProgressPercent = e.ProgressPercent,
            TriggeredBy = e.TriggeredBy,
            CreatedAt = e.CreatedAt
        }).ToList();

        return new PagedExecutionResponse
        {
            Executions = executionList,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize)
        };
    }

    public async Task<ExecutionResponse> CancelExecutionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var execution = await _context.PipelineExecutions
            .Include(e => e.Pipeline)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (execution == null)
        {
            throw new KeyNotFoundException($"Execution with ID {id} not found");
        }

        // Verify tenant access
        var currentTenantId = _currentUserService.GetTenantId();
        if (execution.TenantId != currentTenantId)
        {
            throw new UnauthorizedAccessException("You don't have access to this execution");
        }

        // Can only cancel running or queued executions
        if (execution.Status != ExecutionStatus.Running && execution.Status != ExecutionStatus.Queued)
        {
            throw new InvalidOperationException($"Cannot cancel execution with status: {execution.Status}");
        }

        // Update status
        execution.Status = ExecutionStatus.Cancelled;
        execution.EndTime = DateTimeOffset.UtcNow;
        execution.Duration = execution.EndTime.Value - execution.StartTime;

        // Add cancellation log entry
        var logEntry = new ExecutionLogEntry
        {
            Id = Guid.NewGuid(),
            ExecutionId = execution.Id,
            TenantId = execution.TenantId,
            Timestamp = DateTimeOffset.UtcNow,
            Level = "Warning",
            Source = "System",
            Message = "Execution cancelled by user",
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.ExecutionLogs.Add(logEntry);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Execution {ExecutionId} cancelled", id);

        // Publish cancellation request to RabbitMQ
        await _messagePublisher.PublishCancellationRequestAsync(id, cancellationToken);

        return await MapToExecutionResponse(execution, execution.Pipeline);
    }

    public async Task<ExecutionStatsDto> GetStatsAsync(Guid? pipelineId = null, CancellationToken cancellationToken = default)
    {
        var currentTenantId = _currentUserService.GetTenantId();

        var query = _context.PipelineExecutions
            .Where(e => e.TenantId == currentTenantId);

        if (pipelineId.HasValue)
        {
            query = query.Where(e => e.PipelineId == pipelineId.Value);
        }

        var executions = await query.ToListAsync(cancellationToken);

        var totalExecutions = executions.Count;
        var completedExecutions = executions.Count(e => e.Status == ExecutionStatus.Completed);
        var failedExecutions = executions.Count(e => e.Status == ExecutionStatus.Failed);
        var runningExecutions = executions.Count(e => e.Status == ExecutionStatus.Running);
        var cancelledExecutions = executions.Count(e => e.Status == ExecutionStatus.Cancelled);

        var successRate = totalExecutions > 0 
            ? (decimal)completedExecutions / totalExecutions * 100 
            : 0;

        var completedWithDuration = executions.Where(e => e.Duration.HasValue).ToList();
        var averageDuration = completedWithDuration.Any() 
            ? TimeSpan.FromTicks((long)completedWithDuration.Average(e => e.Duration!.Value.Ticks))
            : (TimeSpan?)null;

        var totalRecords = executions.Sum(e => e.RecordsProcessed);
        var lastExecution = executions.OrderByDescending(e => e.StartTime).FirstOrDefault();

        return new ExecutionStatsDto
        {
            TotalExecutions = totalExecutions,
            RunningExecutions = runningExecutions,
            CompletedExecutions = completedExecutions,
            FailedExecutions = failedExecutions,
            CancelledExecutions = cancelledExecutions,
            SuccessRate = successRate,
            AverageDuration = averageDuration,
            TotalRecordsProcessed = totalRecords,
            LastExecutionTime = lastExecution?.StartTime
        };
    }

    private IQueryable<PipelineExecution> ApplySorting(IQueryable<PipelineExecution> query, string? sortBy)
    {
        return sortBy?.ToLower() switch
        {
            "starttime_asc" => query.OrderBy(e => e.StartTime),
            "starttime_desc" => query.OrderByDescending(e => e.StartTime),
            "status_asc" => query.OrderBy(e => e.Status),
            "status_desc" => query.OrderByDescending(e => e.Status),
            "duration_asc" => query.OrderBy(e => e.Duration),
            "duration_desc" => query.OrderByDescending(e => e.Duration),
            "records_asc" => query.OrderBy(e => e.RecordsProcessed),
            "records_desc" => query.OrderByDescending(e => e.RecordsProcessed),
            _ => query.OrderByDescending(e => e.StartTime) // Default sort
        };
    }

    private async Task<ExecutionResponse> MapToExecutionResponse(PipelineExecution execution, Pipeline? pipeline)
    {
        // Load logs from execution_logs table
        var logEntries = await _context.ExecutionLogs
            .Where(l => l.ExecutionId == execution.Id)
            .OrderBy(l => l.Timestamp)
            .ToListAsync();

        // Get triggered by user email if available
        string? triggeredByUserEmail = null;
        if (execution.TriggeredByUserId.HasValue)
        {
            var user = await _context.Users.FindAsync(execution.TriggeredByUserId.Value);
            triggeredByUserEmail = user?.Email;
        }

        return new ExecutionResponse
        {
            Id = execution.Id,
            PipelineId = execution.PipelineId,
            PipelineName = pipeline?.Name,
            TenantId = execution.TenantId,
            TenantName = execution.Tenant?.Name,
            Status = execution.Status.ToString(),
            StartTime = execution.StartTime,
            EndTime = execution.EndTime,
            Duration = execution.Duration,
            RecordsProcessed = execution.RecordsProcessed,
            RecordsSucceeded = execution.RecordsSucceeded,
            RecordsFailed = execution.RecordsFailed,
            ProgressPercent = execution.ProgressPercent,
            BatchCount = execution.BatchCount,
            ErrorMessage = execution.ErrorMessage,
            Logs = logEntries.Select(l => new ExecutionLogDto
            {
                Timestamp = l.Timestamp,
                Level = l.Level,
                Source = l.Source,
                Message = l.Message,
                Details = l.Details
            }).ToList(),
            TriggeredBy = execution.TriggeredBy,
            TriggeredByUserEmail = triggeredByUserEmail,
            CreatedAt = execution.CreatedAt
        };
    }
}
