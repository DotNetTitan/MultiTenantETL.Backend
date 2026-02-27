namespace MultiTenantETL.Domain.Constants;

public static class ConnectorTypes
{
    public const string Database = "Database";
    public const string File = "File";
    public const string Api = "API";
    public const string Email = "Email";
}

public static class ConnectorProviders
{
    // Database providers
    public const string SqlServer = "SqlServer";
    public const string PostgreSQL = "PostgreSQL";
    public const string MySQL = "MySQL";
    public const string Oracle = "Oracle";
    public const string MongoDb = "MongoDb";
    public const string CosmosDb = "CosmosDb";
    
    // File providers (storage locations)
    public const string FTP = "FTP";
    public const string SFTP = "SFTP";
    public const string AzureBlob = "AzureBlob";
    
    // API providers
    public const string REST = "REST";
    
    // Email providers
    public const string Email = "Email";
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
