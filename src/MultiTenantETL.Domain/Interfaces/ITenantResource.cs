namespace MultiTenantETL.Domain.Interfaces
{
    /// <summary>
    /// Interface for entities that belong to a tenant.
    /// </summary>
    public interface ITenantResource
    {
        /// <summary>
        /// The tenant identifier that owns this resource.
        /// </summary>
        Guid TenantId { get; }
    }
}