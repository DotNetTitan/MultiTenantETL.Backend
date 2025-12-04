namespace MultiTenantETL.Domain.Constants;

public static class TransformationTypes
{
    // Core transformation types
    public const string Filter = "filter";
    public const string Map = "map";
    public const string String = "string";
    public const string Script = "script";
    
    // Legacy/UI transformation types (for backward compatibility)
    public const string FilterLegacy = "Filter";
    public const string MapLegacy = "Map";
    public const string Trim = "Trim";
    public const string CaseConvert = "Case Convert";
    public const string Substring = "Substring";
    public const string Replace = "Replace";
    public const string ScriptLegacy = "Script";
}
