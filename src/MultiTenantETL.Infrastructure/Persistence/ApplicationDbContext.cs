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
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Connector> Connectors { get; set; }
        public DbSet<Transformation> Transformations { get; set; }
        public DbSet<Pipeline> Pipelines { get; set; }

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

            // Audit logs configuration
            builder.Entity<AuditLog>()
                .ToTable("audit_logs");

            builder.Entity<AuditLog>()
                .HasOne(a => a.Tenant)
                .WithMany()
                .HasForeignKey(a => a.TenantId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<AuditLog>()
                .HasIndex(a => a.TenantId);

            builder.Entity<AuditLog>()
                .HasIndex(a => a.UserId);

            builder.Entity<AuditLog>()
                .HasIndex(a => a.Action);

            builder.Entity<AuditLog>()
                .HasIndex(a => a.CreatedAt);

            // Connectors configuration
            builder.Entity<Connector>()
                .ToTable("connectors");

            builder.Entity<Connector>()
                .HasOne(c => c.Tenant)
                .WithMany()
                .HasForeignKey(c => c.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Connector>()
                .HasIndex(c => c.TenantId);

            builder.Entity<Connector>()
                .HasIndex(c => c.Type);

            builder.Entity<Connector>()
                .HasIndex(c => c.Provider);

            builder.Entity<Connector>()
                .HasIndex(c => new { c.TenantId, c.Name });

            // Configure JSON columns for PostgreSQL JSONB
            builder.Entity<Connector>()
                .Property(c => c.ConfigJson)
                .HasColumnType("jsonb");

            builder.Entity<Connector>()
                .Property(c => c.SchemaJson)
                .HasColumnType("jsonb");

            // Transformations configuration
            builder.Entity<Transformation>()
                .ToTable("transformations");

            builder.Entity<Transformation>()
                .HasOne(t => t.Tenant)
                .WithMany()
                .HasForeignKey(t => t.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Transformation>()
                .HasIndex(t => t.TenantId);

            builder.Entity<Transformation>()
                .HasIndex(t => t.Type);

            builder.Entity<Transformation>()
                .HasIndex(t => new { t.TenantId, t.Name });

            builder.Entity<Transformation>()
                .Property(t => t.ConfigJson)
                .HasColumnType("jsonb");

            // Pipelines configuration
            builder.Entity<Pipeline>()
                .ToTable("pipelines");

            builder.Entity<Pipeline>()
                .HasOne(p => p.Tenant)
                .WithMany()
                .HasForeignKey(p => p.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Pipeline>()
                .HasOne(p => p.SourceConnector)
                .WithMany()
                .HasForeignKey(p => p.SourceConnectorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Pipeline>()
                .HasOne(p => p.DestinationConnector)
                .WithMany()
                .HasForeignKey(p => p.DestinationConnectorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Pipeline>()
                .HasIndex(p => p.TenantId);

            builder.Entity<Pipeline>()
                .HasIndex(p => p.Status);

            builder.Entity<Pipeline>()
                .HasIndex(p => new { p.TenantId, p.Name });

            builder.Entity<Pipeline>()
                .Property(p => p.FieldMappingsJson)
                .HasColumnType("jsonb");

            builder.Entity<Pipeline>()
                .Property(p => p.ScheduleJson)
                .HasColumnType("jsonb");
        }
    }
}