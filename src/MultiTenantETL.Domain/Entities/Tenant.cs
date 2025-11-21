using MultiTenantETL.Domain.Interfaces;

namespace MultiTenantETL.Domain.Entities
{
    public class Tenant : ITenantResource
    {
        public Guid Id { get; set; }
        public required string Name { get; set; }
        public required string Slug { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        // Interface implementation
        public Guid TenantId => Id;
    }
}