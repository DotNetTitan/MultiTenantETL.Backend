using FluentAssertions;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Infrastructure.Configuration;
using MultiTenantETL.Infrastructure.Security;
using MultiTenantETL.Infrastructure.Services.ConnectionTesting.Database;
using NSubstitute;
using System.Net;
using System.Text.Json;

namespace MultiTenantETL.UnitTests.Services;

public class DatabaseConnectionTesterTests
{
    private static DatabaseConnectionTester CreateSut(
        Func<string, Task<IPAddress[]>>? resolver = null,
        List<string>? capturedHosts = null)
    {
        var logger = Substitute.For<ILogger<DatabaseConnectionTester>>();
        var guard = new SsrfGuard(new SsrfSettings
        {
            Enabled = true,
            BlockPrivateNetworks = true,
            AllowedPrivateCidrs = new List<string>()
        }, h =>
        {
            capturedHosts?.Add(h);
            return resolver?.Invoke(h) ?? Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") });
        });

        return new DatabaseConnectionTester(logger, guard);
    }

    private static JsonElement ConfigJson(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public async Task TestConnectionAsync_WithPrivateHost_ReturnsFailureInsteadOfThrowing()
    {
        var sut = CreateSut();

        var result = await sut.TestConnectionAsync(
            ConnectorProviders.SqlServer,
            ConfigJson("{\"Host\":\"10.0.0.5\",\"Database\":\"db\",\"Username\":\"u\",\"Password\":\"p\"}"));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("restricted");
    }

    [Fact]
    public async Task TestConnectionAsync_WithTcpPrefixedPrivateHost_ReturnsFailure()
    {
        var sut = CreateSut(resolver: _ => Task.FromResult(new[] { IPAddress.Parse("10.0.0.5") }));

        var result = await sut.TestConnectionAsync(
            ConnectorProviders.SqlServer,
            ConfigJson("{\"Host\":\"tcp:internal.example.com\",\"Database\":\"db\",\"Username\":\"u\",\"Password\":\"p\"}"));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("restricted");
    }

    [Fact]
    public async Task TestConnectionAsync_MongoSeedList_ValidatesEverySeedHost()
    {
        var capturedHosts = new List<string>();
        var sut = CreateSut(capturedHosts: capturedHosts);

        var result = await sut.TestConnectionAsync(
            ConnectorProviders.MongoDb,
            ConfigJson("{\"ConnectionString\":\"mongodb://public.example.com:27017,10.0.0.5:27017/db\"}"));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("restricted");
        capturedHosts.Should().Contain("public.example.com");
    }

    [Fact]
    public async Task TestConnectionAsync_MongoSrvWithRestrictedName_ReturnsFailure()
    {
        var capturedHosts = new List<string>();
        var sut = CreateSut(capturedHosts: capturedHosts);

        var result = await sut.TestConnectionAsync(
            ConnectorProviders.MongoDb,
            ConfigJson("{\"ConnectionString\":\"mongodb+srv://localhost/db\"}"));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("restricted");
        capturedHosts.Should().BeEmpty();
    }

    [Fact]
    public async Task TestConnectionAsync_MongoSrvWithIpLiteral_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.TestConnectionAsync(
            ConnectorProviders.MongoDb,
            ConfigJson("{\"ConnectionString\":\"mongodb+srv://169.254.169.254/db\"}"));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("IP literal");
    }
}
