namespace MultiTenantETL.Infrastructure.Security;

/// <summary>
/// Thrown when an outbound connector target (URL, host or database endpoint) is
/// blocked by SSRF protection because it resolves to a loopback, private,
/// link-local or otherwise restricted network address.
/// </summary>
public class SsrfBlockedException : InvalidOperationException
{
    public SsrfBlockedException(string message)
        : base(message)
    {
    }
}
