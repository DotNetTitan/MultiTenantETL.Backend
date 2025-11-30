using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Connectors;
using MultiTenantETL.Application.Connectors.Models;
using MultiTenantETL.Application.Interfaces;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Persistence;
using MultiTenantETL.Infrastructure.Services;
using NSubstitute;

namespace MultiTenantETL.UnitTests.Services;

public class ConnectorServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ConnectorService> _logger;
    private readonly IConnectionTester _connectionTester;
    private readonly ISchemaDetector _schemaDetector;
    private readonly IEncryptionService _encryptionService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditService _auditService;
    private readonly ConnectorService _sut;

    public ConnectorServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _logger = Substitute.For<ILogger<ConnectorService>>();
        _connectionTester = Substitute.For<IConnectionTester>();
        _schemaDetector = Substitute.For<ISchemaDetector>();
        _encryptionService = Substitute.For<IEncryptionService>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _auditService = Substitute.For<IAuditService>();

        _sut = new ConnectorService(
            _context,
            _logger,
            _connectionTester,
            _schemaDetector,
            _encryptionService,
            _auditService);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task CreateAsync_ValidInput_CreatesConnectorAndEncryptsConfig()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var request = new CreateConnectorRequest
        {
            Name = "Test Connector",
            Description = "Test Description",
            Type = "Database",
            Provider = "PostgreSQL",
            Direction = "Source",
            Config = JsonDocument.Parse("{\"Host\":\"localhost\",\"Password\":\"secret\"}").RootElement
        };

        var encryptedConfig = JsonDocument.Parse("{\"Host\":\"localhost\",\"Password\":\"encrypted_secret\"}").RootElement;
        _encryptionService.EncryptJsonFields(Arg.Any<JsonElement>(), Arg.Any<string[]>())
            .Returns(encryptedConfig);

        // Act
        var result = await _sut.CreateAsync(request, tenantId, userId);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be(request.Name);
        result.Type.Should().Be(request.Type);
        result.Provider.Should().Be(request.Provider);
        result.IsSource.Should().BeTrue();
        result.IsDestination.Should().BeFalse();

        // Verify database
        var connector = await _context.Connectors.FirstOrDefaultAsync(c => c.Id == result.Id);
        connector.Should().NotBeNull();
        connector!.TenantId.Should().Be(tenantId);
        connector.CreatedBy.Should().Be(userId);
        
        // Verify encryption was called
        _encryptionService.Received(1).EncryptJsonFields(Arg.Any<JsonElement>(), Arg.Any<string[]>());
        
        // Verify audit log
        await _auditService.Received(1).LogAsync(
            Arg.Any<string>(), 
            "Connector", 
            result.Id.ToString(), 
            Arg.Any<string>(), 
            Arg.Any<object>());
    }

    [Fact]
    public async Task GetByIdAsync_ExistingConnector_ReturnsDecryptedConnector()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var connectorId = Guid.NewGuid();
        var encryptedConfigJson = "{\"Host\":\"localhost\",\"Password\":\"encrypted_secret\"}";
        
        var connector = new Connector
        {
            Id = connectorId,
            TenantId = tenantId,
            Name = "Test Connector",
            Type = "Database",
            Provider = "PostgreSQL",
            Direction = "Source",
            IsSource = true,
            IsDestination = false,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = encryptedConfigJson,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        _context.Connectors.Add(connector);
        await _context.SaveChangesAsync();

        var decryptedConfig = JsonDocument.Parse("{\"Host\":\"localhost\",\"Password\":\"secret\"}").RootElement;
        _encryptionService.DecryptJsonFields(Arg.Any<JsonElement>(), Arg.Any<string[]>())
            .Returns(decryptedConfig);

        // Act
        var result = await _sut.GetByIdAsync(connectorId, tenantId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(connectorId);
        result.Name.Should().Be("Test Connector");
        
        // Verify decryption was called
        _encryptionService.Received(1).DecryptJsonFields(Arg.Any<JsonElement>(), Arg.Any<string[]>());
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentConnector_ThrowsKeyNotFoundException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var connectorId = Guid.NewGuid();

        // Act
        var act = async () => await _sut.GetByIdAsync(connectorId, tenantId);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetByIdAsync_WrongTenant_ThrowsKeyNotFoundException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var connectorId = Guid.NewGuid();
        
        var connector = new Connector
        {
            Id = connectorId,
            TenantId = otherTenantId, // Different tenant
            Name = "Test Connector",
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

        _context.Connectors.Add(connector);
        await _context.SaveChangesAsync();

        // Act
        var act = async () => await _sut.GetByIdAsync(connectorId, tenantId);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_ValidInput_UpdatesConnectorAndReEncrypts()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var connectorId = Guid.NewGuid();
        
        var connector = new Connector
        {
            Id = connectorId,
            TenantId = tenantId,
            Name = "Old Name",
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

        _context.Connectors.Add(connector);
        await _context.SaveChangesAsync();

        var request = new UpdateConnectorRequest
        {
            Name = "New Name",
            Description = "Updated Description",
            Direction = "Destination",
            IsActive = true,
            Config = JsonDocument.Parse("{\"Host\":\"newhost\"}").RootElement
        };

        var encryptedConfig = JsonDocument.Parse("{\"Host\":\"newhost_encrypted\"}").RootElement;
        _encryptionService.EncryptJsonFields(Arg.Any<JsonElement>(), Arg.Any<string[]>())
            .Returns(encryptedConfig);

        // Act
        var result = await _sut.UpdateAsync(connectorId, request, tenantId, userId);

        // Assert
        result.Name.Should().Be("New Name");
        result.Description.Should().Be("Updated Description");
        result.Direction.Should().Be("Destination");
        result.IsSource.Should().BeFalse();
        result.IsDestination.Should().BeTrue();

        // Verify database update
        var updatedConnector = await _context.Connectors.FindAsync(connectorId);
        updatedConnector!.Name.Should().Be("New Name");
        updatedConnector.UpdatedBy.Should().Be(userId);
        updatedConnector.UpdatedAt.Should().NotBeNull();
        
        // Verify encryption called
        _encryptionService.Received(1).EncryptJsonFields(Arg.Any<JsonElement>(), Arg.Any<string[]>());
    }

    [Fact]
    public async Task DeleteAsync_ExistingConnector_RemovesFromDatabase()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var connectorId = Guid.NewGuid();
        
        var connector = new Connector
        {
            Id = connectorId,
            TenantId = tenantId,
            Name = "To Delete",
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

        _context.Connectors.Add(connector);
        await _context.SaveChangesAsync();

        // Act
        await _sut.DeleteAsync(connectorId, tenantId);

        // Assert
        var deletedConnector = await _context.Connectors.FindAsync(connectorId);
        deletedConnector.Should().BeNull();
        
        // Verify audit log
        await _auditService.Received(1).LogAsync(
            Arg.Is<string>(s => s == "Connector.Deleted"), 
            "Connector", 
            connectorId.ToString(), 
            Arg.Any<string>(), 
            Arg.Any<object>());
    }

    [Fact]
    public async Task GetAllAsync_ReturnsConnectorsForTenant()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        
        var connector1 = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Connector 1",
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

        var connector2 = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Connector 2",
            Type = "File",
            Provider = "CSV",
            Direction = "Destination",
            IsSource = false,
            IsDestination = true,
            RequiresCredentials = false,
            IsActive = true,
            ConfigJson = "{}",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        var otherConnector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = otherTenantId,
            Name = "Other Connector",
            Type = "Database",
            Provider = "MySQL",
            Direction = "Source",
            IsSource = true,
            IsDestination = false,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = "{}",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        _context.Connectors.AddRange(connector1, connector2, otherConnector);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetAllAsync(tenantId);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(c => c.Name == "Connector 1");
        result.Should().Contain(c => c.Name == "Connector 2");
        result.Should().NotContain(c => c.Name == "Other Connector");
    }
}
