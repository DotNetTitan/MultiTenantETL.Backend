using FluentAssertions;
using MultiTenantETL.Infrastructure.Identity;

namespace MultiTenantETL.UnitTests.Identity;

/// <summary>
/// Unit tests for TenantProvider to verify basic functionality
/// </summary>
public class TenantProviderTests
{
    [Fact]
    public void TenantProvider_DefaultValues_AreNull()
    {
        // Arrange & Act
        var provider = new TenantProvider();

        // Assert
        provider.TenantId.Should().BeNull();
        provider.CorrelationId.Should().BeNull();
    }

    [Fact]
    public void TenantProvider_CanSetAndGetTenantId()
    {
        // Arrange
        var provider = new TenantProvider();
        var tenantId = Guid.NewGuid();

        // Act
        provider.TenantId = tenantId;

        // Assert
        provider.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public void TenantProvider_CanSetAndGetCorrelationId()
    {
        // Arrange
        var provider = new TenantProvider();
        var correlationId = Guid.NewGuid().ToString();

        // Act
        provider.CorrelationId = correlationId;

        // Assert
        provider.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public void TenantProvider_CanSetBothProperties()
    {
        // Arrange
        var provider = new TenantProvider();
        var tenantId = Guid.NewGuid();
        var correlationId = "test-correlation-123";

        // Act
        provider.TenantId = tenantId;
        provider.CorrelationId = correlationId;

        // Assert
        provider.TenantId.Should().Be(tenantId);
        provider.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public void TenantProvider_CanClearTenantId()
    {
        // Arrange
        var provider = new TenantProvider
        {
            TenantId = Guid.NewGuid()
        };

        // Act
        provider.TenantId = null;

        // Assert
        provider.TenantId.Should().BeNull();
    }

    [Fact]
    public void TenantProvider_CanClearCorrelationId()
    {
        // Arrange
        var provider = new TenantProvider
        {
            CorrelationId = "test-123"
        };

        // Act
        provider.CorrelationId = null;

        // Assert
        provider.CorrelationId.Should().BeNull();
    }

    [Fact]
    public void TenantProvider_MultipleInstances_AreIndependent()
    {
        // Arrange
        var provider1 = new TenantProvider();
        var provider2 = new TenantProvider();
        var tenantId1 = Guid.NewGuid();
        var tenantId2 = Guid.NewGuid();

        // Act
        provider1.TenantId = tenantId1;
        provider2.TenantId = tenantId2;

        // Assert
        provider1.TenantId.Should().Be(tenantId1);
        provider2.TenantId.Should().Be(tenantId2);
        provider1.TenantId.Should().NotBe(provider2.TenantId.GetValueOrDefault());
    }
}
