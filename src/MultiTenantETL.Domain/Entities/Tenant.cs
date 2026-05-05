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
        /// Current status of the tenant.
        /// </summary>
        public MultiTenantETL.Domain.Enums.TenantStatus Status { get; set; } = MultiTenantETL.Domain.Enums.TenantStatus.Active;

        /// <summary>
        /// When the tenant was deleted (if applicable).
        /// </summary>
        public DateTime? DeletedAt { get; set; }

        /// <summary>
        /// Who deleted the tenant (if applicable).
        /// </summary>
        public Guid? DeletedBy { get; set; }

        /// <summary>
        /// When the tenant was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }
}