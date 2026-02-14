using Microsoft.Extensions.Logging.Abstractions;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Infrastructure.Security;

namespace MultiTenantETL.IntegrationTests.TestUtilities;

/// <summary>
/// Stub implementation of ISecretResolver for integration tests.
/// Returns input unchanged since Key Vault integration is not the focus of these tests.
/// </summary>
public sealed class StubSecretResolver : SecretResolver
{
    public StubSecretResolver() 
        : base(CreateStubSecretStorageService(), NullLogger<SecretResolver>.Instance)
    {
    }

    private static ISecretStorageService CreateStubSecretStorageService()
    {
        // Return a stub service that does nothing - integration tests use real connection strings
        return new StubSecretStorageService();
    }

    /// <summary>
    /// Stub secret storage service that does nothing - integration tests don't use Key Vault
    /// </summary>
    private class StubSecretStorageService : ISecretStorageService
    {
        public Task StoreSecretAsync(string secretName, string secretValue, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<string?> GetSecretAsync(string secretName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }

        public Task DeleteSecretAsync(string secretName, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<bool> SecretExistsAsync(string secretName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public string GenerateSecretName(Guid tenantId, Guid connectorId, string fieldName)
        {
            return $"stub-{tenantId:N}-{connectorId:N}-{fieldName}";
        }
    }
}
