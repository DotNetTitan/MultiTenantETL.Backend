using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Interfaces;
using MultiTenantETL.Application.Transformations.Models;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Persistence;
using MultiTenantETL.Infrastructure.Services;
using NSubstitute;
using System.Text.Json;

namespace MultiTenantETL.UnitTests.Services;

public class TransformationServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TransformationService> _logger;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditService _auditService;
    private readonly TransformationService _sut;

    public TransformationServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var tenantProvider = Substitute.For<ITenantProvider>();
        _context = new ApplicationDbContext(options, tenantProvider);
        _logger = Substitute.For<ILogger<TransformationService>>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _auditService = Substitute.For<IAuditService>();

        _sut = new TransformationService(
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
    public async Task CreateAsync_ValidInput_CreatesTransformation()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _currentUserService.GetTenantId().Returns(tenantId);
        _currentUserService.GetUserId().Returns(userId);

        var request = new CreateTransformationRequest
        {
            Name = "Test Transformation",
            Description = "Test Description",
            Type = "Map",
            Config = JsonSerializer.Deserialize<JsonElement>("{\"mapping\": \"test\"}")
        };

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be(request.Name);
        result.Type.Should().Be(request.Type);

        // Verify database
        var transformation = await _context.Transformations.FirstOrDefaultAsync(t => t.Id == result.Id);
        transformation.Should().NotBeNull();
        transformation!.TenantId.Should().Be(tenantId);
        transformation.CreatedBy.Should().Be(userId);

        // Verify audit log
        await _auditService.Received(1).LogAsync(
            Arg.Is<string>(s => s == AuditActions.TransformationCreated),
            "Transformation",
            result.Id.ToString(),
            Arg.Any<string>(),
            Arg.Any<object>());
    }

    [Fact]
    public async Task CreateAsync_InvalidType_ThrowsArgumentException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _currentUserService.GetTenantId().Returns(tenantId);

        var request = new CreateTransformationRequest
        {
            Name = "Invalid Transformation",
            Type = "InvalidType",
            Config = JsonSerializer.Deserialize<JsonElement>("{}")
        };

        // Act
        var act = async () => await _sut.CreateAsync(request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage($"Invalid transformation type: {request.Type}.*");
    }

    [Fact]
    public async Task GetByIdAsync_ExistingTransformation_ReturnsTransformation()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var transformationId = Guid.NewGuid();
        _currentUserService.GetTenantId().Returns(tenantId);

        var transformation = new Transformation
        {
            Id = transformationId,
            TenantId = tenantId,
            Name = "Existing Transformation",
            Type = "Filter",
            ConfigJson = "{}",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        _context.Transformations.Add(transformation);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetByIdAsync(transformationId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(transformationId);
        result.Name.Should().Be("Existing Transformation");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentTransformation_ThrowsKeyNotFoundException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _currentUserService.GetTenantId().Returns(tenantId);

        // Act
        var act = async () => await _sut.GetByIdAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_ValidInput_UpdatesTransformation()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var transformationId = Guid.NewGuid();
        _currentUserService.GetTenantId().Returns(tenantId);

        var transformation = new Transformation
        {
            Id = transformationId,
            TenantId = tenantId,
            Name = "Old Name",
            Type = "Filter",
            ConfigJson = "{}",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        _context.Transformations.Add(transformation);
        await _context.SaveChangesAsync();

        var request = new UpdateTransformationRequest
        {
            Name = "New Name",
            Description = "Updated Description",
            Config = JsonSerializer.Deserialize<JsonElement>("{\"filter\": \"new\"}")
        };

        // Act
        var result = await _sut.UpdateAsync(transformationId, request);

        // Assert
        result.Name.Should().Be("New Name");
        result.Description.Should().Be("Updated Description");

        // Verify database
        var updatedTransformation = await _context.Transformations.FindAsync(transformationId);
        updatedTransformation!.Name.Should().Be("New Name");

        // Verify audit log
        await _auditService.Received(1).LogAsync(
            Arg.Is<string>(s => s == AuditActions.TransformationUpdated),
            "Transformation",
            transformationId.ToString(),
            Arg.Any<string>(),
            Arg.Any<object>());
    }

    [Fact]
    public async Task DeleteAsync_ExistingTransformation_RemovesTransformation()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var transformationId = Guid.NewGuid();
        _currentUserService.GetTenantId().Returns(tenantId);

        var transformation = new Transformation
        {
            Id = transformationId,
            TenantId = tenantId,
            Name = "To Delete",
            Type = "Filter",
            ConfigJson = "{}",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        _context.Transformations.Add(transformation);
        await _context.SaveChangesAsync();

        // Act
        await _sut.DeleteAsync(transformationId);

        // Assert
        var deletedTransformation = await _context.Transformations.FindAsync(transformationId);
        deletedTransformation.Should().BeNull();

        // Verify audit log
        await _auditService.Received(1).LogAsync(
            Arg.Is<string>(s => s == AuditActions.TransformationDeleted),
            "Transformation",
            transformationId.ToString(),
            Arg.Any<string>(),
            Arg.Any<object>());
    }

    [Fact]
    public async Task GetByIdAsync_WrongTenant_ThrowsKeyNotFoundException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var transformationId = Guid.NewGuid();
        _currentUserService.GetTenantId().Returns(tenantId);

        var transformation = new Transformation
        {
            Id = transformationId,
            TenantId = otherTenantId, // Different tenant
            Name = "Other Tenant Transformation",
            Type = "Filter",
            ConfigJson = "{}",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        _context.Transformations.Add(transformation);
        await _context.SaveChangesAsync();

        // Act
        var act = async () => await _sut.GetByIdAsync(transformationId);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
