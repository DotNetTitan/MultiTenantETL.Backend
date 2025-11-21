using MultiTenantETL.Domain.Entities;

namespace MultiTenantETL.Infrastructure.Identity
{
    public class UserTenant
    {
        public Guid UserId { get; set; }
        public Guid TenantId { get; set; }
        public required string RoleCode { get; set; }
        public bool IsActive { get; set; }

        // Navigation properties
        public virtual ApplicationUser User { get; set; }
        public virtual Tenant Tenant { get; set; }
    }
}