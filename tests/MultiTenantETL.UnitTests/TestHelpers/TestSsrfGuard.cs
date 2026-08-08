using MultiTenantETL.Infrastructure.Configuration;
using MultiTenantETL.Infrastructure.Security;

namespace MultiTenantETL.UnitTests.TestHelpers;

/// <summary>
/// SSRF guard with validation disabled. Used by connector tests that target
/// local hosts / test servers so they are not blocked by SSRF protection.
/// </summary>
public static class TestSsrfGuard
{
    public static ISsrfGuard AllowAll { get; } = new SsrfGuard(new SsrfSettings { Enabled = false });
}
