using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Identity;

namespace MultiTenantETL.Infrastructure.Persistence
{
    public class ApplicationDbContext : IdentityDbContext<
        ApplicationUser,
        ApplicationRole,
        Guid,
        IdentityUserClaim<Guid>,
        IdentityUserRole<Guid>,
        IdentityUserLogin<Guid>,
        IdentityRoleClaim<Guid>,
        IdentityUserToken<Guid>>
    {
        // Domain entities
        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<UserTenant> UserTenants { get; set; }

        // OpenIddict entities
        public DbSet<OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreApplication> OpenIddictApplications { get; set; }
        public DbSet<OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreAuthorization> OpenIddictAuthorizations { get; set; }
        public DbSet<OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreScope> OpenIddictScopes { get; set; }
        public DbSet<OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreToken> OpenIddictTokens { get; set; }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Customize Identity table names
            builder.Entity<ApplicationUser>().ToTable("users");
            builder.Entity<ApplicationRole>().ToTable("roles");
            builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
            builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
            builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
            builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
            builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");

            // User-Tenant relationship (many-to-many)
            builder.Entity<UserTenant>()
                .HasKey(ut => new { ut.UserId, ut.TenantId });

            builder.Entity<UserTenant>()
                .HasOne(ut => ut.User)
                .WithMany(u => u.UserTenants)
                .HasForeignKey(ut => ut.UserId);

            builder.Entity<UserTenant>()
                .HasOne(ut => ut.Tenant)
                .WithMany()
                .HasForeignKey(ut => ut.TenantId);
        }
    }
}