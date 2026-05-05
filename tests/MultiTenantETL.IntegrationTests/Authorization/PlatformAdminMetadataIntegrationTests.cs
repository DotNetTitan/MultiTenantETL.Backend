using FluentAssertions;
using Microsoft.Extensions.Configuration;
using MultiTenantETL.Infrastructure.Services;

namespace MultiTenantETL.IntegrationTests.Authorization;

public class PlatformAdminMetadataIntegrationTests
{
    [Fact]
    public void GetAppConstants_ShouldExposePlatformAdminRole()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenIddict:Clients:Spa:ClientId"] = "test-client"
            })
            .Build();

        var sut = new MetadataService(configuration);

        // Act
        var constants = sut.GetAppConstants();

        // Assert
        constants.Roles.PlatformAdmin.Should().Be("PlatformAdmin");
        constants.Roles.SuperAdmin.Should().Be("SuperAdmin");
        constants.Roles.TenantAdmin.Should().Be("TenantAdmin");
    }
}

