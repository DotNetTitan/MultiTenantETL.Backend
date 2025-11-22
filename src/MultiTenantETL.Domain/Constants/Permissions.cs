namespace MultiTenantETL.Domain.Constants;

/// <summary>
/// Centralized permission constants for the application.
/// Format: {Resource}.{Action} (e.g., "users.create", "pipelines.delete")
/// Supports wildcards: {Resource}.* or *.{Action}
/// </summary>
public static class Permissions
{
    /// <summary>
    /// System-level permissions (SuperAdmin only)
    /// </summary>
    public static class System
    {
        public const string Manage = "system.manage";
    }

    /// <summary>
    /// Tenant management permissions
    /// </summary>
    public static class Tenants
    {
        public const string Create = "tenants.create";
        public const string Read = "tenants.read";
        public const string Update = "tenants.update";
        public const string Delete = "tenants.delete";
        public const string Manage = "tenants.manage";
        public const string All = "tenants.*";
    }

    /// <summary>
    /// User management permissions
    /// </summary>
    public static class Users
    {
        public const string Create = "users.create";
        public const string Read = "users.read";
        public const string Update = "users.update";
        public const string Delete = "users.delete";
        public const string Manage = "users.manage";
        public const string All = "users.*";
    }

    /// <summary>
    /// Role management permissions
    /// </summary>
    public static class Roles
    {
        public const string Create = "roles.create";
        public const string Read = "roles.read";
        public const string Update = "roles.update";
        public const string Delete = "roles.delete";
        public const string Manage = "roles.manage";
        public const string All = "roles.*";
    }

    /// <summary>
    /// Tenant-specific settings permissions
    /// </summary>
    public static class TenantSettings
    {
        public const string Read = "tenant.settings.read";
        public const string Manage = "tenant.settings.manage";
    }

    /// <summary>
    /// Tenant data permissions
    /// </summary>
    public static class TenantData
    {
        public const string Read = "tenant.data.read";
        public const string Write = "tenant.data.write";
        public const string Delete = "tenant.data.delete";
        public const string Manage = "tenant.data.manage";
        public const string All = "tenant.data.*";
    }

    /// <summary>
    /// ETL Pipeline permissions
    /// </summary>
    public static class Pipelines
    {
        public const string Create = "pipelines.create";
        public const string Read = "pipelines.read";
        public const string Update = "pipelines.update";
        public const string Delete = "pipelines.delete";
        public const string Execute = "pipelines.execute";
        public const string View = "pipelines.view";
        public const string Manage = "pipelines.manage";
        public const string All = "pipelines.*";
    }

    /// <summary>
    /// ETL Connector permissions
    /// </summary>
    public static class Connectors
    {
        public const string Create = "connectors.create";
        public const string Read = "connectors.read";
        public const string Update = "connectors.update";
        public const string Delete = "connectors.delete";
        public const string Test = "connectors.test";
        public const string Manage = "connectors.manage";
        public const string All = "connectors.*";
    }

    /// <summary>
    /// ETL Transformation permissions
    /// </summary>
    public static class Transformations
    {
        public const string Create = "transformations.create";
        public const string Read = "transformations.read";
        public const string Update = "transformations.update";
        public const string Delete = "transformations.delete";
        public const string Manage = "transformations.manage";
        public const string All = "transformations.*";
    }

    /// <summary>
    /// ETL Execution permissions
    /// </summary>
    public static class Executions
    {
        public const string View = "executions.view";
        public const string Read = "executions.read";
        public const string Cancel = "executions.cancel";
        public const string Manage = "executions.manage";
        public const string All = "executions.*";
    }

    /// <summary>
    /// Dashboard permissions
    /// </summary>
    public static class Dashboard
    {
        public const string Read = "dashboard.read";
        public const string All = "dashboard.*";
    }

    /// <summary>
    /// General ETL permissions (for backwards compatibility)
    /// </summary>
    public static class ETL
    {
        public const string View = "etl.view";
        public const string Execute = "etl.execute";
        public const string Manage = "etl.manage";
        public const string All = "etl.*";
    }

    /// <summary>
    /// Wildcard permissions
    /// </summary>
    public static class Wildcards
    {
        /// <summary>
        /// Super admin - can do anything
        /// </summary>
        public const string SuperAdmin = "*:*";

        /// <summary>
        /// Read access to all resources
        /// </summary>
        public const string ReadAll = "*:read";

        /// <summary>
        /// Create access to all resources
        /// </summary>
        public const string CreateAll = "*:create";
    }
}
