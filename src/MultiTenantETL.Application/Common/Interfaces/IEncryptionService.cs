namespace MultiTenantETL.Application.Common.Interfaces;

/// <summary>
/// Service for encrypting and decrypting sensitive data
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Encrypts a string value
    /// </summary>
    /// <param name="plainText">The plain text to encrypt</param>
    /// <returns>Base64 encoded encrypted string</returns>
    string Encrypt(string plainText);

    /// <summary>
    /// Decrypts an encrypted string value
    /// </summary>
    /// <param name="cipherText">The Base64 encoded encrypted string</param>
    /// <returns>Decrypted plain text</returns>
    string Decrypt(string cipherText);

    /// <summary>
    /// Encrypts a JSON element containing sensitive data
    /// </summary>
    /// <param name="jsonElement">The JSON element to encrypt</param>
    /// <param name="fieldsToEncrypt">List of field names to encrypt (e.g., "password", "apiKey")</param>
    /// <returns>JSON element with encrypted fields</returns>
    System.Text.Json.JsonElement EncryptJsonFields(System.Text.Json.JsonElement jsonElement, params string[] fieldsToEncrypt);

    /// <summary>
    /// Decrypts a JSON element containing encrypted sensitive data
    /// </summary>
    /// <param name="jsonElement">The JSON element with encrypted fields</param>
    /// <param name="fieldsToDecrypt">List of field names to decrypt</param>
    /// <returns>JSON element with decrypted fields</returns>
    System.Text.Json.JsonElement DecryptJsonFields(System.Text.Json.JsonElement jsonElement, params string[] fieldsToDecrypt);
}
