using System.Collections.Immutable;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Identity;
using MultiTenantETL.Infrastructure.Persistence;
using MultiTenantETL.Infrastructure.Services;
using NSubstitute;
using OpenIddict.Abstractions;
using CustomClaims = MultiTenantETL.Domain.Constants.ClaimTypes;

namespace MultiTenantETL.UnitTests.Services;

public class ClaimsServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ClaimsService _sut;

    public ClaimsServiceTests()
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

        // Mock RoleManager
        var roleStore = Substitute.For<IRoleStore<ApplicationRole>>();
        _roleManager = Substitute.For<RoleManager<ApplicationRole>>(
            roleStore, null, null, null, null);

        // Mock SignInManager
        _signInManager = Substitute.For<SignInManager<ApplicationUser>>(
            _userManager, 
            Substitute.For<Microsoft.AspNetCore.Http.IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            null, null, null, null);

        _sut = new ClaimsService(_signInManager, _userManager, _roleManager, _context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task BuildClaimsPrincipalAsync_UserWithNoTenant_AddsBasicClaims()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            UserName = "john@example.com",
            CurrentTenantId = null
        };

        var mockPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        _signInManager.CreateUserPrincipalAsync(user).Returns(mockPrincipal);
        _userManager.GetRolesAsync(user).Returns(new List<string>());

        // Act
        var result = await _sut.BuildClaimsPrincipalAsync(user, ImmutableArray<string>.Empty);

        // Assert
        result.Should().NotBeNull();
        var identity = (ClaimsIdentity)result.Identity!;
        
        // Check basic claims
        identity.FindFirst(OpenIddictConstants.Claims.Subject)?.Value.Should().Be(userId.ToString());
        identity.FindFirst(OpenIddictConstants.Claims.Email)?.Value.Should().Be("john@example.com");
        identity.FindFirst(OpenIddictConstants.Claims.Name)?.Value.Should().Be("John Doe");
        identity.FindFirst(CustomClaims.TenantId)?.Value.Should().Be("");
    }

    [Fact]
    public async Task BuildClaimsPrincipalAsync_UserWithTenant_AddsTenantClaims()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Acme Corp",
            Slug = "acme",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var user = new ApplicationUser
        {
            Id = userId,
            FirstName = "Jane",
            LastName = "Smith",
            Email = "jane@example.com",
            UserName = "jane@example.com",
            CurrentTenantId = tenantId
        };

        var userTenant = new UserTenant
        {
            UserId = userId,
            TenantId = tenantId,
            RoleCode = "TenantAdmin",
            IsActive = true,
            Tenant = tenant,
            User = user
        };

        _context.Tenants.Add(tenant);
        _context.UserTenants.Add(userTenant);
        await _context.SaveChangesAsync();

        var mockPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        _signInManager.CreateUserPrincipalAsync(user).Returns(mockPrincipal);
        _userManager.GetRolesAsync(user).Returns(new List<string>());

        var role = new ApplicationRole
        {
            Name = "TenantAdmin",
            Description = "Tenant Administrator",
            Permissions = new List<string> { "tenants.manage", "users.manage" }
        };
        _roleManager.FindByNameAsync("TenantAdmin").Returns(role);

        // Act
        var result = await _sut.BuildClaimsPrincipalAsync(user, ImmutableArray<string>.Empty);

        // Assert
        result.Should().NotBeNull();
        var identity = (ClaimsIdentity)result.Identity!;
        
        identity.FindFirst(CustomClaims.TenantId)?.Value.Should().Be(tenantId.ToString());
        identity.FindFirst(CustomClaims.TenantName)?.Value.Should().Be("Acme Corp");
        
        var roleClaims = identity.FindAll(ClaimTypes.Role).ToList();
        roleClaims.Should().Contain(c => c.Value == "TenantAdmin");
        
        var permissionClaims = identity.FindAll(CustomClaims.Permission).ToList();
        permissionClaims.Should().HaveCount(2);
        permissionClaims.Should().Contain(c => c.Value == "tenants.manage");
        permissionClaims.Should().Contain(c => c.Value == "users.manage");
    }

    [Fact]
    public async Task BuildClaimsPrincipalAsync_SuperAdmin_AddsGlobalRoleWithoutTenant()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            FirstName = "Admin",
            LastName = "User",
            Email = "admin@example.com",
            UserName = "admin@example.com",
            CurrentTenantId = null
        };

        var mockPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        _signInManager.CreateUserPrincipalAsync(user).Returns(mockPrincipal);
        _userManager.GetRolesAsync(user).Returns(new List<string> { "SuperAdmin" });

        var role = new ApplicationRole
        {
            Name = "SuperAdmin",
            Description = "Super Administrator",
            Permissions = new List<string> { "*" }
        };
        _roleManager.FindByNameAsync("SuperAdmin").Returns(role);

        // Act
        var result = await _sut.BuildClaimsPrincipalAsync(user, ImmutableArray<string>.Empty);

        // Assert
        result.Should().NotBeNull();
        var identity = (ClaimsIdentity)result.Identity!;
        
        var roleClaims = identity.FindAll(ClaimTypes.Role).ToList();
        roleClaims.Should().Contain(c => c.Value == "SuperAdmin");
        
        var permissionClaims = identity.FindAll(CustomClaims.Permission).ToList();
        permissionClaims.Should().Contain(c => c.Value == "*");
    }

    [Fact]
    public async Task BuildClaimsPrincipalAsync_SuperAdminWithTenant_UsesSuperAdminPermissions()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Test Tenant",
            Slug = "test",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var user = new ApplicationUser
        {
            Id = userId,
            FirstName = "Super",
            LastName = "Admin",
            Email = "superadmin@example.com",
            UserName = "superadmin@example.com",
            CurrentTenantId = tenantId
        };

        var userTenant = new UserTenant
        {
            UserId = userId,
            TenantId = tenantId,
            RoleCode = "User", // User role in tenant, but SuperAdmin globally
            IsActive = true,
            Tenant = tenant,
            User = user
        };

        _context.Tenants.Add(tenant);
        _context.UserTenants.Add(userTenant);
        await _context.SaveChangesAsync();

        var mockPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        _signInManager.CreateUserPrincipalAsync(user).Returns(mockPrincipal);
        _userManager.GetRolesAsync(user).Returns(new List<string> { "SuperAdmin" });

        var superAdminRole = new ApplicationRole
        {
            Name = "SuperAdmin",
            Description = "Super Administrator",
            Permissions = new List<string> { "*" }
        };
        _roleManager.FindByNameAsync("SuperAdmin").Returns(superAdminRole);

        // Act
        var result = await _sut.BuildClaimsPrincipalAsync(user, ImmutableArray<string>.Empty);

        // Assert
        result.Should().NotBeNull();
        var identity = (ClaimsIdentity)result.Identity!;
        
        // Should use SuperAdmin permissions, not User role permissions
        var permissionClaims = identity.FindAll(CustomClaims.Permission).ToList();
        permissionClaims.Should().Contain(c => c.Value == "*");
        
        identity.FindFirst(CustomClaims.TenantName)?.Value.Should().Be("Test Tenant");
    }

    [Fact]
    public async Task BuildClaimsPrincipalAsync_InactiveTenantMembership_DoesNotAddTenantClaims()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Inactive Tenant",
            Slug = "inactive",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var user = new ApplicationUser
        {
            Id = userId,
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com",
            UserName = "test@example.com",
            CurrentTenantId = tenantId
        };

        var userTenant = new UserTenant
        {
            UserId = userId,
            TenantId = tenantId,
            RoleCode = "User",
            IsActive = false, // Inactive!
            Tenant = tenant,
            User = user
        };

        _context.Tenants.Add(tenant);
        _context.UserTenants.Add(userTenant);
        await _context.SaveChangesAsync();

        var mockPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        _signInManager.CreateUserPrincipalAsync(user).Returns(mockPrincipal);
        _userManager.GetRolesAsync(user).Returns(new List<string>());

        // Act
        var result = await _sut.BuildClaimsPrincipalAsync(user, ImmutableArray<string>.Empty);

        // Assert
        result.Should().NotBeNull();
        var identity = (ClaimsIdentity)result.Identity!;
        
        // Should not have tenant-specific role or permissions since membership is inactive
        identity.FindFirst(CustomClaims.TenantName).Should().BeNull();
    }

    [Fact]
    public async Task BuildClaimsPrincipalAsync_UserWithMultipleRoles_AddsAllRoles()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Multi-Role Tenant",
            Slug = "multi",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var user = new ApplicationUser
        {
            Id = userId,
            FirstName = "Multi",
            LastName = "Role",
            Email = "multi@example.com",
            UserName = "multi@example.com",
            CurrentTenantId = tenantId
        };

        var userTenant = new UserTenant
        {
            UserId = userId,
            TenantId = tenantId,
            RoleCode = "TenantAdmin",
            IsActive = true,
            Tenant = tenant,
            User = user
        };

        _context.Tenants.Add(tenant);
        _context.UserTenants.Add(userTenant);
        await _context.SaveChangesAsync();

        var mockPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        _signInManager.CreateUserPrincipalAsync(user).Returns(mockPrincipal);
        
        // User has both global Developer role and TenantAdmin role in tenant
        _userManager.GetRolesAsync(user).Returns(new List<string> { "Developer" });

        var tenantAdminRole = new ApplicationRole
        {
            Name = "TenantAdmin",
            Description = "Tenant Admin",
            Permissions = new List<string> { "tenants.manage" }
        };
        _roleManager.FindByNameAsync("TenantAdmin").Returns(tenantAdminRole);

        // Act
        var result = await _sut.BuildClaimsPrincipalAsync(user, ImmutableArray<string>.Empty);

        // Assert
        result.Should().NotBeNull();
        var identity = (ClaimsIdentity)result.Identity!;
        
        var roleClaims = identity.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        roleClaims.Should().Contain("Developer"); // Global role
        roleClaims.Should().Contain("TenantAdmin"); // Tenant-specific role
    }

    [Fact]
    public async Task BuildClaimsPrincipalAsync_RoleWithNoPermissions_DoesNotAddPermissionClaims()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "No Perms Tenant",
            Slug = "noperms",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var user = new ApplicationUser
        {
            Id = userId,
            FirstName = "No",
            LastName = "Perms",
            Email = "noperms@example.com",
            UserName = "noperms@example.com",
            CurrentTenantId = tenantId
        };

        var userTenant = new UserTenant
        {
            UserId = userId,
            TenantId = tenantId,
            RoleCode = "Viewer",
            IsActive = true,
            Tenant = tenant,
            User = user
        };

        _context.Tenants.Add(tenant);
        _context.UserTenants.Add(userTenant);
        await _context.SaveChangesAsync();

        var mockPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        _signInManager.CreateUserPrincipalAsync(user).Returns(mockPrincipal);
        _userManager.GetRolesAsync(user).Returns(new List<string>());

        var viewerRole = new ApplicationRole
        {
            Name = "Viewer",
            Description = "Read-only viewer",
            Permissions = new List<string>() // No permissions
        };
        _roleManager.FindByNameAsync("Viewer").Returns(viewerRole);

        // Act
        var result = await _sut.BuildClaimsPrincipalAsync(user, ImmutableArray<string>.Empty);

        // Assert
        result.Should().NotBeNull();
        var identity = (ClaimsIdentity)result.Identity!;
        
        var permissionClaims = identity.FindAll(CustomClaims.Permission).ToList();
        permissionClaims.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildClaimsPrincipalAsync_RoleNotFound_DoesNotAddPermissions()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com",
            UserName = "test@example.com",
            CurrentTenantId = null
        };

        var mockPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        _signInManager.CreateUserPrincipalAsync(user).Returns(mockPrincipal);
        _userManager.GetRolesAsync(user).Returns(new List<string> { "NonExistentRole" });

        _roleManager.FindByNameAsync("NonExistentRole").Returns((ApplicationRole?)null);

        // Act
        var result = await _sut.BuildClaimsPrincipalAsync(user, ImmutableArray<string>.Empty);

        // Assert
        result.Should().NotBeNull();
        var identity = (ClaimsIdentity)result.Identity!;
        
        var permissionClaims = identity.FindAll(CustomClaims.Permission).ToList();
        permissionClaims.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildClaimsPrincipalAsync_UserNameFormatting_CombinesFirstAndLastName()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            FirstName = "Marie",
            LastName = "Curie",
            Email = "marie@example.com",
            UserName = "marie@example.com",
            CurrentTenantId = null
        };

        var mockPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        _signInManager.CreateUserPrincipalAsync(user).Returns(mockPrincipal);
        _userManager.GetRolesAsync(user).Returns(new List<string>());

        // Act
        var result = await _sut.BuildClaimsPrincipalAsync(user, ImmutableArray<string>.Empty);

        // Assert
        result.Should().NotBeNull();
        var identity = (ClaimsIdentity)result.Identity!;
        
        identity.FindFirst(OpenIddictConstants.Claims.Name)?.Value.Should().Be("Marie Curie");
    }
}
