using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Domain.Enums;
using MultiTenantETL.Infrastructure.Identity;
using MultiTenantETL.Infrastructure.Persistence;
using MultiTenantETL.Infrastructure.Services;
using NSubstitute;

namespace MultiTenantETL.UnitTests.Services;

public class TenantServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly TenantService _sut;

    public TenantServiceTests()
    {
        // Set up in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);

        // Mock UserManager
        var userStore = Substitute.For<IUserStore<ApplicationUser>>();
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            userStore, null, null, null, null, null, null, null, null);

        _sut = new TenantService(_userManager, _context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task CreateTenantAsync_ValidInput_CreatesTenant()
    {
        // Arrange
        var name = "Test Company";
        var slug = "test-company";

        // Act
        var result = await _sut.CreateTenantAsync(name, slug);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Name.Should().Be(name);
        result.Data.Slug.Should().Be(slug);
        result.Data.IsActive.Should().BeTrue();
        result.Data.Id.Should().NotBe(Guid.Empty);

        // Verify it was saved to database
        var savedTenant = await _context.Tenants.FindAsync(result.Data.Id);
        savedTenant.Should().NotBeNull();
        savedTenant!.Name.Should().Be(name);
    }

    [Fact]
    public async Task CreateTenantAsync_DuplicateSlug_ReturnsError()
    {
        // Arrange
        var existingTenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Existing Company",
            Slug = "existing-slug",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.Tenants.Add(existingTenant);
        await _context.SaveChangesAsync();

        // Act - Try to create tenant with same slug
        var result = await _sut.CreateTenantAsync("New Company", "existing-slug");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(AuthErrorCode.TenantAlreadyExists);
        result.ErrorMessage.Should().Contain("already exists");
    }

    [Fact]
    public async Task GetTenantByIdAsync_ExistingTenant_ReturnsTenant()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Test Company",
            Slug = "test-company",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetTenantByIdAsync(tenant.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(tenant.Id);
        result.Name.Should().Be(tenant.Name);
    }

    [Fact]
    public async Task GetTenantByIdAsync_NonExistentTenant_ReturnsNull()
    {
        // Act
        var result = await _sut.GetTenantByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetTenantBySlugAsync_ExistingTenant_ReturnsTenant()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Test Company",
            Slug = "test-company",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetTenantBySlugAsync("test-company");

        // Assert
        result.Should().NotBeNull();
        result!.Slug.Should().Be("test-company");
    }

    [Fact]
    public async Task GetAllTenantsAsync_ReturnsAllTenantsOrderedByName()
    {
        // Arrange
        var tenants = new[]
        {
            new Tenant { Id = Guid.NewGuid(), Name = "Charlie Company", Slug = "charlie", IsActive = true, CreatedAt = DateTime.UtcNow },
            new Tenant { Id = Guid.NewGuid(), Name = "Alpha Company", Slug = "alpha", IsActive = true, CreatedAt = DateTime.UtcNow },
            new Tenant { Id = Guid.NewGuid(), Name = "Bravo Company", Slug = "bravo", IsActive = true, CreatedAt = DateTime.UtcNow }
        };
        _context.Tenants.AddRange(tenants);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetAllTenantsAsync();

        // Assert
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("Alpha Company");
        result[1].Name.Should().Be("Bravo Company");
        result[2].Name.Should().Be("Charlie Company");
    }

    [Fact]
    public async Task UpdateTenantAsync_ValidUpdate_UpdatesTenant()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Old Name",
            Slug = "old-slug",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.UpdateTenantAsync(tenant.Id, "New Name", false);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.Name.Should().Be("New Name");
        result.Data.IsActive.Should().BeFalse();

        // Verify database was updated
        var updated = await _context.Tenants.FindAsync(tenant.Id);
        updated!.Name.Should().Be("New Name");
        updated.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateTenantAsync_NonExistentTenant_ReturnsError()
    {
        // Act
        var result = await _sut.UpdateTenantAsync(Guid.NewGuid(), "New Name", true);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(AuthErrorCode.TenantNotFound);
    }

    [Fact]
    public async Task DeleteTenantAsync_ExistingTenant_SoftDeletes()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Test Company",
            Slug = "test-company",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.DeleteTenantAsync(tenant.Id);

        // Assert
        result.Success.Should().BeTrue();

        // Verify it was soft deleted
        var deletedTenant = await _context.Tenants.FindAsync(tenant.Id);
        deletedTenant.Should().NotBeNull();
        deletedTenant!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task AddUserToTenantAsync_ValidInput_AddsUserToTenant()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "test@example.com",
            UserName = "test@example.com",
            CurrentTenantId = null
        };
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Test Company",
            Slug = "test-company",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);

        // Act
        var result = await _sut.AddUserToTenantAsync(userId, tenant.Id, "User");

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.UserId.Should().Be(userId);
        result.Data.TenantId.Should().Be(tenant.Id);
        result.Data.RoleCode.Should().Be("User");
        result.Data.IsActive.Should().BeTrue();

        // Verify UserManager was called to update user
        await _userManager.Received(1).UpdateAsync(user);
        user.CurrentTenantId.Should().Be(tenant.Id);
    }

    [Fact]
    public async Task AddUserToTenantAsync_UserNotFound_ReturnsError()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Test Company",
            Slug = "test-company",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();

        _userManager.FindByIdAsync(Arg.Any<string>()).Returns((ApplicationUser?)null);

        // Act
        var result = await _sut.AddUserToTenantAsync(Guid.NewGuid(), tenant.Id, "User");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(AuthErrorCode.UserNotFound);
    }

    [Fact]
    public async Task AddUserToTenantAsync_TenantNotFound_ReturnsError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            FirstName = " Test",
            LastName = "User",
            Id = userId,
            Email = "test@example.com"
        };
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);

        // Act
        var result = await _sut.AddUserToTenantAsync(userId, Guid.NewGuid(), "User");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(AuthErrorCode.TenantNotFound);
    }

    [Fact]
    public async Task AddUserToTenantAsync_UserAlreadyInTenant_ReturnsError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            FirstName = " Test",
            LastName = "User",
            Id = userId,
            Email = "test@example.com"
        };
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Test Company",
            Slug = "test-company",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var existingUserTenant = new UserTenant
        {
            UserId = userId,
            TenantId = tenant.Id,
            RoleCode = "User",
            IsActive = true
        };

        _context.Tenants.Add(tenant);
        _context.UserTenants.Add(existingUserTenant);
        await _context.SaveChangesAsync();

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);

        // Act
        var result = await _sut.AddUserToTenantAsync(userId, tenant.Id, "Admin");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(AuthErrorCode.UserAlreadyInTenant);
    }

    [Fact]
    public async Task RemoveUserFromTenantAsync_ValidInput_RemovesUserFromTenant()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "test@example.com",
            CurrentTenantId = tenantId
        };
        var userTenant = new UserTenant
        {
            UserId = userId,
            TenantId = tenantId,
            RoleCode = "User",
            IsActive = true
        };

        _context.UserTenants.Add(userTenant);
        await _context.SaveChangesAsync();

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);

        // Act
        var result = await _sut.RemoveUserFromTenantAsync(userId, tenantId);

        // Assert
        result.Success.Should().BeTrue();

        // Verify user was removed from database
        var removed = await _context.UserTenants
            .FirstOrDefaultAsync(ut => ut.UserId == userId && ut.TenantId == tenantId);
        removed.Should().BeNull();

        // Verify user's current tenant was cleared
        await _userManager.Received(1).UpdateAsync(user);
        user.CurrentTenantId.Should().BeNull();
    }

    [Fact]
    public async Task RemoveUserFromTenantAsync_UserNotInTenant_ReturnsError()
    {
        // Act
        var result = await _sut.RemoveUserFromTenantAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(AuthErrorCode.UserNotInTenant);
    }

    [Fact]
    public async Task UpdateUserTenantRoleAsync_ValidInput_UpdatesRole()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Test Company",
            Slug = "test-company",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var user = new ApplicationUser
        {
            FirstName = " Test",
            LastName = "User",
            Id = userId,
            Email = "test@example.com"
        };
        var userTenant = new UserTenant
        {
            UserId = userId,
            TenantId = tenantId,
            RoleCode = "User",
            IsActive = true,
            Tenant = tenant,
            User = user
        };

        _context.Tenants.Add(tenant);
        _context.UserTenants.Add(userTenant);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.UpdateUserTenantRoleAsync(userId, tenantId, "Admin");

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.RoleCode.Should().Be("Admin");

        // Verify database was updated
        var updated = await _context.UserTenants
            .FirstOrDefaultAsync(ut => ut.UserId == userId && ut.TenantId == tenantId);
        updated!.RoleCode.Should().Be("Admin");
    }

    [Fact]
    public async Task UpdateUserTenantRoleAsync_UserNotInTenant_ReturnsError()
    {
        // Act
        var result = await _sut.UpdateUserTenantRoleAsync(Guid.NewGuid(), Guid.NewGuid(), "Admin");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(AuthErrorCode.UserNotInTenant);
    }

    [Fact]
    public async Task GetUserTenantsAsync_ReturnsUserTenantsOrdered()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenant1 = new Tenant { Id = Guid.NewGuid(), Name = "Zulu Company", Slug = "zulu", IsActive = true, CreatedAt = DateTime.UtcNow };
        var tenant2 = new Tenant { Id = Guid.NewGuid(), Name = "Alpha Company", Slug = "alpha", IsActive = true, CreatedAt = DateTime.UtcNow };
        var tenant3 = new Tenant { Id = Guid.NewGuid(), Name = "Mike Company", Slug = "mike", IsActive = true, CreatedAt = DateTime.UtcNow };

        var userTenant1 = new UserTenant { UserId = userId, TenantId = tenant1.Id, RoleCode = "User", IsActive = true, Tenant = tenant1 };
        var userTenant2 = new UserTenant { UserId = userId, TenantId = tenant2.Id, RoleCode = "Admin", IsActive = true, Tenant = tenant2 };
        var userTenant3 = new UserTenant { UserId = userId, TenantId = tenant3.Id, RoleCode = "User", IsActive = false, Tenant = tenant3 }; // Inactive

        _context.Tenants.AddRange(tenant1, tenant2, tenant3);
        _context.UserTenants.AddRange(userTenant1, userTenant2, userTenant3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetUserTenantsAsync(userId);

        // Assert
        result.Should().HaveCount(2); // Only active tenants
        result[0].Tenant.Name.Should().Be("Alpha Company");
        result[1].Tenant.Name.Should().Be("Zulu Company");
    }

    [Fact]
    public async Task SwitchUserTenantAsync_ValidUserAndTenant_UpdatesCurrentTenant()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var user = new ApplicationUser
        {
            Id = userId,
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com"
        };

        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Test Company",
            Slug = "test",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var userTenant = new UserTenant
        {
            UserId = userId,
            TenantId = tenantId,
            IsActive = true,
            Tenant = tenant,
            User = user,
            RoleCode = "RoleCode",
        };

        _context.Tenants.Add(tenant);
        _context.UserTenants.Add(userTenant);
        await _context.SaveChangesAsync();

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns(new List<string> { "User" });
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);

        // Act
        var result = await _sut.SwitchUserTenantAsync(userId, tenantId);

        // Assert
        result.Success.Should().BeTrue();
        result.UserTenant.Should().NotBeNull();
        result.UserTenant!.TenantId.Should().Be(tenantId);
        result.UserTenant.TenantName.Should().Be("Test Company");

        // Verify user's current tenant was updated
        await _userManager.Received(1).UpdateAsync(user);
        user.CurrentTenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task SwitchUserTenantAsync_RegularUserWithoutAccess_ReturnsError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var user = new ApplicationUser
        {
            Id = userId,
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com"
        };

        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Test Company",
            Slug = "test",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns(new List<string> { "User" });

        // Act - User not member of tenant
        var result = await _sut.SwitchUserTenantAsync(userId, tenantId);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(AuthErrorCode.TenantAccessDenied);
    }

    [Fact]
    public async Task SwitchUserTenantAsync_SuperAdmin_CanSwitchToAnyTenant()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var user = new ApplicationUser
        {
            Id = userId,
            FirstName = "Test",
            LastName = "User",
            Email = "admin@example.com"
        };

        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Test Company",
            Slug = "test",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns(new List<string> { "SuperAdmin" });
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);

        // Act - SuperAdmin not member of tenant
        var result = await _sut.SwitchUserTenantAsync(userId, tenantId);

        // Assert
        result.Success.Should().BeTrue();
        result.UserTenant.Should().NotBeNull();
        result.UserTenant!.RoleCode.Should().Be("SuperAdmin");

        await _userManager.Received(1).UpdateAsync(user);
    }
}