using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Common.Interfaces;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MultiTenantETL.Infrastructure.Security;

/// <summary>
/// Service for encrypting and decrypting sensitive data using AES-GCM (authenticated encryption)
/// </summary>
public class EncryptionService : IEncryptionService
{
    private readonly byte[] _key;
    private readonly ILogger<EncryptionService> _logger;

    public EncryptionService(IConfiguration configuration, ILogger<EncryptionService> logger)
    {
        _logger = logger;

        // Get encryption key from configuration (should be in user secrets or environment variables)
        var encryptionKey = configuration["Encryption:Key"];

        if (string.IsNullOrEmpty(encryptionKey))
        {
            throw new InvalidOperationException(
                "Encryption key not configured. Set 'Encryption:Key' in user secrets or environment variables.");
        }

        // For AesGcm, we need a 32-byte key. If the config provides a passphrase, derive it with a salt.
        // But prefer a direct 32-byte key in base64.
        if (encryptionKey.Length == 44 && IsBase64String(encryptionKey)) // 32 bytes base64 is 44 chars
        {
            _key = Convert.FromBase64String(encryptionKey);
        }
        else
        {
            // Derive from passphrase with salt
            var saltString = configuration["Encryption:Salt"];
            if (string.IsNullOrEmpty(saltString))
            {
                throw new InvalidOperationException(
                    "Encryption salt not configured. Set 'Encryption:Salt' in user secrets or environment variables.");
            }
            var salt = Encoding.UTF8.GetBytes(saltString);
            _key = DeriveKey(encryptionKey, salt);
        }
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return plainText;
        }

        try
        {
            using var aesGcm = new AesGcm(_key, 16);
            var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
            RandomNumberGenerator.Fill(nonce);

            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var cipherBytes = new byte[plainBytes.Length];
            var tag = new byte[AesGcm.TagByteSizes.MaxSize];

            aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);

            // Combine nonce + tag + ciphertext
            var result = new byte[nonce.Length + tag.Length + cipherBytes.Length];
            Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
            Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
            Buffer.BlockCopy(cipherBytes, 0, result, nonce.Length + tag.Length, cipherBytes.Length);

            return Convert.ToBase64String(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt data");
            throw new InvalidOperationException("Encryption failed", ex);
        }
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
        {
            return cipherText;
        }

        try
        {
            var fullCipher = Convert.FromBase64String(cipherText);
            const int nonceSize = 12; // AesGcm.NonceByteSizes.MaxSize
            const int tagSize = 16; // AesGcm.TagByteSizes.MaxSize

            if (fullCipher.Length < nonceSize + tagSize)
            {
                throw new InvalidOperationException("Cipher text is too short.");
            }

            var nonce = new byte[nonceSize];
            var tag = new byte[tagSize];
            var cipherBytes = new byte[fullCipher.Length - nonceSize - tagSize];

            Buffer.BlockCopy(fullCipher, 0, nonce, 0, nonceSize);
            Buffer.BlockCopy(fullCipher, nonceSize, tag, 0, tagSize);
            Buffer.BlockCopy(fullCipher, nonceSize + tagSize, cipherBytes, 0, cipherBytes.Length);

            using var aesGcm = new AesGcm(_key, 16);
            var plainBytes = new byte[cipherBytes.Length];
            aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (FormatException ex)
        {
            _logger.LogError(ex, "Failed to decrypt data - invalid base64 format");
            throw new InvalidOperationException("Decryption failed: data is not valid base64", ex);
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Failed to decrypt data - integrity check failed");
            throw new InvalidOperationException("Decryption failed: data integrity compromised", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt data");
            throw new InvalidOperationException("Decryption failed", ex);
        }
    }

    public JsonElement EncryptJsonFields(JsonElement jsonElement, params string[] fieldsToEncrypt)
    {
        if (fieldsToEncrypt == null || fieldsToEncrypt.Length == 0)
        {
            return jsonElement;
        }

        try
        {
            var jsonString = jsonElement.GetRawText();
            var jsonDoc = JsonDocument.Parse(jsonString);
            var root = jsonDoc.RootElement;

            var modifiedJson = new Dictionary<string, object?>();

            foreach (var property in root.EnumerateObject())
            {
                if (fieldsToEncrypt.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                {
                    // Encrypt this field
                    var plainValue = property.Value.GetString();
                    if (!string.IsNullOrEmpty(plainValue))
                    {
                        modifiedJson[property.Name] = Encrypt(plainValue);
                    }
                    else
                    {
                        modifiedJson[property.Name] = plainValue;
                    }
                }
                else
                {
                    // Keep original value
                    modifiedJson[property.Name] = GetJsonValue(property.Value);
                }
            }

            var serialized = JsonSerializer.Serialize(modifiedJson);
            return JsonDocument.Parse(serialized).RootElement;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt JSON fields");
            throw new InvalidOperationException("JSON field encryption failed", ex);
        }
    }

    public JsonElement DecryptJsonFields(JsonElement jsonElement, params string[] fieldsToDecrypt)
    {
        if (fieldsToDecrypt == null || fieldsToDecrypt.Length == 0)
        {
            return jsonElement;
        }

        try
        {
            var jsonString = jsonElement.GetRawText();
            var jsonDoc = JsonDocument.Parse(jsonString);
            var root = jsonDoc.RootElement;

            var modifiedJson = new Dictionary<string, object?>();

            foreach (var property in root.EnumerateObject())
            {
                if (fieldsToDecrypt.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                {
                    // Decrypt this field
                    var encryptedValue = property.Value.GetString();
                    if (!string.IsNullOrEmpty(encryptedValue))
                    {
                        try
                        {
                            modifiedJson[property.Name] = Decrypt(encryptedValue);
                        }
                        catch
                        {
                            // If decryption fails, it might be plain text (backward compatibility)
                            _logger.LogWarning("Failed to decrypt field {FieldName}, using as plain text", property.Name);
                            modifiedJson[property.Name] = encryptedValue;
                        }
                    }
                    else
                    {
                        modifiedJson[property.Name] = encryptedValue;
                    }
                }
                else
                {
                    // Keep original value
                    modifiedJson[property.Name] = GetJsonValue(property.Value);
                }
            }

            var serialized = JsonSerializer.Serialize(modifiedJson);
            return JsonDocument.Parse(serialized).RootElement;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt JSON fields");
            throw new InvalidOperationException("JSON field decryption failed", ex);
        }
    }

    private static byte[] DeriveKey(string password, byte[] salt)
    {
        // Use PBKDF2 to derive a 256-bit key from the password
        using var deriveBytes = new Rfc2898DeriveBytes(
            password,
            salt,
            100000, // Iterations
            HashAlgorithmName.SHA256);

        return deriveBytes.GetBytes(32); // 32 bytes = 256 bits
    }

    private static bool IsBase64String(string base64)
    {
        try
        {
            Convert.FromBase64String(base64);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static object? GetJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt32(out var intValue) ? intValue : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Object => JsonSerializer.Deserialize<Dictionary<string, object>>(element.GetRawText()),
            JsonValueKind.Array => JsonSerializer.Deserialize<List<object>>(element.GetRawText()),
            _ => element.GetRawText()
        };
    }
}
