using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Interfaces;
using MultiTenantETL.Application.Pipelines;
using MultiTenantETL.Application.Pipelines.Models;
using MultiTenantETL.Application.Scheduling;
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
    private readonly IScheduleService _scheduleService;
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
        _scheduleService = Substitute.For<IScheduleService>();

        _sut = new PipelineService(
            _context,
            _logger,
            _currentUserService,
            _auditService,
            _scheduleService);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region Helper Methods

    private Task<(Guid tenantId, Guid userId)> SetupTenantAsync()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _tenantProvider.TenantId.Returns(tenantId);
        _currentUserService.GetTenantId().Returns(tenantId);
        _currentUserService.GetUserId().Returns(userId);

        return Task.FromResult((tenantId, userId));
    }

    private (Connector source, Connector destination) CreateConnectorPair(Guid tenantId, Guid userId)
    {
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

        return (sourceConnector, destConnector);
    }

    #endregion

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
            FieldMappings = JsonDocument.Parse("[]").RootElement
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
            IsActive = false
        };

        // Act
        var result = await _sut.UpdateAsync(pipelineId, request);

        // Assert
        result.Name.Should().Be("New Name");
        result.Description.Should().Be("Updated Description");
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
        
        // Verify schedule service was called
        await _scheduleService.Received(1).PauseSchedulesForPipelineAsync(pipelineId, Arg.Any<CancellationToken>());
        await _scheduleService.Received(1).ResumeSchedulesForPipelineAsync(pipelineId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ToggleStatusAsync_WhenDeactivating_PausesSchedules()
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
            Name = "Active Pipeline",
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

        // Act - Deactivate the pipeline
        var result = await _sut.ToggleStatusAsync(pipelineId);

        // Assert
        result.IsActive.Should().BeFalse();
        await _scheduleService.Received(1).PauseSchedulesForPipelineAsync(pipelineId, Arg.Any<CancellationToken>());
        await _scheduleService.DidNotReceive().ResumeSchedulesForPipelineAsync(pipelineId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ToggleStatusAsync_WhenActivating_ResumesSchedules()
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
            Name = "Inactive Pipeline",
            SourceConnectorId = Guid.NewGuid(),
            DestinationConnectorId = Guid.NewGuid(),
            Status = "Idle",
            FieldMappingsJson = "[]",
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        _context.Pipelines.Add(pipeline);
        await _context.SaveChangesAsync();

        // Act - Activate the pipeline
        var result = await _sut.ToggleStatusAsync(pipelineId);

        // Assert
        result.IsActive.Should().BeTrue();
        await _scheduleService.Received(1).ResumeSchedulesForPipelineAsync(pipelineId, Arg.Any<CancellationToken>());
        await _scheduleService.DidNotReceive().PauseSchedulesForPipelineAsync(pipelineId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WithFrontendGeneratedIds_RegeneratesProperGuidIds()
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

        // Frontend-style field mappings with temporary IDs
        var fieldMappingsJson = @"[
            {
                ""id"": ""mapping-1701629000000-0.5"",
                ""sourceFields"": [""firstName""],
                ""destinationField"": ""first_name"",
                ""order"": 1,
                ""transformations"": [
                    {
                        ""id"": ""trans-1701629000000-0.123"",
                        ""type"": ""Trim"",
                        ""config"": {},
                        ""order"": 1,
                        ""isEnabled"": true
                    },
                    {
                        ""id"": ""trans-1701629000000-0.456"",
                        ""type"": ""CaseConvert"",
                        ""config"": {""caseType"": ""uppercase""},
                        ""order"": 2,
                        ""isEnabled"": true
                    }
                ]
            },
            {
                ""id"": ""mapping-1701629000001-0.789"",
                ""sourceFields"": [""lastName""],
                ""destinationField"": ""last_name"",
                ""order"": 2,
                ""transformations"": []
            }
        ]";

        var request = new CreatePipelineRequest
        {
            Name = "Test Pipeline with Transformations",
            Description = "Test Description",
            SourceConnectorId = sourceConnector.Id,
            DestinationConnectorId = destConnector.Id,
            FieldMappings = JsonDocument.Parse(fieldMappingsJson).RootElement
        };

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        result.Should().NotBeNull();
        
        // Get the stored pipeline from database
        var pipeline = await _context.Pipelines.FirstOrDefaultAsync(p => p.Id == result.Id);
        pipeline.Should().NotBeNull();

        // Parse the stored field mappings
        var storedMappings = JsonDocument.Parse(pipeline!.FieldMappingsJson).RootElement;
        storedMappings.GetArrayLength().Should().Be(2);

        // Check first mapping
        var firstMapping = storedMappings[0];
        var firstMappingId = firstMapping.GetProperty("id").GetString();
        firstMappingId.Should().NotBe("mapping-1701629000000-0.5"); // Should not be the frontend ID
        Guid.TryParse(firstMappingId, out _).Should().BeTrue(); // Should be a valid GUID
        firstMapping.GetProperty("destinationField").GetString().Should().Be("first_name");
        firstMapping.GetProperty("order").GetInt32().Should().Be(1);

        // Check transformations in first mapping
        var transformations = firstMapping.GetProperty("transformations");
        transformations.GetArrayLength().Should().Be(2);

        var firstTrans = transformations[0];
        var firstTransId = firstTrans.GetProperty("id").GetString();
        firstTransId.Should().NotBe("trans-1701629000000-0.123"); // Should not be the frontend ID
        Guid.TryParse(firstTransId, out _).Should().BeTrue(); // Should be a valid GUID
        firstTrans.GetProperty("type").GetString().Should().Be("Trim");
        firstTrans.GetProperty("isEnabled").GetBoolean().Should().BeTrue();

        var secondTrans = transformations[1];
        var secondTransId = secondTrans.GetProperty("id").GetString();
        secondTransId.Should().NotBe("trans-1701629000000-0.456");
        Guid.TryParse(secondTransId, out _).Should().BeTrue();
        secondTrans.GetProperty("type").GetString().Should().Be("CaseConvert");

        // Check second mapping
        var secondMapping = storedMappings[1];
        var secondMappingId = secondMapping.GetProperty("id").GetString();
        secondMappingId.Should().NotBe("mapping-1701629000001-0.789");
        Guid.TryParse(secondMappingId, out _).Should().BeTrue();
        secondMapping.GetProperty("destinationField").GetString().Should().Be("last_name");

        // Verify that the response also contains GUID IDs
        var responseMappings = result.FieldMappings;
        responseMappings.GetArrayLength().Should().Be(2);
        var responseFirstId = responseMappings[0].GetProperty("id").GetString();
        Guid.TryParse(responseFirstId, out _).Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_WithFrontendGeneratedIds_RegeneratesProperGuidIds()
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
            Name = "Original Name",
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

        // Frontend-style field mappings with temporary IDs
        var newFieldMappingsJson = @"[
            {
                ""id"": ""new-mapping-1701629999999-0.111"",
                ""sourceFields"": [""email""],
                ""destinationField"": ""email_address"",
                ""order"": 1,
                ""transformations"": [
                    {
                        ""id"": ""new-trans-1701629999999-0.222"",
                        ""type"": ""Replace"",
                        ""config"": {""findPattern"": ""old"", ""replaceWith"": ""new""},
                        ""order"": 1,
                        ""isEnabled"": true
                    }
                ]
            }
        ]";

        var request = new UpdatePipelineRequest
        {
            Name = "Updated Pipeline",
            Description = "Updated Description",
            FieldMappings = JsonDocument.Parse(newFieldMappingsJson).RootElement,
            IsActive = true
        };

        // Act
        var result = await _sut.UpdateAsync(pipelineId, request);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Updated Pipeline");

        // Parse the response field mappings
        var responseMappings = result.FieldMappings;
        responseMappings.GetArrayLength().Should().Be(1);

        var mapping = responseMappings[0];
        var mappingId = mapping.GetProperty("id").GetString();
        mappingId.Should().NotBe("new-mapping-1701629999999-0.111");
        Guid.TryParse(mappingId, out _).Should().BeTrue();

        var transformations = mapping.GetProperty("transformations");
        transformations.GetArrayLength().Should().Be(1);

        var trans = transformations[0];
        var transId = trans.GetProperty("id").GetString();
        transId.Should().NotBe("new-trans-1701629999999-0.222");
        Guid.TryParse(transId, out _).Should().BeTrue();
        trans.GetProperty("type").GetString().Should().Be("Replace");
    }

    [Fact]
    public async Task CreateAsync_WithEmptyFieldMappings_HandlesCorrectly()
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
            Name = "Empty Mappings Pipeline",
            Description = "Test with no mappings",
            SourceConnectorId = sourceConnector.Id,
            DestinationConnectorId = destConnector.Id,
            FieldMappings = JsonDocument.Parse("[]").RootElement
        };

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        result.Should().NotBeNull();
        
        var pipeline = await _context.Pipelines.FirstOrDefaultAsync(p => p.Id == result.Id);
        pipeline.Should().NotBeNull();
        pipeline!.FieldMappingsJson.Should().Be("[]");
    }

    [Fact]
    public async Task GetAllAsync_WithIsActiveTrue_ReturnsOnlyActivePipelines()
    {
        // Arrange
        var (tenantId, userId) = await SetupTenantAsync();
        var (sourceConnector, destConnector) = CreateConnectorPair(tenantId, userId);

        var activePipeline = new Pipeline
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Active Pipeline",
            SourceConnectorId = sourceConnector.Id,
            DestinationConnectorId = destConnector.Id,
            Status = "Idle",
            FieldMappingsJson = "[]",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        var inactivePipeline = new Pipeline
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Inactive Pipeline",
            SourceConnectorId = sourceConnector.Id,
            DestinationConnectorId = destConnector.Id,
            Status = "Idle",
            FieldMappingsJson = "[]",
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        _context.Pipelines.AddRange(activePipeline, inactivePipeline);
        await _context.SaveChangesAsync();

        var request = new PipelineSearchRequest
        {
            IsActive = true
        };

        // Act
        var result = await _sut.GetAllAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(1);
        result.Pipelines.Should().HaveCount(1);
        result.Pipelines[0].Name.Should().Be("Active Pipeline");
        result.Pipelines[0].IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetAllAsync_WithIsActiveFalse_ReturnsOnlyInactivePipelines()
    {
        // Arrange
        var (tenantId, userId) = await SetupTenantAsync();
        var (sourceConnector, destConnector) = CreateConnectorPair(tenantId, userId);

        var activePipeline = new Pipeline
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Active Pipeline",
            SourceConnectorId = sourceConnector.Id,
            DestinationConnectorId = destConnector.Id,
            Status = "Idle",
            FieldMappingsJson = "[]",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        var inactivePipeline = new Pipeline
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Inactive Pipeline",
            SourceConnectorId = sourceConnector.Id,
            DestinationConnectorId = destConnector.Id,
            Status = "Idle",
            FieldMappingsJson = "[]",
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        _context.Pipelines.AddRange(activePipeline, inactivePipeline);
        await _context.SaveChangesAsync();

        var request = new PipelineSearchRequest
        {
            IsActive = false
        };

        // Act
        var result = await _sut.GetAllAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(1);
        result.Pipelines.Should().HaveCount(1);
        result.Pipelines[0].Name.Should().Be("Inactive Pipeline");
        result.Pipelines[0].IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetAllAsync_WithoutIsActiveFilter_ReturnsAllPipelines()
    {
        // Arrange
        var (tenantId, userId) = await SetupTenantAsync();
        var (sourceConnector, destConnector) = CreateConnectorPair(tenantId, userId);

        var activePipeline = new Pipeline
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Active Pipeline",
            SourceConnectorId = sourceConnector.Id,
            DestinationConnectorId = destConnector.Id,
            Status = "Idle",
            FieldMappingsJson = "[]",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        var inactivePipeline = new Pipeline
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Inactive Pipeline",
            SourceConnectorId = sourceConnector.Id,
            DestinationConnectorId = destConnector.Id,
            Status = "Idle",
            FieldMappingsJson = "[]",
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        _context.Pipelines.AddRange(activePipeline, inactivePipeline);
        await _context.SaveChangesAsync();

        var request = new PipelineSearchRequest
        {
            // IsActive is null - no filter applied
        };

        // Act
        var result = await _sut.GetAllAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(2);
        result.Pipelines.Should().HaveCount(2);
        result.Pipelines.Should().Contain(p => p.Name == "Active Pipeline" && p.IsActive);
        result.Pipelines.Should().Contain(p => p.Name == "Inactive Pipeline" && !p.IsActive);
    }
}
