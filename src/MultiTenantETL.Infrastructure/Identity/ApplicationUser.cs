using Microsoft.AspNetCore.Identity;
using MultiTenantETL.Domain.Entities;

namespace MultiTenantETL.Infrastructure.Identity
{
    public class ApplicationUser : IdentityUser<Guid>
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public Guid? CurrentTenantId { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }

        // Navigation properties
        public virtual ICollection<UserTenant> UserTenants { get; set; } = new List<UserTenant>();
        public virtual Tenant? CurrentTenant { get; set; }
    }
}