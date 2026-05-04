using MultiTenantETL.Application.Common.Interfaces;
using System.Text.RegularExpressions;

namespace MultiTenantETL.Infrastructure.Security;

/// <summary>
/// Service for sanitizing user input to prevent SQL injection and XSS attacks
/// </summary>
public class InputSanitizer : IInputSanitizer
{
    /// <summary>
    /// Sanitizes a string by removing potentially dangerous characters and patterns
    /// </summary>
    public string SanitizeString(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // Remove leading/trailing whitespace
        input = input.Trim();

        // Remove control characters (0x00-0x1F except newline/tab)
        input = Regex.Replace(input, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", "");

        // Remove common SQL injection patterns
        // Note: This is defense in depth - EF Core parameterization is the primary defense
        input = Regex.Replace(input, @"('|(--)|;|\/\*|\*\/|xp_|sp_)", "", RegexOptions.IgnoreCase);

        // Remove script tags
        input = Regex.Replace(input, @"<script[^>]*>.*?</script>", "", RegexOptions.IgnoreCase);

        // Remove javascript: protocol
        input = Regex.Replace(input, @"javascript:", "", RegexOptions.IgnoreCase);

        // Remove on* event handlers
        input = Regex.Replace(input, @"on\w+\s*=", "", RegexOptions.IgnoreCase);

        return input;
    }

    /// <summary>
    /// Sanitizes HTML content by encoding dangerous characters
    /// </summary>
    public string SanitizeHtml(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // Use built-in HTML encoding
        return System.Net.WebUtility.HtmlEncode(input);
    }
}
