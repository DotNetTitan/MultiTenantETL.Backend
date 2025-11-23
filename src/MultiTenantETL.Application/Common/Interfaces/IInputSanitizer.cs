namespace MultiTenantETL.Application.Common.Interfaces;

/// <summary>
/// Service for sanitizing user input to prevent injection attacks
/// </summary>
public interface IInputSanitizer
{
    /// <summary>
    /// Sanitizes a string by removing potentially dangerous characters and patterns
    /// </summary>
    string SanitizeString(string input);

    /// <summary>
    /// Sanitizes HTML content by encoding dangerous characters
    /// </summary>
    string SanitizeHtml(string input);
}
