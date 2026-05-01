namespace MultiTenantETL.Domain.Constants;

/// <summary>
/// Audit action constants for consistent logging
/// </summary>
public static class AuditActions
{
    public static class Authentication
    {
        public const string Login = "Auth.Login";
        public const string LoginFailed = "Auth.LoginFailed";
        public const string Logout = "Auth.Logout";
        public const string Register = "Auth.Register";
        public const string PasswordChanged = "Auth.PasswordChanged";
        public const string PasswordReset = "Auth.PasswordReset";
        public const string EmailConfirmed = "Auth.EmailConfirmed";
        public const string TenantSwitched = "Auth.TenantSwitched";
    }

    public static class Users
    {
        public const string Created = "User.Created";
        public const string Updated = "User.Updated";
        public const string Deleted = "User.Deleted";
        public const string Activated = "User.Activated";
        public const string Deactivated = "User.Deactivated";
        public const string RoleAssigned = "User.RoleAssigned";
        public const string RoleRemoved = "User.RoleRemoved";
        public const string AddedToTenant = "User.AddedToTenant";
        public const string RemovedFromTenant = "User.RemovedFromTenant";
        public const string TenantRoleUpdated = "User.TenantRoleUpdated";
    }

    public static class Tenants
    {
        public const string Created = "Tenant.Created";
        public const string Updated = "Tenant.Updated";
        public const string Deleted = "Tenant.Deleted";
        public const string UserAdded = "Tenant.UserAdded";
        public const string UserRemoved = "Tenant.UserRemoved";
        public const string UserRoleUpdated = "Tenant.UserRoleUpdated";
    }

    public static class Pipelines
    {
        public const string Created = "Pipeline.Created";
        public const string Updated = "Pipeline.Updated";
        public const string Deleted = "Pipeline.Deleted";
        public const string Executed = "Pipeline.Executed";
        public const string ExecutionFailed = "Pipeline.ExecutionFailed";
        public const string Activated = "Pipeline.Activated";
        public const string Deactivated = "Pipeline.Deactivated";
    }

    public static class Connectors
    {
        public const string Created = "Connector.Created";
        public const string Updated = "Connector.Updated";
        public const string Deleted = "Connector.Deleted";
        public const string Tested = "Connector.Tested";
        public const string TestFailed = "Connector.TestFailed";
        public const string SchemaDetected = "Connector.SchemaDetected";
    }

    // Backward compatibility constants
    public const string ConnectorCreated = "Connector.Created";
    public const string ConnectorUpdated = "Connector.Updated";
    public const string ConnectorDeleted = "Connector.Deleted";
    public const string ConnectorTested = "Connector.Tested";
    public const string ConnectorSchemaDetected = "Connector.SchemaDetected";

    // Backward compatibility constants for transformations (deprecated - transformations are now embedded in field mappings)
    [Obsolete("Transformation entity has been removed. Transformations are now embedded in Pipeline.FieldMappingsJson.")]
    public const string TransformationCreated = "Transformation.Created";
    [Obsolete("Transformation entity has been removed. Transformations are now embedded in Pipeline.FieldMappingsJson.")]
    public const string TransformationUpdated = "Transformation.Updated";
    [Obsolete("Transformation entity has been removed. Transformations are now embedded in Pipeline.FieldMappingsJson.")]
    public const string TransformationDeleted = "Transformation.Deleted";

    public static class Schedules
    {
        public const string Created = "Schedule.Created";
        public const string Updated = "Schedule.Updated";
        public const string Deleted = "Schedule.Deleted";
        public const string Enabled = "Schedule.Enabled";
        public const string Disabled = "Schedule.Disabled";
        public const string TriggeredManually = "Schedule.TriggeredManually";
        public const string PausedForPipeline = "Schedule.PausedForPipeline";
        public const string ResumedForPipeline = "Schedule.ResumedForPipeline";
    }
}
