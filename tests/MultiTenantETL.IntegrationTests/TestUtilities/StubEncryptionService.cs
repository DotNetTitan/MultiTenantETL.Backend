using System.Text.Json;
using MultiTenantETL.Application.Common.Interfaces;

namespace MultiTenantETL.IntegrationTests.TestUtilities;

/// <summary>
/// Stub implementation of IEncryptionService for integration tests.
/// Returns input unchanged since encryption is not the focus of these tests.
/// </summary>
public sealed class StubEncryptionService : IEncryptionService
{
    public string Encrypt(string plainText) => plainText;
    public string Decrypt(string cipherText) => cipherText;
    public JsonElement EncryptJsonFields(JsonElement jsonElement, params string[] fieldsToEncrypt) => jsonElement;
    public JsonElement DecryptJsonFields(JsonElement jsonElement, params string[] fieldsToDecrypt) => jsonElement;
}
