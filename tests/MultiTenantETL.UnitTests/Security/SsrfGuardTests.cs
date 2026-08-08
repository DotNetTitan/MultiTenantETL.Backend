using FluentAssertions;
using MultiTenantETL.Infrastructure.Configuration;
using MultiTenantETL.Infrastructure.Security;
using System.Net;

namespace MultiTenantETL.UnitTests.Security;

public class SsrfGuardTests
{
    private static SsrfGuard CreateGuard(
        bool enabled = true,
        bool blockPrivateNetworks = true,
        Func<string, Task<IPAddress[]>>? resolver = null,
        IEnumerable<string>? allowedCidrs = null)
    {
        return new SsrfGuard(new SsrfSettings
        {
            Enabled = enabled,
            BlockPrivateNetworks = blockPrivateNetworks,
            AllowedPrivateCidrs = allowedCidrs?.ToList() ?? new List<string>()
        }, resolver);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.0.0.2")]
    [InlineData("10.0.0.1")]
    [InlineData("10.255.255.255")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.1")]
    [InlineData("0.0.0.0")]
    public void ValidateHost_WithRestrictedIpLiteral_ShouldThrow(string host)
    {
        var sut = CreateGuard();

        var act = () => sut.ValidateHost(host);

        act.Should().Throw<SsrfBlockedException>();
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("93.184.216.34")]
    public void ValidateHost_WithPublicIpLiteral_ShouldNotThrow(string host)
    {
        var sut = CreateGuard();

        var act = () => sut.ValidateHost(host);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateHost_WithLocalhostName_ShouldThrow()
    {
        var sut = CreateGuard();

        var act = () => sut.ValidateHost("localhost");

        act.Should().Throw<SsrfBlockedException>();
    }

    [Fact]
    public void ValidateHost_WithDnsResolvingToPublic_ShouldNotThrow()
    {
        var sut = CreateGuard(resolver: _ => Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") }));

        var act = () => sut.ValidateHost("api.example.com");

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateHost_WithDnsResolvingToPrivate_ShouldThrow()
    {
        var sut = CreateGuard(resolver: _ => Task.FromResult(new[] { IPAddress.Parse("10.0.0.5") }));

        var act = () => sut.ValidateHost("internal.example.com");

        act.Should().Throw<SsrfBlockedException>();
    }

    [Fact]
    public void ValidateHost_WithDnsResolvingToMetadataAddress_ShouldThrow()
    {
        var sut = CreateGuard(resolver: _ => Task.FromResult(new[] { IPAddress.Parse("169.254.169.254") }));

        var act = () => sut.ValidateHost("metadata.service");

        act.Should().Throw<SsrfBlockedException>();
    }

    [Fact]
    public void ValidateHost_WithUnresolvableHost_ShouldThrow()
    {
        var sut = CreateGuard(resolver: _ => Task.FromResult(Array.Empty<IPAddress>()));

        var act = () => sut.ValidateHost("does-not-exist.invalid");

        act.Should().Throw<SsrfBlockedException>();
    }

    [Fact]
    public void ValidateHost_WhenDisabled_ShouldNotThrowForPrivateIp()
    {
        var sut = CreateGuard(enabled: false);

        var act = () => sut.ValidateHost("192.168.1.1");

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateHost_WhenPrivateNetworksAllowed_ShouldNotThrow()
    {
        var sut = CreateGuard(blockPrivateNetworks: false);

        var act = () => sut.ValidateHost("10.0.0.1");

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateHost_WhenPrivateIpInAllowedCidr_ShouldNotThrow()
    {
        var sut = CreateGuard(allowedCidrs: new[] { "10.0.0.0/8" });

        var act = () => sut.ValidateHost("10.1.2.3");

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateHost_WhenPrivateIpOutsideAllowedCidr_ShouldThrow()
    {
        var sut = CreateGuard(allowedCidrs: new[] { "10.0.0.0/8" });

        var act = () => sut.ValidateHost("172.16.0.1");

        act.Should().Throw<SsrfBlockedException>();
    }

    [Fact]
    public void ValidateHost_WithHostAndPort_ShouldValidateHostOnly()
    {
        var sut = CreateGuard();

        var act = () => sut.ValidateHost("127.0.0.1:8080");

        act.Should().Throw<SsrfBlockedException>();
    }

    [Fact]
    public void ValidateHost_WithFullUrl_ShouldValidateHostOnly()
    {
        var sut = CreateGuard();

        var act = () => sut.ValidateHost("http://169.254.169.254/latest/meta-data");

        act.Should().Throw<SsrfBlockedException>();
    }

    [Fact]
    public void ValidateUrl_WithPrivateHost_ShouldThrow()
    {
        var sut = CreateGuard();

        var act = () => sut.ValidateUrl("http://192.168.1.1/schema");

        act.Should().Throw<SsrfBlockedException>();
    }

    [Fact]
    public void ValidateUrl_WithLoopbackHost_ShouldThrow()
    {
        var sut = CreateGuard();

        var act = () => sut.ValidateUrl("https://localhost:5000/api");

        act.Should().Throw<SsrfBlockedException>();
    }

    [Fact]
    public void ValidateUrl_WithPublicHost_ShouldNotThrow()
    {
        var sut = CreateGuard(resolver: _ => Task.FromResult(new[] { IPAddress.Parse("93.184.216.34") }));

        var act = () => sut.ValidateUrl("https://api.example.com/v1/data");

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("")]
    [InlineData("ftp://10.0.0.1/file")]
    [InlineData("file:///etc/passwd")]
    public void ValidateUrl_WithInvalidOrNonHttpUrl_ShouldThrow(string url)
    {
        var sut = CreateGuard();

        var act = () => sut.ValidateUrl(url);

        act.Should().Throw<SsrfBlockedException>();
    }

    [Theory]
    [InlineData("tcp:sql.example.com")]
    [InlineData("nc:sql.example.com")]
    [InlineData("sql.example.com\\instance")]
    [InlineData("tcp:sql.example.com,1433")]
    [InlineData("sql.example.com:1433")]
    public void ValidateHost_WithSqlServerStyleHost_ShouldNormalizeAndValidateHost(string host)
    {
        string? resolvedHost = null;
        var sut = CreateGuard(resolver: h =>
        {
            resolvedHost = h;
            return Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") });
        });

        var act = () => sut.ValidateHost(host);

        act.Should().NotThrow();
        resolvedHost.Should().Be("sql.example.com");
    }

    [Fact]
    public void ValidateHost_WithTcpPrefixedPrivateHost_ShouldThrow()
    {
        var sut = CreateGuard(resolver: _ => Task.FromResult(new[] { IPAddress.Parse("10.0.0.5") }));

        var act = () => sut.ValidateHost("tcp:internal.example.com");

        act.Should().Throw<SsrfBlockedException>();
    }

    [Fact]
    public void ValidateHost_WithBareIpv6LoopbackLiteral_ShouldBeBlockedAsLiteral()
    {
        var sut = CreateGuard();

        // ::1 must be treated as an IP literal (not DNS) and blocked as loopback,
        // rather than failing host normalization.
        var act = () => sut.ValidateHost("::1");

        act.Should().Throw<SsrfBlockedException>();
    }

    [Fact]
    public void ValidateHostName_WithPublicDnsName_ShouldNotThrow()
    {
        var sut = CreateGuard();

        var act = () => sut.ValidateHostName("cluster0.example.mongodb.net");

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("db.internal")]
    [InlineData("host.local")]
    public void ValidateHostName_WithRestrictedName_ShouldThrow(string host)
    {
        var sut = CreateGuard();

        var act = () => sut.ValidateHostName(host);

        act.Should().Throw<SsrfBlockedException>();
    }

    [Theory]
    [InlineData("10.0.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("127.0.0.1")]
    public void ValidateHostName_WithIpLiteral_ShouldThrow(string host)
    {
        var sut = CreateGuard();

        var act = () => sut.ValidateHostName(host);

        act.Should().Throw<SsrfBlockedException>();
    }

    [Fact]
    public void ValidateHostName_WhenDisabled_ShouldNotThrow()
    {
        var sut = CreateGuard(enabled: false);

        var act = () => sut.ValidateHostName("localhost");

        act.Should().NotThrow();
    }
}
