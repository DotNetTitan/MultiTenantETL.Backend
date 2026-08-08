namespace MultiTenantETL.Infrastructure.Configuration;

/// <summary>
/// Settings governing Server-Side Request Forgery (SSRF) protection for outbound
/// connector traffic. In a cloud-hosted multi-tenant deployment these should stay
/// enabled so tenant users cannot reach internal/metadata endpoints. Self-hosted
/// deployments that must reach private networks can allow specific CIDR ranges or
/// disable the check entirely.
/// </summary>
public class SsrfSettings
{
    public const string SectionName = "Ssrf";

    /// <summary>
    /// Master switch. When false, no SSRF validation is performed.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// When true, outbound requests to loopback, link-local (169.254.0.0/16,
    /// including cloud metadata services), RFC1918 private, CGNAT, multicast and
    /// reserved addresses are blocked.
    /// </summary>
    public bool BlockPrivateNetworks { get; set; } = true;

    /// <summary>
    /// CIDR ranges (e.g. "10.0.0.0/8", "172.16.0.0/12") that are explicitly
    /// allowed even though they fall within blocked private ranges. Used by
    /// self-hosted deployments that legitimately connect to corporate networks.
    /// </summary>
    public List<string> AllowedPrivateCidrs { get; set; } = new();
}
