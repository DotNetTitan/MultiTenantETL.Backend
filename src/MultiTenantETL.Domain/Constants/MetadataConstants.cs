namespace MultiTenantETL.Domain.Constants;

/// <summary>
/// Domain constants for ETL platform metadata
/// These define the available connector types, transformations, and data types
/// </summary>
public static class MetadataConstants
{
    public static class ConnectorTypes
    {
        public static readonly (string Value, string LabelKey, string Icon)[] Types = new[]
        {
            (Constants.ConnectorTypes.Database, "connectors.database", "mdi-database"),
            (Constants.ConnectorTypes.Api, "connectors.api", "mdi-api"),
            (Constants.ConnectorTypes.File, "connectors.file", "mdi-file-document")
        };
    }

    public static class ConnectorProviders
    {
        public static readonly Dictionary<string, string[]> ProvidersByType = new()
        {
            { 
                Constants.ConnectorTypes.Database, 
                new[] 
                { 
                    Constants.ConnectorProviders.SqlServer, 
                    Constants.ConnectorProviders.PostgreSQL, 
                    Constants.ConnectorProviders.MySQL,
                    Constants.ConnectorProviders.Oracle,
                    Constants.ConnectorProviders.Snowflake,
                    Constants.ConnectorProviders.BigQuery
                } 
            },
            { 
                Constants.ConnectorTypes.Api, 
                new[] 
                { 
                    Constants.ConnectorProviders.REST 
                } 
            },
            { 
                Constants.ConnectorTypes.File, 
                new[] 
                { 
                    Constants.ConnectorProviders.Local, 
                    Constants.ConnectorProviders.FTP, 
                    Constants.ConnectorProviders.SFTP, 
                    Constants.ConnectorProviders.S3, 
                    Constants.ConnectorProviders.AzureBlob,
                    Constants.ConnectorProviders.GCS
                } 
            }
        };

        /// <summary>
        /// Provider icons and colors for UI display
        /// </summary>
        public static readonly Dictionary<string, (string Icon, string Color)> ProviderMetadata = new()
        {
            // Database providers
            { Constants.ConnectorProviders.SqlServer, ("mdi-database", "blue-darken-2") },
            { Constants.ConnectorProviders.PostgreSQL, ("mdi-database", "blue-darken-2") },
            { Constants.ConnectorProviders.MySQL, ("mdi-database", "blue-darken-2") },
            { Constants.ConnectorProviders.Oracle, ("mdi-database", "blue-darken-2") },
            { Constants.ConnectorProviders.Snowflake, ("mdi-snowflake", "blue-darken-2") },
            { Constants.ConnectorProviders.BigQuery, ("mdi-google-cloud", "blue-darken-2") },
            
            // File providers
            { Constants.ConnectorProviders.Local, ("mdi-folder", "grey-darken-1") },
            { Constants.ConnectorProviders.FTP, ("mdi-server-network", "green-darken-1") },
            { Constants.ConnectorProviders.SFTP, ("mdi-server-security", "green-darken-2") },
            { Constants.ConnectorProviders.S3, ("mdi-aws", "orange-darken-2") },
            { Constants.ConnectorProviders.AzureBlob, ("mdi-microsoft-azure", "blue-lighten-1") },
            { Constants.ConnectorProviders.GCS, ("mdi-google-cloud", "blue-lighten-1") },
            
            // API providers
            { Constants.ConnectorProviders.REST, ("mdi-api", "purple-darken-1") }
        };
    }

    public static class ConnectorDirections
    {
        public static readonly (string Value, string LabelKey, string Icon)[] Directions = new[]
        {
            (Constants.ConnectorDirections.Source, "connectors.sourceOnly", "mdi-export"),
            (Constants.ConnectorDirections.Destination, "connectors.destinationOnly", "mdi-import"),
            (Constants.ConnectorDirections.Both, "connectors.both", "mdi-swap-horizontal")
        };
    }

    public static class AuthTypes
    {
        public static readonly (string Value, string LabelKey)[] Types = new[]
        {
            ("None", "connectors.authNone"),
            ("Basic", "connectors.authBasic"),
            ("Bearer", "connectors.authBearer"),
            ("API Key", "connectors.authApiKey")
        };
    }

    public static class FileFormats
    {
        public static readonly (string Value, string LabelKey, string Extension)[] Formats = new[]
        {
            ("CSV", "common.csv", ".csv"),
            ("JSON", "common.json", ".json"),
            ("Excel", "common.excel", ".xlsx"),
            ("XML", "common.xml", ".xml"),
            ("Parquet", "common.parquet", ".parquet")
        };
    }

    public static class WriteOperations
    {
        public static readonly (string Value, string LabelKey, string DescriptionKey, bool RequiresPrimaryKey)[] Operations = new[]
        {
            ("INSERT", "connectors.writeOperationInsert", "connectors.insertDescription", false),
            ("UPDATE", "connectors.writeOperationUpdate", "connectors.updateDescription", true),
            ("UPSERT", "connectors.writeOperationUpsert", "connectors.upsertDescription", true),
            ("BULK_INSERT", "connectors.writeOperationBulkInsert", "connectors.bulkInsertDescription", false)
        };
    }

    public static class HttpMethods
    {
        public static readonly (string Value, string LabelKey, string Color)[] Methods = new[]
        {
            ("GET", "common.httpGet", "success"),
            ("POST", "common.httpPost", "primary"),
            ("PUT", "common.httpPut", "warning"),
            ("PATCH", "common.httpPatch", "info"),
            ("DELETE", "common.httpDelete", "error")
        };
    }

    public static class TransformationTypes
    {
        public static readonly (string Value, string LabelKey, string Icon, string CategoryKey, string DescriptionKey)[] Types = new[]
        {
            ("Filter", "transformations.filter", "mdi-filter", "transformations.categoryDataQuality", "transformations.filterDescription"),
            ("Map", "transformations.map", "mdi-map", "transformations.categoryTransformation", "transformations.mapDescription"),
            ("Script", "transformations.script", "mdi-code-braces", "transformations.categoryCustom", "transformations.scriptDescription"),
            ("Trim", "transformations.trim", "mdi-content-cut", "transformations.categoryText", "transformations.trimDescription"),
            ("CaseConvert", "transformations.case", "mdi-format-letter-case", "transformations.categoryText", "transformations.caseDescription"),
            ("Substring", "transformations.substring", "mdi-text-box-outline", "transformations.categoryText", "transformations.substringDescription"),
            ("Replace", "transformations.replace", "mdi-find-replace", "transformations.categoryText", "transformations.replaceDescription")
        };
    }

    public static class DataTypes
    {
        public static readonly (string Value, string Label, string Icon, string Category)[] Types = new[]
        {
            // String types
            ("varchar", "String (Varchar)", "mdi-text", "string"),
            ("char", "Char", "mdi-text", "string"),
            ("text", "Text (Long)", "mdi-text-long", "string"),
            ("nvarchar", "NVarchar (Unicode)", "mdi-text", "string"),
            ("nchar", "NChar (Unicode)", "mdi-text", "string"),
            ("ntext", "NText (Unicode)", "mdi-text-long", "string"),
            
            // Numeric types
            ("int", "Integer", "mdi-numeric", "numeric"),
            ("bigint", "Big Integer", "mdi-numeric", "numeric"),
            ("smallint", "Small Integer", "mdi-numeric", "numeric"),
            ("tinyint", "Tiny Integer", "mdi-numeric", "numeric"),
            ("decimal", "Decimal", "mdi-decimal", "numeric"),
            ("numeric", "Numeric", "mdi-decimal", "numeric"),
            ("float", "Float", "mdi-decimal", "numeric"),
            ("real", "Real", "mdi-decimal", "numeric"),
            ("money", "Money", "mdi-currency-usd", "numeric"),
            ("smallmoney", "Small Money", "mdi-currency-usd", "numeric"),
            
            // Boolean
            ("boolean", "Boolean", "mdi-checkbox-marked", "boolean"),
            ("bit", "Bit", "mdi-checkbox-marked", "boolean"),
            
            // Date/Time types
            ("date", "Date", "mdi-calendar", "datetime"),
            ("datetime", "Date Time", "mdi-calendar-clock", "datetime"),
            ("datetime2", "DateTime2", "mdi-calendar-clock", "datetime"),
            ("smalldatetime", "Small DateTime", "mdi-calendar-clock", "datetime"),
            ("time", "Time", "mdi-clock", "datetime"),
            ("timestamp", "Timestamp", "mdi-clock", "datetime"),
            ("datetimeoffset", "DateTime Offset", "mdi-calendar-clock", "datetime"),
            
            // UUID/GUID
            ("uuid", "UUID/GUID", "mdi-identifier", "identifier"),
            ("uniqueidentifier", "Unique Identifier", "mdi-identifier", "identifier"),
            
            // Binary types
            ("binary", "Binary", "mdi-file-code", "binary"),
            ("varbinary", "VarBinary", "mdi-file-code", "binary"),
            ("image", "Image", "mdi-image", "binary"),
            
            // JSON/XML
            ("json", "JSON", "mdi-code-json", "structured"),
            ("jsonb", "JSONB (Binary)", "mdi-code-json", "structured"),
            ("xml", "XML", "mdi-xml", "structured"),
            
            // PostgreSQL specific
            ("serial", "Serial (Auto-increment)", "mdi-numeric", "numeric"),
            ("bigserial", "Big Serial", "mdi-numeric", "numeric"),
            ("inet", "IP Address", "mdi-ip-network", "network"),
            ("cidr", "CIDR", "mdi-ip-network", "network"),
            ("macaddr", "MAC Address", "mdi-network", "network"),
            ("array", "Array", "mdi-code-brackets", "structured"),
            
            // Spatial
            ("geometry", "Geometry", "mdi-vector-polygon", "spatial"),
            ("geography", "Geography", "mdi-earth", "spatial")
        };
    }

    public static class ScheduleFrequencies
    {
        public static readonly (string Value, string LabelKey, string Icon)[] Frequencies = new[]
        {
            ("daily", "pipelines.daily", "mdi-calendar-today"),
            ("weekly", "pipelines.weekly", "mdi-calendar-week"),
            ("monthly", "pipelines.monthly", "mdi-calendar-month"),
            ("custom", "pipelines.custom", "mdi-cog")
        };
    }

    public static class DaysOfWeek
    {
        public static readonly (string Value, string LabelKey, string ShortKey)[] Days = new[]
        {
            ("monday", "pipelines.monday", "common.mon"),
            ("tuesday", "pipelines.tuesday", "common.tue"),
            ("wednesday", "pipelines.wednesday", "common.wed"),
            ("thursday", "pipelines.thursday", "common.thu"),
            ("friday", "pipelines.friday", "common.fri"),
            ("saturday", "pipelines.saturday", "common.sat"),
            ("sunday", "pipelines.sunday", "common.sun")
        };
    }

    public static class OAuth
    {
        /// <summary>
        /// Default OAuth scopes for the application
        /// </summary>
        public static readonly string[] DefaultScopes = new[]
        {
            "openid",
            "email",
            "profile",
            "roles",
            "api",
            "offline_access"
        };

        /// <summary>
        /// OAuth endpoint paths (relative to base URL)
        /// </summary>
        public const string AuthorizeEndpoint = "/connect/authorize";
        public const string TokenEndpoint = "/connect/token";
        public const string RevokeEndpoint = "/connect/revoke";
    }

    public static class SupportedLanguages
    {
        public static readonly (string Code, string Name, string NativeName)[] Languages = new[]
        {
            ("en", "English", "English"),
            ("es", "Spanish", "Español"),
            ("fr", "French", "Français"),
            ("de", "German", "Deutsch"),
            ("it", "Italian", "Italiano"),
            ("pt", "Portuguese", "Português")
        };
    }
}
