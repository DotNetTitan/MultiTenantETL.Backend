using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Interfaces;
using MultiTenantETL.Application.Pipelines;
using MultiTenantETL.Application.Pipelines.Models;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Persistence;
using MultiTenantETL.Infrastructure.Services;
using NSubstitute;

namespace MultiTenantETL.UnitTests.Services;

public class PipelineServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PipelineService> _logger;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditService _auditService;
    private readonly ITenantProvider _tenantProvider;
    private readonly PipelineService _sut;

    public PipelineServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _tenantProvider = Substitute.For<ITenantProvider>();
        _context = new ApplicationDbContext(options, _tenantProvider);
        _logger = Substitute.For<ILogger<PipelineService>>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _auditService = Substitute.For<IAuditService>();

        _sut = new PipelineService(
            _context,
            _logger,
            _currentUserService,
            _auditService);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task CreateAsync_ValidInput_CreatesPipeline()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        
        _tenantProvider.TenantId.Returns(tenantId);
        _currentUserService.GetTenantId().Returns(tenantId);
        _currentUserService.GetUserId().Returns(userId);

        var sourceConnector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Source",
            Type = "Database",
            Provider = "PostgreSQL",
            Direction = "Source",
            IsSource = true,
            IsDestination = false,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = "{}",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        var destConnector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Dest",
            Type = "Database",
            Provider = "PostgreSQL",
            Direction = "Destination",
            IsSource = false,
            IsDestination = true,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = "{}",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        _context.Connectors.AddRange(sourceConnector, destConnector);
        await _context.SaveChangesAsync();

        var request = new CreatePipelineRequest
        {
            Name = "Test Pipeline",
            Description = "Test Description",
            SourceConnectorId = sourceConnector.Id,
            DestinationConnectorId = destConnector.Id,
            FieldMappings = JsonDocument.Parse("[]").RootElement,
            IsScheduled = false
        };

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Test Pipeline");
        result.SourceConnectorId.Should().Be(sourceConnector.Id);
        result.DestinationConnectorId.Should().Be(destConnector.Id);
        result.Status.Should().Be("Idle");

        // Verify database
        var pipeline = await _context.Pipelines.FirstOrDefaultAsync(p => p.Id == result.Id);
        pipeline.Should().NotBeNull();
        pipeline!.TenantId.Should().Be(tenantId);
        pipeline.CreatedBy.Should().Be(userId);

        // Verify audit log
        await _auditService.Received(1).LogAsync(
            Arg.Is<string>(s => s == AuditActions.Pipelines.Created),
            "Pipeline",
            result.Id.ToString(),
            Arg.Any<string>(),
            Arg.Any<object>());
    }

    [Fact]
    public async Task CreateAsync_SourceConnectorNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _currentUserService.GetTenantId().Returns(tenantId);

        var request = new CreatePipelineRequest
        {
            Name = "Test Pipeline",
            SourceConnectorId = Guid.NewGuid(), // Non-existent
            DestinationConnectorId = Guid.NewGuid(),
            FieldMappings = JsonDocument.Parse("[]").RootElement
        };

        // Act
        var act = async () => await _sut.CreateAsync(request);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Source connector with ID {request.SourceConnectorId} not found");
    }

    [Fact]
    public async Task CreateAsync_DestinationConnectorNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _tenantProvider.TenantId.Returns(tenantId);
        _currentUserService.GetTenantId().Returns(tenantId);

        var sourceConnector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Source",
            Type = "Database",
            Provider = "PostgreSQL",
            Direction = "Source",
            IsSource = true,
            IsDestination = false,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = "{}",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        _context.Connectors.Add(sourceConnector);
        await _context.SaveChangesAsync();

        var request = new CreatePipelineRequest
        {
            Name = "Test Pipeline",
            SourceConnectorId = sourceConnector.Id,
            DestinationConnectorId = Guid.NewGuid(), // Non-existent
            FieldMappings = JsonDocument.Parse("[]").RootElement
        };

        // Act
        var act = async () => await _sut.CreateAsync(request);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Destination connector with ID {request.DestinationConnectorId} not found");
    }

    [Fact]
    public async Task GetByIdAsync_ExistingPipeline_ReturnsPipeline()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var pipelineId = Guid.NewGuid();
        _tenantProvider.TenantId.Returns(tenantId);
        _currentUserService.GetTenantId().Returns(tenantId);

        var sourceConnector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Source",
            Type = "Database",
            Provider = "PostgreSQL",
            Direction = "Source",
            IsSource = true,
            IsDestination = false,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = "{}",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        var destConnector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Dest",
            Type = "Database",
            Provider = "PostgreSQL",
            Direction = "Destination",
            IsSource = false,
            IsDestination = true,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = "{}",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        _context.Connectors.AddRange(sourceConnector, destConnector);

        var pipeline = new Pipeline
        {
            Id = pipelineId,
            TenantId = tenantId,
            Name = "Existing Pipeline",
            SourceConnectorId = sourceConnector.Id,
            DestinationConnectorId = destConnector.Id,
            Status = "Idle",
            FieldMappingsJson = "[]",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        _context.Pipelines.Add(pipeline);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetByIdAsync(pipelineId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(pipelineId);
        result.Name.Should().Be("Existing Pipeline");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentPipeline_ThrowsKeyNotFoundException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _currentUserService.GetTenantId().Returns(tenantId);
        var pipelineId = Guid.NewGuid();

        // Act
        var act = async () => await _sut.GetByIdAsync(pipelineId);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_ValidInput_UpdatesPipeline()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var pipelineId = Guid.NewGuid();
        
        _tenantProvider.TenantId.Returns(tenantId);
        _currentUserService.GetTenantId().Returns(tenantId);
        _currentUserService.GetUserId().Returns(userId);

        var pipeline = new Pipeline
        {
            Id = pipelineId,
            TenantId = tenantId,
            Name = "Old Name",
            SourceConnectorId = Guid.NewGuid(),
            DestinationConnectorId = Guid.NewGuid(),
            Status = "Idle",
            FieldMappingsJson = "[]",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        _context.Pipelines.Add(pipeline);
        await _context.SaveChangesAsync();

        var request = new UpdatePipelineRequest
        {
            Name = "New Name",
            Description = "Updated Description",
            FieldMappings = JsonDocument.Parse("[]").RootElement,
            IsScheduled = true,
            IsActive = false
        };

        // Act
        var result = await _sut.UpdateAsync(pipelineId, request);

        // Assert
        result.Name.Should().Be("New Name");
        result.Description.Should().Be("Updated Description");
        result.IsScheduled.Should().BeTrue();
        result.IsActive.Should().BeFalse();

        // Verify database update
        var updatedPipeline = await _context.Pipelines.FindAsync(pipelineId);
        updatedPipeline!.Name.Should().Be("New Name");
        updatedPipeline.UpdatedBy.Should().Be(userId);
        
        // Verify audit log
        await _auditService.Received(1).LogAsync(
            Arg.Is<string>(s => s == AuditActions.Pipelines.Updated),
            "Pipeline",
            pipelineId.ToString(),
            Arg.Any<string>(),
            Arg.Any<object>());
    }

    [Fact]
    public async Task DeleteAsync_ExistingPipeline_RemovesPipeline()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var pipelineId = Guid.NewGuid();
        _tenantProvider.TenantId.Returns(tenantId);
        _currentUserService.GetTenantId().Returns(tenantId);

        var pipeline = new Pipeline
        {
            Id = pipelineId,
            TenantId = tenantId,
            Name = "To Delete",
            SourceConnectorId = Guid.NewGuid(),
            DestinationConnectorId = Guid.NewGuid(),
            Status = "Idle",
            FieldMappingsJson = "[]",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        _context.Pipelines.Add(pipeline);
        await _context.SaveChangesAsync();

        // Act
        await _sut.DeleteAsync(pipelineId);

        // Assert
        var deletedPipeline = await _context.Pipelines.FindAsync(pipelineId);
        deletedPipeline.Should().BeNull();

        // Verify audit log
        await _auditService.Received(1).LogAsync(
            Arg.Is<string>(s => s == AuditActions.Pipelines.Deleted),
            "Pipeline",
            pipelineId.ToString(),
            Arg.Any<string>(),
            Arg.Any<object>());
    }

    [Fact]
    public async Task ToggleStatusAsync_TogglesIsActive()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var pipelineId = Guid.NewGuid();
        _tenantProvider.TenantId.Returns(tenantId);
        _currentUserService.GetTenantId().Returns(tenantId);

        var pipeline = new Pipeline
        {
            Id = pipelineId,
            TenantId = tenantId,
            Name = "Toggle Me",
            SourceConnectorId = Guid.NewGuid(),
            DestinationConnectorId = Guid.NewGuid(),
            Status = "Idle",
            FieldMappingsJson = "[]",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        _context.Pipelines.Add(pipeline);
        await _context.SaveChangesAsync();

        // Act - Toggle to false
        var result1 = await _sut.ToggleStatusAsync(pipelineId);
        result1.IsActive.Should().BeFalse();

        // Act - Toggle back to true
        var result2 = await _sut.ToggleStatusAsync(pipelineId);
        result2.IsActive.Should().BeTrue();

        // Verify audit log (called twice)
        await _auditService.Received(1).LogAsync(
            Arg.Is<string>(s => s == AuditActions.Pipelines.Deactivated),
            "Pipeline",
            pipelineId.ToString(),
            Arg.Any<string>(),
            Arg.Any<object>());
            
        await _auditService.Received(1).LogAsync(
            Arg.Is<string>(s => s == AuditActions.Pipelines.Activated),
            "Pipeline",
            pipelineId.ToString(),
            Arg.Any<string>(),
            Arg.Any<object>());
    }
}
