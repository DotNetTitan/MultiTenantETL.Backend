namespace MultiTenantETL.Application.Metadata;

/// <summary>
/// Application constants that should be shared between backend and frontend
/// </summary>
public class AppConstantsDto
{
    public RolesDto Roles { get; set; } = new();
    public OAuthConfigDto OAuthConfig { get; set; } = new();
    public List<SupportedLanguageDto> SupportedLanguages { get; set; } = new();
}

public class RolesDto
{
    public string SuperAdmin { get; set; } = string.Empty;
    public string PlatformAdmin { get; set; } = string.Empty;
    public string TenantAdmin { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string Viewer { get; set; } = string.Empty;
}

public class OAuthConfigDto
{
    public string ClientId { get; set; } = string.Empty;
    public List<string> Scopes { get; set; } = new();
    public string AuthorizeEndpoint { get; set; } = string.Empty;
    public string TokenEndpoint { get; set; } = string.Empty;
    public string RevokeEndpoint { get; set; } = string.Empty;
}

public class SupportedLanguageDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NativeName { get; set; } = string.Empty;
}
