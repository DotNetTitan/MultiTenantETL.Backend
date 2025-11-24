namespace MultiTenantETL.Domain.Constants;

public static class ConnectorTypes
{
    public const string Database = "Database";
    public const string File = "File";
    public const string Api = "API";
}

public static class ConnectorProviders
{
    // Database providers
    public const string SqlServer = "SqlServer";
    public const string PostgreSQL = "PostgreSQL";
    public const string MySQL = "MySQL";
    
    // File providers (storage locations)
    public const string Local = "Local";
    public const string FTP = "FTP";
    public const string S3 = "S3";
    public const string AzureBlob = "AzureBlob";
    
    // API providers
    public const string REST = "REST";
}

public static class ConnectorDirections
{
    public const string Source = "source";
    public const string Destination = "destination";
    public const string Both = "both";
}

public static class TestResults
{
    public const string Success = "Success";
    public const string Failed = "Failed";
}
