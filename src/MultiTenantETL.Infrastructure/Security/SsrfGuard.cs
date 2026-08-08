using Microsoft.Extensions.Options;
using MultiTenantETL.Infrastructure.Configuration;
using System.Net;

namespace MultiTenantETL.Infrastructure.Security;

/// <summary>
/// Validates outbound connector targets (URLs and bare hosts) before they are
/// contacted, blocking access to loopback, private, link-local and other
/// restricted network ranges to prevent Server-Side Request Forgery (SSRF).
/// </summary>
public interface ISsrfGuard
{
    /// <summary>
    /// Validates an absolute URL (e.g. a REST connector base/full URL). Throws
    /// <see cref="SsrfBlockedException"/> if the host resolves to a blocked range.
    /// </summary>
    void ValidateUrl(string url);

    /// <summary>
    /// Validates a bare host name or IP literal (e.g. an FTP/SFTP host). Throws
    /// <see cref="SsrfBlockedException"/> if the host resolves to a blocked range.
    /// </summary>
    void ValidateHost(string host);

    /// <summary>
    /// Validates a host name that cannot be resolved to IP addresses (e.g. a
    /// <c>mongodb+srv</c> SRV hostname). Applies name-based restrictions only:
    /// localhost / private-name suffixes, IP literals and empty hosts are
    /// blocked, but no DNS resolution is performed because SRV records point to
    /// the actual targets.
    /// </summary>
    void ValidateHostName(string host);
}

/// <summary>
/// Default <see cref="ISsrfGuard"/> implementation. Resolves the host via DNS and
/// blocks any address in a restricted range unless it is explicitly allow-listed.
/// Fails closed: a host that cannot be resolved or validated is blocked.
/// </summary>
public class SsrfGuard : ISsrfGuard
{
    private static readonly IPNetwork[] DefaultBlockedNetworks =
    {
        // IPv4
        new("0.0.0.0", 8),       // "this network"
        new("10.0.0.0", 8),      // RFC1918
        new("100.64.0.0", 10),   // CGNAT
        new("127.0.0.0", 8),     // loopback
        new("169.254.0.0", 16),  // link-local (incl. cloud metadata 169.254.169.254)
        new("172.16.0.0", 12),   // RFC1918
        new("192.0.0.0", 24),    // IETF protocol assignments
        new("192.0.2.0", 24),    // TEST-NET
        new("192.168.0.0", 16),  // RFC1918
        new("198.18.0.0", 15),   // benchmarking
        new("198.51.100.0", 24), // TEST-NET-2
        new("203.0.113.0", 24),  // TEST-NET-3
        new("224.0.0.0", 4),     // multicast
        new("240.0.0.0", 4),     // reserved
        // IPv6
        new("::", 128),          // unspecified
        new("::1", 128),         // loopback
        new("64:ff9b::", 96),    // NAT64 well-known prefix
        new("fc00::", 7),        // unique local
        new("fe80::", 10),       // link-local
        new("ff00::", 8),        // multicast
        new("2001:db8::", 32),   // documentation
    };

    private readonly SsrfSettings _settings;
    private readonly Func<string, Task<IPAddress[]>> _dnsResolver;
    private readonly IPNetwork[] _allowedNetworks;

    public SsrfGuard(IOptions<SsrfSettings> options)
        : this(options.Value, host => Dns.GetHostAddressesAsync(host))
    {
    }

    public SsrfGuard(SsrfSettings settings, Func<string, Task<IPAddress[]>>? dnsResolver = null)
    {
        _settings = settings;
        _dnsResolver = dnsResolver ?? (host => Dns.GetHostAddressesAsync(host));

        var allowed = new List<IPNetwork>();
        foreach (var cidr in _settings.AllowedPrivateCidrs ?? new List<string>())
        {
            if (!string.IsNullOrWhiteSpace(cidr) && IPNetwork.TryParse(cidr, out var network))
            {
                allowed.Add(network);
            }
        }

        _allowedNetworks = allowed.ToArray();
    }

    public void ValidateUrl(string url)
    {
        if (!_settings.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new SsrfBlockedException("Invalid URL: the connector target must be an absolute URL.");
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new SsrfBlockedException($"URL scheme '{uri.Scheme}' is not supported.");
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new SsrfBlockedException("Invalid URL: the connector target has no host.");
        }

        ValidateHost(uri.Host);
    }

    public void ValidateHost(string host)
    {
        if (!_settings.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(host))
        {
            throw new SsrfBlockedException("The connector target host cannot be empty.");
        }

        var hostName = NormalizeHost(host);

        EnsureHostNameAllowed(hostName);

        IPAddress[] addresses;

        if (IPAddress.TryParse(hostName, out var literal))
        {
            addresses = new[] { literal };
        }
        else
        {
            try
            {
                addresses = _dnsResolver(hostName).GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                throw new SsrfBlockedException($"Could not resolve connector target host '{hostName}'.");
            }

            if (addresses.Length == 0)
            {
                throw new SsrfBlockedException($"Could not resolve connector target host '{hostName}'.");
            }
        }

        if (!_settings.BlockPrivateNetworks)
        {
            return;
        }

        foreach (var address in addresses)
        {
            var ip = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

            if (IsBlocked(ip))
            {
                throw new SsrfBlockedException(
                    $"The connector target host '{hostName}' resolves to a restricted network address ({ip}) and is not allowed.");
            }
        }
    }

    public void ValidateHostName(string host)
    {
        if (!_settings.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(host))
        {
            throw new SsrfBlockedException("The connector target host cannot be empty.");
        }

        var hostName = NormalizeHost(host);

        if (IPAddress.TryParse(hostName, out _))
        {
            throw new SsrfBlockedException(
                $"The connector target host '{hostName}' is an IP literal; SRV hostnames must be DNS names.");
        }

        EnsureHostNameAllowed(hostName);
    }

    private void EnsureHostNameAllowed(string hostName)
    {
        if (_settings.BlockPrivateNetworks &&
            (hostName.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
             hostName.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
             hostName.EndsWith(".internal", StringComparison.OrdinalIgnoreCase)))
        {
            throw new SsrfBlockedException($"The connector target host '{hostName}' is restricted.");
        }
    }

    private bool IsBlocked(IPAddress ip)
    {
        if (IsInNetworks(ip, _allowedNetworks))
        {
            return false;
        }

        return IsInNetworks(ip, DefaultBlockedNetworks);
    }

    private static bool IsInNetworks(IPAddress ip, IEnumerable<IPNetwork> networks)
    {
        foreach (var network in networks)
        {
            if (network.Contains(ip))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeHost(string host)
    {
        var trimmed = host.Trim();

        // Strip SQL Server-style connection prefixes (e.g. "tcp:", "nc:") that
        // precede the host in connection strings.
        if (!trimmed.Contains("://", StringComparison.Ordinal))
        {
            var prefixColon = trimmed.IndexOf(':');
            if (prefixColon > 0)
            {
                var prefix = trimmed.Substring(0, prefixColon);
                if (prefix.Equals("tcp", StringComparison.OrdinalIgnoreCase) ||
                    prefix.Equals("nc", StringComparison.OrdinalIgnoreCase))
                {
                    trimmed = trimmed.Substring(prefixColon + 1);
                }
            }
        }

        // SQL Server named instances ("host\instance") connect to "host".
        var backslash = trimmed.IndexOf('\\');
        if (backslash > 0)
        {
            trimmed = trimmed.Substring(0, backslash);
        }

        // A ",port" suffix is never part of a host name.
        var comma = trimmed.IndexOf(',');
        if (comma > 0)
        {
            trimmed = trimmed.Substring(0, comma);
        }

        // Accept a full URL or host:port and extract just the host.
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
        {
            trimmed = uri.Host;
        }
        else if (Uri.TryCreate($"scheme://{trimmed}", UriKind.Absolute, out var hostPortUri) &&
                 !string.IsNullOrWhiteSpace(hostPortUri.Host))
        {
            trimmed = hostPortUri.Host;
        }
        else
        {
            // Last resort for formats the URI parser rejects: strip any
            // remaining ":port" suffix.
            if (!trimmed.Contains('[') && trimmed.Count(c => c == ':') == 1)
            {
                var colon = trimmed.IndexOf(':');
                if (colon > 0)
                {
                    trimmed = trimmed.Substring(0, colon);
                }
            }
        }

        // IPv6 literals are wrapped in brackets by Uri.Host; strip them.
        return trimmed.Trim('[', ']');
    }

    private readonly struct IPNetwork
    {
        private readonly byte[] _networkBytes;
        private readonly int _prefixLength;

        public IPNetwork(string networkAddress, int prefixLength)
        {
            var network = IPAddress.Parse(networkAddress);
            _networkBytes = network.GetAddressBytes();
            _prefixLength = prefixLength;
        }

        public static bool TryParse(string cidr, out IPNetwork network)
        {
            network = default;

            var slash = cidr.IndexOf('/');
            if (slash <= 0)
            {
                return false;
            }

            if (!IPAddress.TryParse(cidr.Substring(0, slash), out var address) ||
                !int.TryParse(cidr.Substring(slash + 1), out var prefix))
            {
                return false;
            }

            var bytes = address.GetAddressBytes();
            if (prefix < 0 || prefix > bytes.Length * 8)
            {
                return false;
            }

            network = new IPNetwork(address.ToString(), prefix);
            return true;
        }

        public bool Contains(IPAddress address)
        {
            var bytes = address.GetAddressBytes();

            if (bytes.Length != _networkBytes.Length)
            {
                return false;
            }

            var fullBytes = _prefixLength / 8;
            var remainingBits = _prefixLength % 8;

            for (var i = 0; i < fullBytes; i++)
            {
                if (bytes[i] != _networkBytes[i])
                {
                    return false;
                }
            }

            if (remainingBits > 0)
            {
                var mask = (byte)(0xFF << (8 - remainingBits));
                if ((bytes[fullBytes] & mask) != (_networkBytes[fullBytes] & mask))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
