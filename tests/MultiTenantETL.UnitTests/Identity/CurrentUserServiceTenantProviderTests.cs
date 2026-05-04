using FluentAssertions;
using Microsoft.AspNetCore.Http;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Infrastructure.Identity;
using NSubstitute;
using System.Security.Claims;
using CustomClaims = MultiTenantETL.Domain.Constants.ClaimTypes;

namespace MultiTenantETL.UnitTests.Identity;

/// <summary>
/// Tests for CurrentUserService fallback to TenantProvider in non-HTTP contexts
/// </summary>
public class CurrentUserServiceTenantProviderTests
{
    [Fact]
    public void GetTenantId_WithHttpContextClaims_ReturnsClaimValue()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var tenantProvider = Substitute.For<ITenantProvider>();

        var claims = new[]
        {
            new Claim(CustomClaims.TenantId, tenantId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext
        {
            User = principal
        };
        httpContextAccessor.HttpContext.Returns(httpContext);

        var service = new CurrentUserService(httpContextAccessor, tenantProvider);

        // Act
        var result = service.GetTenantId();

        // Assert
        result.Should().Be(tenantId);
        // TenantProvider should not be accessed when HttpContext has claims
        _ = tenantProvider.DidNotReceive().TenantId;
    }

    [Fact]
    public void GetTenantId_WithoutHttpContext_FallsBackToTenantProvider()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var tenantProvider = Substitute.For<ITenantProvider>();

        // No HttpContext (worker scenario)
        httpContextAccessor.HttpContext.Returns((HttpContext?)null);
        tenantProvider.TenantId.Returns(tenantId);

        var service = new CurrentUserService(httpContextAccessor, tenantProvider);

        // Act
        var result = service.GetTenantId();

        // Assert
        result.Should().Be(tenantId);
        _ = tenantProvider.Received(1).TenantId;
    }

    [Fact]
    public void GetTenantId_WithHttpContextButNoClaim_FallsBackToTenantProvider()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var tenantProvider = Substitute.For<ITenantProvider>();

        // HttpContext exists but has no tenant claim
        var identity = new ClaimsIdentity();
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext
        {
            User = principal
        };
        httpContextAccessor.HttpContext.Returns(httpContext);
        tenantProvider.TenantId.Returns(tenantId);

        var service = new CurrentUserService(httpContextAccessor, tenantProvider);

        // Act
        var result = service.GetTenantId();

        // Assert
        result.Should().Be(tenantId);
        _ = tenantProvider.Received(1).TenantId;
    }

    [Fact]
    public void GetTenantId_WithInvalidClaimValue_FallsBackToTenantProvider()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var tenantProvider = Substitute.For<ITenantProvider>();

        var claims = new[]
        {
            new Claim(CustomClaims.TenantId, "not-a-guid")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext
        {
            User = principal
        };
        httpContextAccessor.HttpContext.Returns(httpContext);
        tenantProvider.TenantId.Returns(tenantId);

        var service = new CurrentUserService(httpContextAccessor, tenantProvider);

        // Act
        var result = service.GetTenantId();

        // Assert
        result.Should().Be(tenantId);
        _ = tenantProvider.Received(1).TenantId;
    }

    [Fact]
    public void GetTenantId_TenantProviderIsNull_ReturnsGuidEmpty()
    {
        // Arrange
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var tenantProvider = Substitute.For<ITenantProvider>();

        httpContextAccessor.HttpContext.Returns((HttpContext?)null);
        tenantProvider.TenantId.Returns((Guid?)null);

        var service = new CurrentUserService(httpContextAccessor, tenantProvider);

        // Act
        var result = service.GetTenantId();

        // Assert
        result.Should().Be(Guid.Empty);
    }

    [Fact]
    public void GetUserId_WithoutHttpContext_ReturnsGuidEmpty()
    {
        // Arrange
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var tenantProvider = Substitute.For<ITenantProvider>();

        httpContextAccessor.HttpContext.Returns((HttpContext?)null);

        var service = new CurrentUserService(httpContextAccessor, tenantProvider);

        // Act
        var result = service.GetUserId();

        // Assert
        result.Should().Be(Guid.Empty);
    }

    [Fact]
    public void GetRole_WithoutHttpContext_ReturnsDefaultUserRole()
    {
        // Arrange
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var tenantProvider = Substitute.For<ITenantProvider>();

        httpContextAccessor.HttpContext.Returns((HttpContext?)null);

        var service = new CurrentUserService(httpContextAccessor, tenantProvider);

        // Act
        var result = service.GetRole();

        // Assert
        result.Should().Be(MultiTenantETL.Domain.Constants.Roles.User);
    }

    [Fact]
    public void GetEmail_WithoutHttpContext_ReturnsEmptyString()
    {
        // Arrange
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var tenantProvider = Substitute.For<ITenantProvider>();

        httpContextAccessor.HttpContext.Returns((HttpContext?)null);

        var service = new CurrentUserService(httpContextAccessor, tenantProvider);

        // Act
        var result = service.GetEmail();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetPermissions_WithoutHttpContext_ReturnsEmptyList()
    {
        // Arrange
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var tenantProvider = Substitute.For<ITenantProvider>();

        httpContextAccessor.HttpContext.Returns((HttpContext?)null);

        var service = new CurrentUserService(httpContextAccessor, tenantProvider);

        // Act
        var result = service.GetPermissions();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void HasPermission_WithoutHttpContext_ReturnsFalse()
    {
        // Arrange
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var tenantProvider = Substitute.For<ITenantProvider>();

        httpContextAccessor.HttpContext.Returns((HttpContext?)null);

        var service = new CurrentUserService(httpContextAccessor, tenantProvider);

        // Act
        var result = service.HasPermission("pipelines:read");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsInRole_WithoutHttpContext_ReturnsFalse()
    {
        // Arrange
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var tenantProvider = Substitute.For<ITenantProvider>();

        httpContextAccessor.HttpContext.Returns((HttpContext?)null);

        var service = new CurrentUserService(httpContextAccessor, tenantProvider);

        // Act
        var result = service.IsInRole("Admin");

        // Assert
        result.Should().BeFalse();
    }
}
