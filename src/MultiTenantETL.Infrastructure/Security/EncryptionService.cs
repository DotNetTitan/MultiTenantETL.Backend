using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Common.Interfaces;

namespace MultiTenantETL.Infrastructure.Security;

/// <summary>
/// Service for encrypting and decrypting sensitive data using AES-256
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

        // Ensure key is 32 bytes (256 bits) for AES-256
        _key = DeriveKey(encryptionKey);
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return plainText;
        }

        try
        {
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using var msEncrypt = new MemoryStream();
            
            // Write IV to the beginning of the stream
            msEncrypt.Write(aes.IV, 0, aes.IV.Length);
            
            using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
            using (var swEncrypt = new StreamWriter(csEncrypt))
            {
                swEncrypt.Write(plainText);
            }

            return Convert.ToBase64String(msEncrypt.ToArray());
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

            using var aes = Aes.Create();
            aes.Key = _key;

            // Check if the cipher text is long enough to contain IV
            if (fullCipher.Length < aes.IV.Length)
            {
                throw new InvalidOperationException(
                    $"Cipher text is too short ({fullCipher.Length} bytes). Expected at least {aes.IV.Length} bytes for IV.");
            }

            // Extract IV from the beginning of the cipher text
            var iv = new byte[aes.IV.Length];
            Array.Copy(fullCipher, 0, iv, 0, iv.Length);
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using var msDecrypt = new MemoryStream(fullCipher, iv.Length, fullCipher.Length - iv.Length);
            using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
            using var srDecrypt = new StreamReader(csDecrypt);
            
            return srDecrypt.ReadToEnd();
        }
        catch (FormatException ex)
        {
            _logger.LogError(ex, "Failed to decrypt data - invalid base64 format");
            throw new InvalidOperationException("Decryption failed: data is not valid base64", ex);
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

    private static byte[] DeriveKey(string password)
    {
        // Use PBKDF2 to derive a 256-bit key from the password
        using var deriveBytes = new Rfc2898DeriveBytes(
            password,
            Encoding.UTF8.GetBytes("MultiTenantETL.Salt.v1"), // Salt (should be unique per installation)
            100000, // Iterations
            HashAlgorithmName.SHA256);
        
        return deriveBytes.GetBytes(32); // 32 bytes = 256 bits
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
