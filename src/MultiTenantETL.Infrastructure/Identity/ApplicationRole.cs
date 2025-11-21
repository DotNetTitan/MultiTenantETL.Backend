using Microsoft.AspNetCore.Identity;

namespace MultiTenantETL.Infrastructure.Identity
{
    public class ApplicationRole : IdentityRole<Guid>
    {
        public string Description { get; set; }
        public List<string> Permissions { get; set; }
    }
}