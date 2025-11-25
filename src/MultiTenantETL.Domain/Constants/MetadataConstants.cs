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
            ("Database", "connectors.database", "mdi-database"),
            ("API", "connectors.api", "mdi-api"),
            ("File", "connectors.file", "mdi-file-document")
        };
    }

    public static class ConnectorProviders
    {
        public static readonly Dictionary<string, string[]> ProvidersByType = new()
        {
            { "Database", new[] { "SqlServer", "PostgreSQL", "MySQL" } },
            { "API", new[] { "REST" } },
            { "File", new[] { "Local", "FTP", "S3", "AzureBlob" } }
        };
    }

    public static class ConnectorDirections
    {
        public static readonly (string Value, string LabelKey, string Icon)[] Directions = new[]
        {
            ("source", "connectors.sourceOnly", "mdi-export"),
            ("destination", "connectors.destinationOnly", "mdi-import"),
            ("both", "connectors.both", "mdi-swap-horizontal")
        };
    }

    public static class AuthTypes
    {
        public static readonly (string Value, string LabelKey)[] Types = new[]
        {
            ("None", "connectors.authNone"),
            ("Basic", "connectors.authBasic"),
            ("Bearer", "connectors.authBearer"),
            ("OAuth2", "connectors.authOAuth2"),
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
            ("Case", "transformations.case", "mdi-format-letter-case", "transformations.categoryText", "transformations.caseDescription"),
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
}
