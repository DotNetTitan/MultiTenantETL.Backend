namespace MultiTenantETL.Domain.Entities
{
    /// <summary>
    /// Represents a tenant organization in the multi-tenant system.
    /// </summary>
    public class Tenant
    {
        /// <summary>
        /// Unique identifier for the tenant.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Display name of the tenant.
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// URL-friendly slug for the tenant (used in routes).
        /// </summary>
        public required string Slug { get; set; }

        /// <summary>
        /// Indicates whether the tenant is active.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// When the tenant was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }
}