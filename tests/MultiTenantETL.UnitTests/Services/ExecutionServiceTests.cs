using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Executions;
using MultiTenantETL.Application.Executions.Models;
using MultiTenantETL.Application.Messaging;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Domain.Enums;
using MultiTenantETL.Infrastructure.Persistence;
using MultiTenantETL.Infrastructure.Services;
using NSubstitute;

namespace MultiTenantETL.UnitTests.Services;

public class ExecutionServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ExecutionService> _logger;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ITenantProvider _tenantProvider;
    private readonly ExecutionService _sut;

    public ExecutionServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _tenantProvider = Substitute.For<ITenantProvider>();
        _context = new ApplicationDbContext(options, _tenantProvider);
        _logger = Substitute.For<ILogger<ExecutionService>>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _messagePublisher = Substitute.For<IMessagePublisher>();

        _sut = new ExecutionService(
            _context,
            _currentUserService,
            _messagePublisher,
            _logger);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task StartExecutionAsync_ValidPipeline_CreatesExecutionAndPublishesMessage()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var pipelineId = Guid.NewGuid();
        
        _tenantProvider.TenantId.Returns(tenantId);
        _currentUserService.GetTenantId().Returns(tenantId);

        var tenant = new Tenant { Id = tenantId, Name = "Test Tenant", Slug = "test", IsActive = true, CreatedAt = DateTime.UtcNow };
        _context.Tenants.Add(tenant);

        var pipeline = new Pipeline
        {
            Id = pipelineId,
            TenantId = tenantId,
            Name = "Test Pipeline",
            SourceConnectorId = Guid.NewGuid(),
            DestinationConnectorId = Guid.NewGuid(),
            Status = "Idle",
            FieldMappingsJson = "[]",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId,
            Tenant = tenant
        };

        _context.Pipelines.Add(pipeline);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.StartExecutionAsync(pipelineId, "Manual", userId);

        // Assert
        result.Should().NotBeNull();
        result.PipelineId.Should().Be(pipelineId);
        result.Status.Should().Be(ExecutionStatus.Queued.ToString());
        result.TriggeredBy.Should().Be("Manual");

        // Verify database
        var execution = await _context.PipelineExecutions.FirstOrDefaultAsync(e => e.Id == result.Id);
        execution.Should().NotBeNull();
        execution!.TenantId.Should().Be(tenantId);
        execution.PipelineId.Should().Be(pipelineId);

        // Verify message published
        await _messagePublisher.Received(1).PublishExecutionTaskAsync(
            Arg.Is<ExecutionTask>(t => t.PipelineId == pipelineId && t.ExecutionId == result.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartExecutionAsync_PipelineNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var pipelineId = Guid.NewGuid();

        // Act
        var act = async () => await _sut.StartExecutionAsync(pipelineId, "Manual", null);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Pipeline with ID {pipelineId} not found");
    }

    [Fact]
    public async Task StartExecutionAsync_InactivePipeline_ThrowsInvalidOperationException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var pipelineId = Guid.NewGuid();
        _tenantProvider.TenantId.Returns(tenantId);
        _currentUserService.GetTenantId().Returns(tenantId);

        var tenant = new Tenant { Id = tenantId, Name = "Test Tenant", Slug = "test", IsActive = true, CreatedAt = DateTime.UtcNow };
        _context.Tenants.Add(tenant);

        var pipeline = new Pipeline
        {
            Id = pipelineId,
            TenantId = tenantId,
            Name = "Inactive Pipeline",
            SourceConnectorId = Guid.NewGuid(),
            DestinationConnectorId = Guid.NewGuid(),
            Status = "Idle",
            FieldMappingsJson = "[]",
            IsActive = false, // Inactive
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            Tenant = tenant
        };

        _context.Pipelines.Add(pipeline);
        await _context.SaveChangesAsync();

        // Act
        var act = async () => await _sut.StartExecutionAsync(pipelineId, "Manual", null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot execute an inactive pipeline");
    }

    [Fact]
    public async Task StartExecutionAsync_WrongTenant_ThrowsKeyNotFoundException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var pipelineId = Guid.NewGuid();
        
        _tenantProvider.TenantId.Returns(otherTenantId);
        _currentUserService.GetTenantId().Returns(tenantId);

        var tenant = new Tenant { Id = tenantId, Name = "Test Tenant", Slug = "test", IsActive = true, CreatedAt = DateTime.UtcNow };
        var otherTenant = new Tenant { Id = otherTenantId, Name = "Other Tenant", Slug = "other", IsActive = true, CreatedAt = DateTime.UtcNow };
        _context.Tenants.AddRange(tenant, otherTenant);

        var pipeline = new Pipeline
        {
            Id = pipelineId,
            TenantId = otherTenantId, // Different tenant
            Name = "Other Tenant Pipeline",
            SourceConnectorId = Guid.NewGuid(),
            DestinationConnectorId = Guid.NewGuid(),
            Status = "Idle",
            FieldMappingsJson = "[]",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            Tenant = otherTenant
        };

        _context.Pipelines.Add(pipeline);
        await _context.SaveChangesAsync();
        
        // Switch tenant context for the query (service will use different tenant than inserted pipeline)
        // With tenant query filters, the pipeline won't be found - this is the correct tenant isolation behavior
        _tenantProvider.TenantId.Returns(tenantId);

        // Act
        var act = async () => await _sut.StartExecutionAsync(pipelineId, "Manual", null);

        // Assert - With tenant query filters, pipelines from other tenants are not found
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Pipeline with ID {pipelineId} not found");
    }

    [Fact]
    public async Task CancelExecutionAsync_RunningExecution_UpdatesStatusAndPublishesMessage()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var executionId = Guid.NewGuid();
        var pipelineId = Guid.NewGuid();
        
        _tenantProvider.TenantId.Returns(tenantId);
        _currentUserService.GetTenantId().Returns(tenantId);

        var pipeline = new Pipeline
        {
            Id = pipelineId,
            TenantId = tenantId,
            Name = "Test Pipeline",
            SourceConnectorId = Guid.NewGuid(),
            DestinationConnectorId = Guid.NewGuid(),
            Status = "Running",
            FieldMappingsJson = "[]",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        var execution = new PipelineExecution
        {
            Id = executionId,
            PipelineId = pipelineId,
            TenantId = tenantId,
            Status = ExecutionStatus.Running,
            StartTime = DateTimeOffset.UtcNow,
            TriggeredBy = "Manual",
            CreatedAt = DateTimeOffset.UtcNow,
            Pipeline = pipeline
        };

        _context.Pipelines.Add(pipeline);
        _context.PipelineExecutions.Add(execution);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.CancelExecutionAsync(executionId);

        // Assert
        result.Status.Should().Be(ExecutionStatus.Cancelled.ToString());

        // Verify database
        var updatedExecution = await _context.PipelineExecutions.FindAsync(executionId);
        updatedExecution!.Status.Should().Be(ExecutionStatus.Cancelled);
        updatedExecution.EndTime.Should().NotBeNull();

        // Verify message published
        await _messagePublisher.Received(1).PublishCancellationRequestAsync(
            executionId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelExecutionAsync_CompletedExecution_ThrowsInvalidOperationException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var executionId = Guid.NewGuid();
        var pipelineId = Guid.NewGuid();
        
        _tenantProvider.TenantId.Returns(tenantId);
        _currentUserService.GetTenantId().Returns(tenantId);

        var pipeline = new Pipeline
        {
            Id = pipelineId,
            TenantId = tenantId,
            Name = "Test Pipeline",
            SourceConnectorId = Guid.NewGuid(),
            DestinationConnectorId = Guid.NewGuid(),
            Status = "Idle",
            FieldMappingsJson = "[]",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        var execution = new PipelineExecution
        {
            Id = executionId,
            PipelineId = pipelineId,
            TenantId = tenantId,
            Status = ExecutionStatus.Completed, // Already completed
            StartTime = DateTimeOffset.UtcNow,
            TriggeredBy = "Manual",
            CreatedAt = DateTimeOffset.UtcNow,
            Pipeline = pipeline
        };

        _context.Pipelines.Add(pipeline);
        _context.PipelineExecutions.Add(execution);
        await _context.SaveChangesAsync();

        // Act
        var act = async () => await _sut.CancelExecutionAsync(executionId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Cannot cancel execution with status: {ExecutionStatus.Completed}");
    }

    [Fact]
    public async Task GetByIdAsync_ExistingExecution_ReturnsExecution()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var executionId = Guid.NewGuid();
        var pipelineId = Guid.NewGuid();
        
        _tenantProvider.TenantId.Returns(tenantId);
        _currentUserService.GetTenantId().Returns(tenantId);

        var tenant = new Tenant { Id = tenantId, Name = "Test Tenant", Slug = "test", IsActive = true, CreatedAt = DateTime.UtcNow };
        _context.Tenants.Add(tenant);

        var pipeline = new Pipeline
        {
            Id = pipelineId,
            TenantId = tenantId,
            Name = "Test Pipeline",
            SourceConnectorId = Guid.NewGuid(),
            DestinationConnectorId = Guid.NewGuid(),
            Status = "Idle",
            FieldMappingsJson = "[]",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            Tenant = tenant
        };

        var execution = new PipelineExecution
        {
            Id = executionId,
            PipelineId = pipelineId,
            TenantId = tenantId,
            Status = ExecutionStatus.Completed,
            StartTime = DateTimeOffset.UtcNow,
            TriggeredBy = "Manual",
            CreatedAt = DateTimeOffset.UtcNow,
            Pipeline = pipeline,
            Tenant = tenant
        };

        _context.Pipelines.Add(pipeline);
        _context.PipelineExecutions.Add(execution);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetByIdAsync(executionId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(executionId);
        result.PipelineName.Should().Be("Test Pipeline");
    }
}
