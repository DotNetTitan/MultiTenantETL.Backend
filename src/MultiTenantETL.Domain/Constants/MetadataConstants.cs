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
        public static readonly (string Value, string LabelKey, string Icon)[] Types = new[]
        {
            ("string", "schema.dataTypes.string", "mdi-format-text"),
            ("integer", "schema.dataTypes.integer", "mdi-numeric"),
            ("bigInteger", "schema.dataTypes.bigInteger", "mdi-numeric"),
            ("decimal", "schema.dataTypes.decimal", "mdi-decimal"),
            ("boolean", "schema.dataTypes.boolean", "mdi-checkbox-marked"),
            ("date", "schema.dataTypes.date", "mdi-calendar"),
            ("dateTime", "schema.dataTypes.dateTime", "mdi-calendar-clock"),
            ("timestamp", "schema.dataTypes.timestamp", "mdi-clock-outline"),
            ("json", "schema.dataTypes.json", "mdi-code-json"),
            ("textLong", "schema.dataTypes.textLong", "mdi-text-long")
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
