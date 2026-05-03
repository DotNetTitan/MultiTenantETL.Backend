namespace MultiTenantETL.Domain.Constants;

/// <summary>
/// Constants related to encryption and sensitive data handling.
/// </summary>
public static class EncryptionConstants
{
    /// <summary>
    /// Prefix used to indicate a field value is a Key Vault secret reference.
    /// Format: keyvault:{secretName}
    /// </summary>
    public const string SecretReferencePrefix = "keyvault:";

    /// <summary>
    /// Field names that contain sensitive data and should be encrypted.
    /// Used for encrypting/decrypting connector configurations.
    /// </summary>
    public static readonly string[] SensitiveFields = new[]
    {
        "password",
        "Password",
        "apiKey",
        "ApiKey",
        "secret",
        "Secret",
        "accessKey",
        "AccessKey",
        "secretKey",
        "SecretKey",
        "connectionString",
        "ConnectionString",
        "privateKey",
        "PrivateKey"
    };
}
