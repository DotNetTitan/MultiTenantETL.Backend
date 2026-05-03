using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Interfaces;
using MultiTenantETL.Application.Scheduling.Models;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Persistence;
using MultiTenantETL.Infrastructure.Scheduling;
using NSubstitute;
using Quartz;
using Quartz.Impl;

namespace MultiTenantETL.UnitTests.Services;

public class ScheduleServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IScheduler _scheduler;
    private readonly ILogger<ScheduleService> _logger;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditService _auditService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ScheduleService _sut;

    public ScheduleServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _tenantProvider = Substitute.For<ITenantProvider>();
        _context = new ApplicationDbContext(options, _tenantProvider);
        _logger = Substitute.For<ILogger<ScheduleService>>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _auditService = Substitute.For<IAuditService>();
        
        // Setup Quartz scheduler mock
        _schedulerFactory = Substitute.For<ISchedulerFactory>();
        _scheduler = Substitute.For<IScheduler>();
        _schedulerFactory.GetScheduler(Arg.Any<CancellationToken>()).Returns(_scheduler);

        _sut = new ScheduleService(
            _context,
            _schedulerFactory,
            _logger,
            _currentUserService,
            _auditService);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private async Task<(Guid tenantId, Guid userId, Pipeline pipeline)> SetupTestDataAsync()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _tenantProvider.TenantId.Returns(tenantId);
        _currentUserService.GetTenantId().Returns(tenantId);
        _currentUserService.GetUserId().Returns(userId);

        var pipeline = new Pipeline
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Test Pipeline",
            SourceConnectorId = Guid.NewGuid(),
            DestinationConnectorId = Guid.NewGuid(),
            Status = "Idle",
            FieldMappingsJson = "[]",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        _context.Pipelines.Add(pipeline);
        await _context.SaveChangesAsync();

        return (tenantId, userId, pipeline);
    }

    [Fact]
    public async Task CreateAsync_ValidInput_CreatesSchedule()
    {
        // Arrange
        var (tenantId, userId, pipeline) = await SetupTestDataAsync();

        var request = new CreateScheduleRequest
        {
            PipelineId = pipeline.Id,
            CronExpression = "0 0 0 * * ?", // Quartz format: daily at midnight (sec min hour dayOfMonth month dayOfWeek)
            Timezone = "UTC",
            Description = "Daily schedule",
            IsActive = true
        };

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.PipelineId.Should().Be(pipeline.Id);
        result.CronExpression.Should().Be("0 0 0 * * ?");
        result.Timezone.Should().Be("UTC");
        result.IsActive.Should().BeTrue();
        result.NextRunAt.Should().NotBeNull();

        // Verify database
        var schedule = await _context.PipelineSchedules.FirstOrDefaultAsync(s => s.Id == result.Id);
        schedule.Should().NotBeNull();
        schedule!.TenantId.Should().Be(tenantId);
        schedule.CreatedBy.Should().Be(userId);

        // Verify audit log
        await _auditService.Received(1).LogAsync(
            Arg.Is<string>(s => s == AuditActions.Schedules.Created),
            "PipelineSchedule",
            result.Id.ToString(),
            Arg.Any<string>(),
            Arg.Any<object>());
    }

    [Fact]
    public async Task CreateAsync_PipelineNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _tenantProvider.TenantId.Returns(tenantId);
        _currentUserService.GetTenantId().Returns(tenantId);

        var request = new CreateScheduleRequest
        {
            PipelineId = Guid.NewGuid(), // Non-existent
            CronExpression = "0 0 * * *",
            Timezone = "UTC"
        };

        // Act
        var act = async () => await _sut.CreateAsync(request);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Pipeline with ID {request.PipelineId} not found");
    }

    [Fact]
    public async Task CreateAsync_ScheduleAlreadyExists_ThrowsInvalidOperationException()
    {
        // Arrange
        var (tenantId, userId, pipeline) = await SetupTestDataAsync();

        // Create existing schedule
        var existingSchedule = new PipelineSchedule
        {
            Id = Guid.NewGuid(),
            PipelineId = pipeline.Id,
            TenantId = tenantId,
            CronExpression = "0 0 0 * * ?",
            Timezone = "UTC",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };
        _context.PipelineSchedules.Add(existingSchedule);
        await _context.SaveChangesAsync();

        var request = new CreateScheduleRequest
        {
            PipelineId = pipeline.Id,
            CronExpression = "0 0 12 * * ?",
            Timezone = "UTC"
        };

        // Act
        var act = async () => await _sut.CreateAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"A schedule already exists for pipeline {pipeline.Id}*");
    }

    [Fact]
    public async Task CreateAsync_InvalidCronExpression_ThrowsArgumentException()
    {
        // Arrange
        var (_, _, pipeline) = await SetupTestDataAsync();

        var request = new CreateScheduleRequest
        {
            PipelineId = pipeline.Id,
            CronExpression = "invalid cron",
            Timezone = "UTC"
        };

        // Act
        var act = async () => await _sut.CreateAsync(request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateAsync_InvalidTimezone_ThrowsArgumentException()
    {
        // Arrange
        var (_, _, pipeline) = await SetupTestDataAsync();

        var request = new CreateScheduleRequest
        {
            PipelineId = pipeline.Id,
            CronExpression = "0 0 0 * * ?", // Valid Quartz cron
            Timezone = "Invalid/Timezone"
        };

        // Act
        var act = async () => await _sut.CreateAsync(request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Invalid timezone*");
    }

    [Fact]
    public async Task GetByIdAsync_ExistingSchedule_ReturnsSchedule()
    {
        // Arrange
        var (tenantId, userId, pipeline) = await SetupTestDataAsync();

        var schedule = new PipelineSchedule
        {
            Id = Guid.NewGuid(),
            PipelineId = pipeline.Id,
            TenantId = tenantId,
            CronExpression = "0 0 0 * * ?",
            Timezone = "UTC",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };
        _context.PipelineSchedules.Add(schedule);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetByIdAsync(schedule.Id);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(schedule.Id);
        result.CronExpression.Should().Be("0 0 0 * * ?");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentSchedule_ThrowsKeyNotFoundException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _tenantProvider.TenantId.Returns(tenantId);
        _currentUserService.GetTenantId().Returns(tenantId);

        // Act
        var act = async () => await _sut.GetByIdAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetByPipelineIdAsync_ExistingSchedule_ReturnsSchedule()
    {
        // Arrange
        var (tenantId, userId, pipeline) = await SetupTestDataAsync();

        var schedule = new PipelineSchedule
        {
            Id = Guid.NewGuid(),
            PipelineId = pipeline.Id,
            TenantId = tenantId,
            CronExpression = "0 0 0 * * ?",
            Timezone = "UTC",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };
        _context.PipelineSchedules.Add(schedule);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetByPipelineIdAsync(pipeline.Id);

        // Assert
        result.Should().NotBeNull();
        result!.PipelineId.Should().Be(pipeline.Id);
    }

    [Fact]
    public async Task GetByPipelineIdAsync_NoSchedule_ReturnsNull()
    {
        // Arrange
        var (_, _, pipeline) = await SetupTestDataAsync();

        // Act
        var result = await _sut.GetByPipelineIdAsync(pipeline.Id);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_ExistingSchedule_RemovesSchedule()
    {
        // Arrange
        var (tenantId, userId, pipeline) = await SetupTestDataAsync();

        var schedule = new PipelineSchedule
        {
            Id = Guid.NewGuid(),
            PipelineId = pipeline.Id,
            TenantId = tenantId,
            CronExpression = "0 0 0 * * ?",
            Timezone = "UTC",
            IsActive = true,
            QuartzJobKey = $"pipeline-{pipeline.Id}-test",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };
        _context.PipelineSchedules.Add(schedule);
        await _context.SaveChangesAsync();

        // Act
        await _sut.DeleteAsync(schedule.Id);

        // Assert
        var deletedSchedule = await _context.PipelineSchedules.FindAsync(schedule.Id);
        deletedSchedule.Should().BeNull();

        // Verify audit log
        await _auditService.Received(1).LogAsync(
            Arg.Is<string>(s => s == AuditActions.Schedules.Deleted),
            "PipelineSchedule",
            schedule.Id.ToString(),
            Arg.Any<string>(),
            Arg.Any<object>());
    }

    [Fact]
    public async Task EnableAsync_DisabledSchedule_EnablesSchedule()
    {
        // Arrange
        var (tenantId, userId, pipeline) = await SetupTestDataAsync();

        var schedule = new PipelineSchedule
        {
            Id = Guid.NewGuid(),
            PipelineId = pipeline.Id,
            TenantId = tenantId,
            CronExpression = "0 0 0 * * ?", // Quartz format
            Timezone = "UTC",
            IsActive = false, // Disabled
            ConsecutiveFailures = 3,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };
        _context.PipelineSchedules.Add(schedule);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.EnableAsync(schedule.Id);

        // Assert
        result.IsActive.Should().BeTrue();
        result.ConsecutiveFailures.Should().Be(0); // Should be reset

        // Verify database
        var updatedSchedule = await _context.PipelineSchedules.FindAsync(schedule.Id);
        updatedSchedule!.IsActive.Should().BeTrue();
        updatedSchedule.ConsecutiveFailures.Should().Be(0);

        // Verify audit log
        await _auditService.Received(1).LogAsync(
            Arg.Is<string>(s => s == AuditActions.Schedules.Enabled),
            "PipelineSchedule",
            schedule.Id.ToString(),
            Arg.Any<string>(),
            Arg.Any<object>());
    }

    [Fact]
    public async Task DisableAsync_EnabledSchedule_DisablesSchedule()
    {
        // Arrange
        var (tenantId, userId, pipeline) = await SetupTestDataAsync();

        var schedule = new PipelineSchedule
        {
            Id = Guid.NewGuid(),
            PipelineId = pipeline.Id,
            TenantId = tenantId,
            CronExpression = "0 0 0 * * ?",
            Timezone = "UTC",
            IsActive = true, // Enabled
            QuartzJobKey = $"pipeline-{pipeline.Id}-test",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };
        _context.PipelineSchedules.Add(schedule);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.DisableAsync(schedule.Id);

        // Assert
        result.IsActive.Should().BeFalse();

        // Verify database
        var updatedSchedule = await _context.PipelineSchedules.FindAsync(schedule.Id);
        updatedSchedule!.IsActive.Should().BeFalse();

        // Verify audit log
        await _auditService.Received(1).LogAsync(
            Arg.Is<string>(s => s == AuditActions.Schedules.Disabled),
            "PipelineSchedule",
            schedule.Id.ToString(),
            Arg.Any<string>(),
            Arg.Any<object>());
    }

    [Fact]
    public async Task ValidateCronExpressionAsync_ValidCron_ReturnsValidResult()
    {
        // Arrange & Act - Use Quartz format (6 fields: sec min hour dayOfMonth month dayOfWeek)
        var result = await _sut.ValidateCronExpressionAsync("0 0 0 * * ?", "UTC");

        // Assert
        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.NextExecutions.Should().NotBeEmpty();
        result.NextExecutions.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task ValidateCronExpressionAsync_InvalidCron_ReturnsInvalidResult()
    {
        // Arrange & Act
        var result = await _sut.ValidateCronExpressionAsync("invalid cron", "UTC");

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ValidateCronExpressionAsync_InvalidTimezone_ReturnsInvalidResult()
    {
        // Arrange & Act - Valid Quartz cron but invalid timezone
        var result = await _sut.ValidateCronExpressionAsync("0 0 0 * * ?", "Invalid/Timezone");

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid timezone");
    }

    [Fact]
    public async Task UpdateAsync_ValidInput_UpdatesSchedule()
    {
        // Arrange
        var (tenantId, userId, pipeline) = await SetupTestDataAsync();

        var schedule = new PipelineSchedule
        {
            Id = Guid.NewGuid(),
            PipelineId = pipeline.Id,
            TenantId = tenantId,
            CronExpression = "0 0 0 * * ?",
            Timezone = "UTC",
            Description = "Old description",
            IsActive = true,
            QuartzJobKey = $"pipeline-{pipeline.Id}-test",
            QuartzTriggerKey = $"trigger-{pipeline.Id}-test",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };
        _context.PipelineSchedules.Add(schedule);
        await _context.SaveChangesAsync();

        var request = new UpdateScheduleRequest
        {
            CronExpression = "0 0 12 * * ?", // Changed to noon
            Timezone = "UTC",
            Description = "New description",
            IsActive = true
        };

        // Act
        var result = await _sut.UpdateAsync(schedule.Id, request);

        // Assert
        result.CronExpression.Should().Be("0 0 12 * * ?");
        result.Description.Should().Be("New description");

        // Verify database
        var updatedSchedule = await _context.PipelineSchedules.FindAsync(schedule.Id);
        updatedSchedule!.CronExpression.Should().Be("0 0 12 * * ?");
        updatedSchedule.UpdatedBy.Should().Be(userId);

        // Verify audit log
        await _auditService.Received(1).LogAsync(
            Arg.Is<string>(s => s == AuditActions.Schedules.Updated),
            "PipelineSchedule",
            schedule.Id.ToString(),
            Arg.Any<string>(),
            Arg.Any<object>());
    }

    [Fact]
    public async Task GetAllAsync_MultipleSchedules_ReturnsPagedResults()
    {
        // Arrange
        var (tenantId, userId, pipeline1) = await SetupTestDataAsync();

        // Create a second pipeline
        var pipeline2 = new Pipeline
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Test Pipeline 2",
            SourceConnectorId = Guid.NewGuid(),
            DestinationConnectorId = Guid.NewGuid(),
            Status = "Idle",
            FieldMappingsJson = "[]",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };
        _context.Pipelines.Add(pipeline2);

        // Create schedules for both pipelines
        var schedule1 = new PipelineSchedule
        {
            Id = Guid.NewGuid(),
            PipelineId = pipeline1.Id,
            TenantId = tenantId,
            CronExpression = "0 0 0 * * ?",
            Timezone = "UTC",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };
        var schedule2 = new PipelineSchedule
        {
            Id = Guid.NewGuid(),
            PipelineId = pipeline2.Id,
            TenantId = tenantId,
            CronExpression = "0 0 12 * * ?",
            Timezone = "UTC",
            IsActive = false,
            CreatedAt = DateTime.UtcNow.AddMinutes(1),
            CreatedBy = userId
        };

        _context.PipelineSchedules.AddRange(schedule1, schedule2);
        await _context.SaveChangesAsync();

        var request = new ScheduleSearchRequest
        {
            Page = 1,
            PageSize = 10
        };

        // Act
        var result = await _sut.GetAllAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Schedules.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task GetAllAsync_FilterByActive_ReturnsFilteredResults()
    {
        // Arrange
        var (tenantId, userId, pipeline1) = await SetupTestDataAsync();

        var pipeline2 = new Pipeline
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Test Pipeline 2",
            SourceConnectorId = Guid.NewGuid(),
            DestinationConnectorId = Guid.NewGuid(),
            Status = "Idle",
            FieldMappingsJson = "[]",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };
        _context.Pipelines.Add(pipeline2);

        var activeSchedule = new PipelineSchedule
        {
            Id = Guid.NewGuid(),
            PipelineId = pipeline1.Id,
            TenantId = tenantId,
            CronExpression = "0 0 0 * * ?",
            Timezone = "UTC",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };
        var inactiveSchedule = new PipelineSchedule
        {
            Id = Guid.NewGuid(),
            PipelineId = pipeline2.Id,
            TenantId = tenantId,
            CronExpression = "0 0 12 * * ?",
            Timezone = "UTC",
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        _context.PipelineSchedules.AddRange(activeSchedule, inactiveSchedule);
        await _context.SaveChangesAsync();

        var request = new ScheduleSearchRequest
        {
            IsActive = true,
            Page = 1,
            PageSize = 10
        };

        // Act
        var result = await _sut.GetAllAsync(request);

        // Assert
        result.Schedules.Should().HaveCount(1);
        result.Schedules[0].IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task TenantIsolation_ScheduleFromDifferentTenant_NotAccessible()
    {
        // Arrange
        var tenantId1 = Guid.NewGuid();
        var tenantId2 = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Setup tenant 2's context
        _tenantProvider.TenantId.Returns(tenantId2);
        _currentUserService.GetTenantId().Returns(tenantId2);
        _currentUserService.GetUserId().Returns(userId);

        // Create a schedule in tenant 1 (different tenant)
        var pipeline = new Pipeline
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId1,
            Name = "Tenant 1 Pipeline",
            SourceConnectorId = Guid.NewGuid(),
            DestinationConnectorId = Guid.NewGuid(),
            Status = "Idle",
            FieldMappingsJson = "[]",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };
        _context.Pipelines.Add(pipeline);

        var schedule = new PipelineSchedule
        {
            Id = Guid.NewGuid(),
            PipelineId = pipeline.Id,
            TenantId = tenantId1, // Different tenant
            CronExpression = "0 0 0 * * ?",
            Timezone = "UTC",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };
        _context.PipelineSchedules.Add(schedule);
        await _context.SaveChangesAsync();

        // Act - Try to access from tenant 2
        var act = async () => await _sut.GetByIdAsync(schedule.Id);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task PauseSchedulesForPipelineAsync_ActiveSchedule_SetsIsActiveToFalse()
    {
        // Arrange
        var (tenantId, userId, pipeline) = await SetupTestDataAsync();

        // Setup Quartz mock to report job exists
        _scheduler.CheckExists(Arg.Any<JobKey>(), Arg.Any<CancellationToken>()).Returns(true);

        var schedule = new PipelineSchedule
        {
            Id = Guid.NewGuid(),
            PipelineId = pipeline.Id,
            TenantId = tenantId,
            CronExpression = "0 0 0 * * ?",
            Timezone = "UTC",
            IsActive = true,
            QuartzJobKey = $"pipeline-{pipeline.Id}-test",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };
        _context.PipelineSchedules.Add(schedule);
        await _context.SaveChangesAsync();

        // Act
        await _sut.PauseSchedulesForPipelineAsync(pipeline.Id);

        // Assert
        var pausedSchedule = await _context.PipelineSchedules.FindAsync(schedule.Id);
        pausedSchedule.Should().NotBeNull();
        pausedSchedule!.IsActive.Should().BeFalse();
        pausedSchedule.IsPausedByPipeline.Should().BeTrue();
        pausedSchedule.UpdatedAt.Should().NotBeNull();

        // Verify Quartz job was deleted
        await _scheduler.Received(1).DeleteJob(Arg.Any<JobKey>(), Arg.Any<CancellationToken>());

        // Verify audit log
        await _auditService.Received(1).LogAsync(
            Arg.Is<string>(s => s == AuditActions.Schedules.PausedForPipeline),
            "Pipeline",
            pipeline.Id.ToString(),
            Arg.Any<string>(),
            Arg.Any<object>());
    }

    [Fact]
    public async Task PauseSchedulesForPipelineAsync_NoActiveSchedules_DoesNothing()
    {
        // Arrange
        var (tenantId, userId, pipeline) = await SetupTestDataAsync();

        var schedule = new PipelineSchedule
        {
            Id = Guid.NewGuid(),
            PipelineId = pipeline.Id,
            TenantId = tenantId,
            CronExpression = "0 0 0 * * ?",
            Timezone = "UTC",
            IsActive = false, // Already inactive
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };
        _context.PipelineSchedules.Add(schedule);
        await _context.SaveChangesAsync();

        // Act
        await _sut.PauseSchedulesForPipelineAsync(pipeline.Id);

        // Assert - no audit log should be called
        await _auditService.DidNotReceive().LogAsync(
            Arg.Is<string>(s => s == AuditActions.Schedules.PausedForPipeline),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<object>());
    }

    [Fact]
    public async Task ResumeSchedulesForPipelineAsync_PipelinePausedSchedule_SetsIsActiveToTrue()
    {
        // Arrange
        var (tenantId, userId, pipeline) = await SetupTestDataAsync();

        var schedule = new PipelineSchedule
        {
            Id = Guid.NewGuid(),
            PipelineId = pipeline.Id,
            TenantId = tenantId,
            CronExpression = "0 0 0 * * ?",
            Timezone = "UTC",
            IsActive = false, // Was paused
            IsPausedByPipeline = true,
            QuartzJobKey = $"pipeline-{pipeline.Id}-test",
            QuartzTriggerKey = $"trigger-{pipeline.Id}-test",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };
        _context.PipelineSchedules.Add(schedule);
        await _context.SaveChangesAsync();

        // Act
        await _sut.ResumeSchedulesForPipelineAsync(pipeline.Id);

        // Assert
        var resumedSchedule = await _context.PipelineSchedules.FindAsync(schedule.Id);
        resumedSchedule.Should().NotBeNull();
        resumedSchedule!.IsActive.Should().BeTrue();
        resumedSchedule.IsPausedByPipeline.Should().BeFalse();
        resumedSchedule.UpdatedAt.Should().NotBeNull();
        resumedSchedule.NextRunAt.Should().NotBeNull();

        // Verify audit log
        await _auditService.Received(1).LogAsync(
            Arg.Is<string>(s => s == AuditActions.Schedules.ResumedForPipeline),
            "Pipeline",
            pipeline.Id.ToString(),
            Arg.Any<string>(),
            Arg.Any<object>());
    }

    [Fact]
    public async Task ResumeSchedulesForPipelineAsync_NoSchedules_DoesNothing()
    {
        // Arrange
        var (_, _, pipeline) = await SetupTestDataAsync();

        // Act
        await _sut.ResumeSchedulesForPipelineAsync(pipeline.Id);

        // Assert - no audit log should be called
        await _auditService.DidNotReceive().LogAsync(
            Arg.Is<string>(s => s == AuditActions.Schedules.ResumedForPipeline),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<object>());
    }

    [Fact]
    public async Task ResumeSchedulesForPipelineAsync_ManualDisabledSchedule_RemainsInactive()
    {
        // Arrange
        var (tenantId, userId, pipeline) = await SetupTestDataAsync();

        var schedule = new PipelineSchedule
        {
            Id = Guid.NewGuid(),
            PipelineId = pipeline.Id,
            TenantId = tenantId,
            CronExpression = "0 0 0 * * ?",
            Timezone = "UTC",
            IsActive = false,
            IsPausedByPipeline = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };
        _context.PipelineSchedules.Add(schedule);
        await _context.SaveChangesAsync();

        // Act
        await _sut.ResumeSchedulesForPipelineAsync(pipeline.Id);

        // Assert
        var updatedSchedule = await _context.PipelineSchedules.FindAsync(schedule.Id);
        updatedSchedule.Should().NotBeNull();
        updatedSchedule!.IsActive.Should().BeFalse();
        updatedSchedule.IsPausedByPipeline.Should().BeFalse();

        await _auditService.DidNotReceive().LogAsync(
            Arg.Is<string>(s => s == AuditActions.Schedules.ResumedForPipeline),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<object>());
    }

    [Fact]
    public async Task PauseAndResumeSchedules_RoundTrip_RestoresScheduleState()
    {
        // Arrange
        var (tenantId, userId, pipeline) = await SetupTestDataAsync();

        // Setup Quartz mock to report job exists
        _scheduler.CheckExists(Arg.Any<JobKey>(), Arg.Any<CancellationToken>()).Returns(true);

        var schedule = new PipelineSchedule
        {
            Id = Guid.NewGuid(),
            PipelineId = pipeline.Id,
            TenantId = tenantId,
            CronExpression = "0 0 0 * * ?",
            Timezone = "UTC",
            IsActive = true,
            QuartzJobKey = $"pipeline-{pipeline.Id}-test",
            QuartzTriggerKey = $"trigger-{pipeline.Id}-test",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };
        _context.PipelineSchedules.Add(schedule);
        await _context.SaveChangesAsync();

        // Act - Pause the schedule
        await _sut.PauseSchedulesForPipelineAsync(pipeline.Id);

        // Verify schedule is inactive
        var pausedSchedule = await _context.PipelineSchedules.FindAsync(schedule.Id);
        pausedSchedule!.IsActive.Should().BeFalse();
        pausedSchedule.IsPausedByPipeline.Should().BeTrue();

        // Act - Resume the schedule
        await _sut.ResumeSchedulesForPipelineAsync(pipeline.Id);

        // Assert - Schedule should be active again
        var resumedSchedule = await _context.PipelineSchedules.FindAsync(schedule.Id);
        resumedSchedule!.IsActive.Should().BeTrue();
        resumedSchedule.IsPausedByPipeline.Should().BeFalse();
    }

    [Fact]
    public async Task DisableAsync_PipelinePausedSchedule_ClearsPauseFlag()
    {
        // Arrange
        var (tenantId, userId, pipeline) = await SetupTestDataAsync();

        var schedule = new PipelineSchedule
        {
            Id = Guid.NewGuid(),
            PipelineId = pipeline.Id,
            TenantId = tenantId,
            CronExpression = "0 0 0 * * ?",
            Timezone = "UTC",
            IsActive = false,
            IsPausedByPipeline = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };
        _context.PipelineSchedules.Add(schedule);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.DisableAsync(schedule.Id);

        // Assert
        result.IsActive.Should().BeFalse();

        var updatedSchedule = await _context.PipelineSchedules.FindAsync(schedule.Id);
        updatedSchedule.Should().NotBeNull();
        updatedSchedule!.IsActive.Should().BeFalse();
        updatedSchedule.IsPausedByPipeline.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_DeactivatedPipeline_ThrowsInvalidOperationException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _tenantProvider.TenantId.Returns(tenantId);
        _currentUserService.GetTenantId().Returns(tenantId);
        _currentUserService.GetUserId().Returns(userId);

        // Create a deactivated pipeline
        var pipeline = new Pipeline
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Deactivated Pipeline",
            SourceConnectorId = Guid.NewGuid(),
            DestinationConnectorId = Guid.NewGuid(),
            Status = "Idle",
            FieldMappingsJson = "[]",
            IsActive = false, // Pipeline is deactivated
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        _context.Pipelines.Add(pipeline);
        await _context.SaveChangesAsync();

        var request = new CreateScheduleRequest
        {
            PipelineId = pipeline.Id,
            CronExpression = "0 0 0 * * ?",
            Timezone = "UTC",
            IsActive = true
        };

        // Act
        var act = async () => await _sut.CreateAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Cannot create schedule for pipeline {pipeline.Id} because it is deactivated");
    }

    [Fact]
    public async Task EnableAsync_DeactivatedPipeline_ThrowsInvalidOperationException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _tenantProvider.TenantId.Returns(tenantId);
        _currentUserService.GetTenantId().Returns(tenantId);
        _currentUserService.GetUserId().Returns(userId);

        // Create a deactivated pipeline
        var pipeline = new Pipeline
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Deactivated Pipeline",
            SourceConnectorId = Guid.NewGuid(),
            DestinationConnectorId = Guid.NewGuid(),
            Status = "Idle",
            FieldMappingsJson = "[]",
            IsActive = false, // Pipeline is deactivated
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        _context.Pipelines.Add(pipeline);

        var schedule = new PipelineSchedule
        {
            Id = Guid.NewGuid(),
            PipelineId = pipeline.Id,
            TenantId = tenantId,
            CronExpression = "0 0 0 * * ?",
            Timezone = "UTC",
            IsActive = false, // Schedule is disabled
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };
        _context.PipelineSchedules.Add(schedule);
        await _context.SaveChangesAsync();

        // Act
        var act = async () => await _sut.EnableAsync(schedule.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Cannot enable schedule because pipeline 'Deactivated Pipeline' is deactivated");
    }

    [Fact]
    public async Task UpdateAsync_ActivateScheduleForDeactivatedPipeline_ThrowsInvalidOperationException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _tenantProvider.TenantId.Returns(tenantId);
        _currentUserService.GetTenantId().Returns(tenantId);
        _currentUserService.GetUserId().Returns(userId);

        // Create a deactivated pipeline
        var pipeline = new Pipeline
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Deactivated Pipeline",
            SourceConnectorId = Guid.NewGuid(),
            DestinationConnectorId = Guid.NewGuid(),
            Status = "Idle",
            FieldMappingsJson = "[]",
            IsActive = false, // Pipeline is deactivated
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        _context.Pipelines.Add(pipeline);

        var schedule = new PipelineSchedule
        {
            Id = Guid.NewGuid(),
            PipelineId = pipeline.Id,
            TenantId = tenantId,
            CronExpression = "0 0 0 * * ?",
            Timezone = "UTC",
            IsActive = false, // Schedule is disabled
            QuartzJobKey = $"pipeline-{pipeline.Id}-test",
            QuartzTriggerKey = $"trigger-{pipeline.Id}-test",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };
        _context.PipelineSchedules.Add(schedule);
        await _context.SaveChangesAsync();

        var request = new UpdateScheduleRequest
        {
            CronExpression = "0 0 12 * * ?",
            Timezone = "UTC",
            IsActive = true // Trying to activate
        };

        // Act
        var act = async () => await _sut.UpdateAsync(schedule.Id, request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Cannot activate schedule because pipeline 'Deactivated Pipeline' is deactivated");
    }

    [Fact]
    public async Task UpdateAsync_DeactivateScheduleForDeactivatedPipeline_Succeeds()
    {
        // Arrange - Even if pipeline is deactivated, we should be able to deactivate the schedule
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _tenantProvider.TenantId.Returns(tenantId);
        _currentUserService.GetTenantId().Returns(tenantId);
        _currentUserService.GetUserId().Returns(userId);

        // Create a deactivated pipeline
        var pipeline = new Pipeline
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Deactivated Pipeline",
            SourceConnectorId = Guid.NewGuid(),
            DestinationConnectorId = Guid.NewGuid(),
            Status = "Idle",
            FieldMappingsJson = "[]",
            IsActive = false, // Pipeline is deactivated
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        _context.Pipelines.Add(pipeline);

        var schedule = new PipelineSchedule
        {
            Id = Guid.NewGuid(),
            PipelineId = pipeline.Id,
            TenantId = tenantId,
            CronExpression = "0 0 0 * * ?",
            Timezone = "UTC",
            IsActive = true, // Schedule is currently active
            QuartzJobKey = $"pipeline-{pipeline.Id}-test",
            QuartzTriggerKey = $"trigger-{pipeline.Id}-test",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };
        _context.PipelineSchedules.Add(schedule);
        await _context.SaveChangesAsync();

        var request = new UpdateScheduleRequest
        {
            CronExpression = "0 0 12 * * ?",
            Timezone = "UTC",
            IsActive = false // Deactivating the schedule - should be allowed
        };

        // Act
        var result = await _sut.UpdateAsync(schedule.Id, request);

        // Assert
        result.IsActive.Should().BeFalse();
    }
}
