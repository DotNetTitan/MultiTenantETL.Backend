namespace MultiTenantETL.Domain.Constants;

/// <summary>
/// Audit action constants for consistent logging.
/// </summary>
public static class AuditActions
{
    /// <summary>
    /// Authentication audit actions.
    /// </summary>
    public static class Authentication
    {
        /// <summary>User logged in.</summary>
        public const string Login = "Auth.Login";

        /// <summary>User login failed.</summary>
        public const string LoginFailed = "Auth.LoginFailed";

        /// <summary>User logged out.</summary>
        public const string Logout = "Auth.Logout";

        /// <summary>User registered.</summary>
        public const string Register = "Auth.Register";

        /// <summary>User changed password.</summary>
        public const string PasswordChanged = "Auth.PasswordChanged";

        /// <summary>User reset password.</summary>
        public const string PasswordReset = "Auth.PasswordReset";

        /// <summary>User confirmed email.</summary>
        public const string EmailConfirmed = "Auth.EmailConfirmed";

        /// <summary>User switched tenant.</summary>
        public const string TenantSwitched = "Auth.TenantSwitched";
    }

    /// <summary>
    /// User audit actions.
    /// </summary>
    public static class Users
    {
        /// <summary>User was created.</summary>
        public const string Created = "User.Created";

        /// <summary>User was updated.</summary>
        public const string Updated = "User.Updated";

        /// <summary>User was deleted.</summary>
        public const string Deleted = "User.Deleted";

        /// <summary>User was activated.</summary>
        public const string Activated = "User.Activated";

        /// <summary>User was deactivated.</summary>
        public const string Deactivated = "User.Deactivated";

        /// <summary>Role was assigned to user.</summary>
        public const string RoleAssigned = "User.RoleAssigned";

        /// <summary>Role was removed from user.</summary>
        public const string RoleRemoved = "User.RoleRemoved";

        /// <summary>User was added to tenant.</summary>
        public const string AddedToTenant = "User.AddedToTenant";

        /// <summary>User was removed from tenant.</summary>
        public const string RemovedFromTenant = "User.RemovedFromTenant";

        /// <summary>User's tenant role was updated.</summary>
        public const string TenantRoleUpdated = "User.TenantRoleUpdated";
    }

    /// <summary>
    /// Tenant audit actions.
    /// </summary>
    public static class Tenants
    {
        /// <summary>Tenant was created.</summary>
        public const string Created = "Tenant.Created";

        /// <summary>Tenant was updated.</summary>
        public const string Updated = "Tenant.Updated";

        /// <summary>Tenant was deleted.</summary>
        public const string Deleted = "Tenant.Deleted";

        /// <summary>User was added to tenant.</summary>
        public const string UserAdded = "Tenant.UserAdded";

        /// <summary>User was removed from tenant.</summary>
        public const string UserRemoved = "Tenant.UserRemoved";

        /// <summary>User's role in tenant was updated.</summary>
        public const string UserRoleUpdated = "Tenant.UserRoleUpdated";
    }

    /// <summary>
    /// Pipeline audit actions.
    /// </summary>
    public static class Pipelines
    {
        /// <summary>Pipeline was created.</summary>
        public const string Created = "Pipeline.Created";

        /// <summary>Pipeline was updated.</summary>
        public const string Updated = "Pipeline.Updated";

        /// <summary>Pipeline was deleted.</summary>
        public const string Deleted = "Pipeline.Deleted";

        /// <summary>Pipeline was executed.</summary>
        public const string Executed = "Pipeline.Executed";

        /// <summary>Pipeline execution failed.</summary>
        public const string ExecutionFailed = "Pipeline.ExecutionFailed";

        /// <summary>Pipeline was activated.</summary>
        public const string Activated = "Pipeline.Activated";

        /// <summary>Pipeline was deactivated.</summary>
        public const string Deactivated = "Pipeline.Deactivated";
    }

    /// <summary>
    /// Connector audit actions.
    /// </summary>
    public static class Connectors
    {
        /// <summary>Connector was created.</summary>
        public const string Created = "Connector.Created";

        /// <summary>Connector was updated.</summary>
        public const string Updated = "Connector.Updated";

        /// <summary>Connector was deleted.</summary>
        public const string Deleted = "Connector.Deleted";

        /// <summary>Connector was tested.</summary>
        public const string Tested = "Connector.Tested";

        /// <summary>Connector test failed.</summary>
        public const string TestFailed = "Connector.TestFailed";

        /// <summary>Connector schema was detected.</summary>
        public const string SchemaDetected = "Connector.SchemaDetected";
    }

    // Backward compatibility constants

    /// <summary>Connector was created (backward compatibility).</summary>
    public const string ConnectorCreated = "Connector.Created";

    /// <summary>Connector was updated (backward compatibility).</summary>
    public const string ConnectorUpdated = "Connector.Updated";

    /// <summary>Connector was deleted (backward compatibility).</summary>
    public const string ConnectorDeleted = "Connector.Deleted";

    /// <summary>Connector was tested (backward compatibility).</summary>
    public const string ConnectorTested = "Connector.Tested";

    /// <summary>Connector schema was detected (backward compatibility).</summary>
    public const string ConnectorSchemaDetected = "Connector.SchemaDetected";

    // Backward compatibility constants for transformations (deprecated - transformations are now embedded in field mappings)

    /// <summary>Transformation was created (deprecated).</summary>
    [Obsolete("Transformation entity has been removed. Transformations are now embedded in Pipeline.FieldMappingsJson.")]
    public const string TransformationCreated = "Transformation.Created";

    /// <summary>Transformation was updated (deprecated).</summary>
    [Obsolete("Transformation entity has been removed. Transformations are now embedded in Pipeline.FieldMappingsJson.")]
    public const string TransformationUpdated = "Transformation.Updated";

    /// <summary>Transformation was deleted (deprecated).</summary>
    [Obsolete("Transformation entity has been removed. Transformations are now embedded in Pipeline.FieldMappingsJson.")]
    public const string TransformationDeleted = "Transformation.Deleted";

    /// <summary>
    /// Schedule audit actions.
    /// </summary>
    public static class Schedules
    {
        /// <summary>Schedule was created.</summary>
        public const string Created = "Schedule.Created";

        /// <summary>Schedule was updated.</summary>
        public const string Updated = "Schedule.Updated";

        /// <summary>Schedule was deleted.</summary>
        public const string Deleted = "Schedule.Deleted";

        /// <summary>Schedule was enabled.</summary>
        public const string Enabled = "Schedule.Enabled";

        /// <summary>Schedule was disabled.</summary>
        public const string Disabled = "Schedule.Disabled";

        /// <summary>Schedule was triggered manually.</summary>
        public const string TriggeredManually = "Schedule.TriggeredManually";

        /// <summary>Schedule was paused because pipeline was deactivated.</summary>
        public const string PausedForPipeline = "Schedule.PausedForPipeline";

        /// <summary>Schedule was resumed because pipeline was activated.</summary>
        public const string ResumedForPipeline = "Schedule.ResumedForPipeline";
    }
}
