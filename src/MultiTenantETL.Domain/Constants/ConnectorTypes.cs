namespace MultiTenantETL.Domain.Constants;

/// <summary>
/// Connector type constants.
/// </summary>
public static class ConnectorTypes
{
    /// <summary>Database connector type.</summary>
    public const string Database = "Database";

    /// <summary>File connector type.</summary>
    public const string File = "File";

    /// <summary>API connector type.</summary>
    public const string Api = "API";

    /// <summary>Email connector type.</summary>
    public const string Email = "Email";
}

/// <summary>
/// Connector provider constants.
/// </summary>
public static class ConnectorProviders
{
    // Database providers

    /// <summary>SQL Server provider.</summary>
    public const string SqlServer = "SqlServer";

    /// <summary>PostgreSQL provider.</summary>
    public const string PostgreSQL = "PostgreSQL";

    /// <summary>MySQL provider.</summary>
    public const string MySQL = "MySQL";

    /// <summary>Oracle provider.</summary>
    public const string Oracle = "Oracle";

    /// <summary>MongoDB provider.</summary>
    public const string MongoDb = "MongoDb";

    /// <summary>Azure Cosmos DB provider.</summary>
    public const string CosmosDb = "CosmosDb";

    // File providers (storage locations)

    /// <summary>FTP provider.</summary>
    public const string FTP = "FTP";

    /// <summary>SFTP provider.</summary>
    public const string SFTP = "SFTP";

    /// <summary>Azure Blob Storage provider.</summary>
    public const string AzureBlob = "AzureBlob";

    // API providers

    /// <summary>REST API provider.</summary>
    public const string REST = "REST";

    // Email providers

    /// <summary>Email provider.</summary>
    public const string Email = "Email";
}

/// <summary>
/// Connector direction constants.
/// </summary>
public static class ConnectorDirections
{
    /// <summary>Can serve as a source.</summary>
    public const string Source = "source";

    /// <summary>Can serve as a destination.</summary>
    public const string Destination = "destination";

    /// <summary>Can serve as both source and destination.</summary>
    public const string Both = "both";
}

/// <summary>
/// Test result constants.
/// </summary>
public static class TestResults
{
    /// <summary>Test passed.</summary>
    public const string Success = "Success";

    /// <summary>Test failed.</summary>
    public const string Failed = "Failed";
}

/// <summary>
/// API response format constants.
/// </summary>
public static class ApiResponseFormats
{
    /// <summary>JSON format.</summary>
    public const string Json = "JSON";

    /// <summary>XML format.</summary>
    public const string Xml = "XML";
}
