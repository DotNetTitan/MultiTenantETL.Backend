using Microsoft.AspNetCore.Identity;

namespace MultiTenantETL.Infrastructure.Identity
{
    public class ApplicationRole : IdentityRole<Guid>
    {
        public string Description { get; set; } = string.Empty;
        public List<string> Permissions { get; set; } = new List<string>();
    }
}