using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Interfaces;
using MultiTenantETL.Infrastructure.Persistence;
using MultiTenantETL.Infrastructure.Services;
using NSubstitute;

namespace MultiTenantETL.UnitTests.Services;

public class AuditServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditService> _logger;
    private readonly ITenantProvider _tenantProvider;
    private readonly AuditService _sut;

    public AuditServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _tenantProvider = Substitute.For<ITenantProvider>();
        _context = new ApplicationDbContext(options, _tenantProvider);
        _currentUser = Substitute.For<ICurrentUserService>();
        _httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _logger = Substitute.For<ILogger<AuditService>>();

        _sut = new AuditService(
            _context,
            _currentUser,
            _httpContextAccessor,
            _logger);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task LogAsync_SuccessFalse_DefaultsToErrorSeverity()
    {
        // Act
        await _sut.LogAsync(
            action: "Test.Action",
            resourceType: "Test",
            success: false,
            errorMessage: "Error occurred"
        );

        // Assert
        var log = await _context.AuditLogs.FirstAsync();
        log.Severity.Should().Be("Error");
        log.Success.Should().BeFalse();
    }

    [Fact]
    public async Task LogAuthenticationAsync_SuccessFalse_UsesErrorSeverity()
    {
        // Act
        await _sut.LogAuthenticationAsync(
            action: "Auth.Login",
            userEmail: "test@example.com",
            success: false,
            errorMessage: "Invalid credentials"
        );

        // Assert
        var log = await _context.AuditLogs.FirstAsync();
        log.Severity.Should().Be("Error");
    }

    [Fact]
    public async Task GetAuditLogsAsync_FilterByError_IncludesFailedLogs()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _currentUser.GetTenantId().Returns(tenantId);

        // 1. Explicit Error severity
        await _sut.LogAsync("Action.1", "Type", severity: "Error");
        
        // 2. Success false (should be Error internally)
        await _sut.LogAsync("Action.2", "Type", success: false);
        
        // 3. Info severity
        await _sut.LogAsync("Action.3", "Type", severity: "Info");

        // Act
        var (logs, totalCount) = await _sut.GetAuditLogsAsync(severity: "Error");
        var (logsLower, totalCountLower) = await _sut.GetAuditLogsAsync(severity: "error");

        // Assert
        totalCount.Should().Be(2);
        logs.Should().Contain(l => l.Action == "Action.1");
        logs.Should().Contain(l => l.Action == "Action.2");
        logs.Should().NotContain(l => l.Action == "Action.3");

        totalCountLower.Should().Be(2);
        logsLower.Should().HaveCount(2);
    }
}
