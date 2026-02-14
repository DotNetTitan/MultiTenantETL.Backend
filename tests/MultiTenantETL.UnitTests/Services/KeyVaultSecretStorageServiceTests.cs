using System.Text.Json;
using Azure;
using Azure.Security.KeyVault.Secrets;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiTenantETL.Infrastructure.Configuration;
using MultiTenantETL.Infrastructure.Security;
using NSubstitute;

namespace MultiTenantETL.UnitTests.Services;

public class KeyVaultSecretStorageServiceTests
{
    private readonly ILogger<KeyVaultSecretStorageService> _logger;
    private readonly IOptions<AzureKeyVaultSettings> _settings;

    public KeyVaultSecretStorageServiceTests()
    {
        _logger = Substitute.For<ILogger<KeyVaultSecretStorageService>>();
        
        var keyVaultSettings = new AzureKeyVaultSettings
        {
            VaultUri = "https://test-vault.vault.azure.net/",
            UseKeyVault = true,
            SecretNamePrefix = "connector"
        };
        _settings = Options.Create(keyVaultSettings);
    }

    [Fact]
    public void GenerateSecretName_ValidInputs_ReturnsFormattedName()
    {
        // Arrange
        var sut = CreateServiceMock();
        var tenantId = Guid.NewGuid();
        var connectorId = Guid.NewGuid();
        var fieldName = "password";

        // Act
        var secretName = sut.GenerateSecretName(tenantId, connectorId, fieldName);

        // Assert
        secretName.Should().StartWith("connector-");
        secretName.Should().Contain(tenantId.ToString("N"));
        secretName.Should().Contain(connectorId.ToString("N"));
        secretName.Should().Contain("password");
        secretName.Should().MatchRegex("^[a-z0-9-]+$"); // Azure Key Vault name restrictions
        secretName.Length.Should().BeLessThanOrEqualTo(127); // Azure Key Vault limit
    }

    [Fact]
    public void GenerateSecretName_UppercaseFieldName_ConvertsToLowercase()
    {
        // Arrange
        var sut = CreateServiceMock();
        var fieldName = "ApiKey";

        // Act
        var secretName = sut.GenerateSecretName(Guid.NewGuid(), Guid.NewGuid(), fieldName);

        // Assert
        secretName.Should().Contain("apikey");
        secretName.Should().NotContain("ApiKey");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void GenerateSecretName_InvalidFieldName_ThrowsArgumentException(string? invalidFieldName)
    {
        // Arrange
        var sut = CreateServiceMock();

        // Act
        Action act = () => sut.GenerateSecretName(Guid.NewGuid(), Guid.NewGuid(), invalidFieldName!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*fieldName*");
    }

    [Fact]
    public void GenerateSecretName_EmptyTenantId_ThrowsArgumentException()
    {
        // Arrange
        var sut = CreateServiceMock();

        // Act
        Action act = () => sut.GenerateSecretName(Guid.Empty, Guid.NewGuid(), "password");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*tenantId*");
    }

    [Fact]
    public void GenerateSecretName_EmptyConnectorId_ThrowsArgumentException()
    {
        // Arrange
        var sut = CreateServiceMock();

        // Act
        Action act = () => sut.GenerateSecretName(Guid.NewGuid(), Guid.Empty, "password");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*connectorId*");
    }

    [Fact]
    public void Constructor_WithNullSettings_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new KeyVaultSecretStorageService(null!, _logger);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("settings");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new KeyVaultSecretStorageService(_settings, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void GenerateSecretName_WithDifferentPrefixInSettings_UsesCustomPrefix()
    {
        // Arrange
        var customSettings = Options.Create(new AzureKeyVaultSettings
        {
            VaultUri = "https://test-vault.vault.azure.net/",
            UseKeyVault = true,
            SecretNamePrefix = "custom"
        });
        var sut = new KeyVaultSecretStorageServiceTestHelper(customSettings, _logger);

        // Act
        var secretName = sut.GenerateSecretName(Guid.NewGuid(), Guid.NewGuid(), "test");

        // Assert
        secretName.Should().StartWith("custom-");
    }

    [Fact]
    public void GenerateSecretName_LongInputs_TruncatesWithinAzureLimit()
    {
        // Arrange
        var sut = CreateServiceMock();
        var tenantId = Guid.NewGuid();
        var connectorId = Guid.NewGuid();
        var longFieldName = new string('a', 100);

        // Act
        var secretName = sut.GenerateSecretName(tenantId, connectorId, longFieldName);

        // Assert
        secretName.Length.Should().BeLessThanOrEqualTo(127);
    }

    [Fact]
    public void GenerateSecretName_SpecialCharacters_SanitizesToValidFormat()
    {
        // Arrange
        var sut = CreateServiceMock();
        var fieldNameWithSpecialChars = "my_Field@Name#123";

        // Act
        var secretName = sut.GenerateSecretName(Guid.NewGuid(), Guid.NewGuid(), fieldNameWithSpecialChars);

        // Assert
        secretName.Should().MatchRegex("^[a-z0-9-]+$");
        secretName.Should().NotContain("_");
        secretName.Should().NotContain("@");
        secretName.Should().NotContain("#");
    }

    // Helper to access public GenerateSecretName method
    private KeyVaultSecretStorageServiceTestHelper CreateServiceMock()
    {
        return new KeyVaultSecretStorageServiceTestHelper(_settings, _logger);
    }

    // Test helper class to expose protected methods
    private class KeyVaultSecretStorageServiceTestHelper : KeyVaultSecretStorageService
    {
        public KeyVaultSecretStorageServiceTestHelper(
            IOptions<AzureKeyVaultSettings> settings,
            ILogger<KeyVaultSecretStorageService> logger)
            : base(settings, logger)
        {
        }

        // Expose GenerateSecretName for testing
        public new string GenerateSecretName(Guid tenantId, Guid connectorId, string fieldName)
        {
            return base.GenerateSecretName(tenantId, connectorId, fieldName);
        }
    }
}
