using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Domain.Interfaces;
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
        private readonly ITenantProvider _tenantProvider;

        // Domain entities
        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<UserTenant> UserTenants { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Connector> Connectors { get; set; }
        public DbSet<Transformation> Transformations { get; set; }
        public DbSet<Pipeline> Pipelines { get; set; }
        public DbSet<PipelineSchedule> PipelineSchedules { get; set; }
        public DbSet<PipelineExecution> PipelineExecutions { get; set; }
        public DbSet<ExecutionLogEntry> ExecutionLogs { get; set; }
        public DbSet<ExecutionBatch> ExecutionBatches { get; set; }

        // OpenIddict entities
        public DbSet<OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreApplication> OpenIddictApplications { get; set; }
        public DbSet<OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreAuthorization> OpenIddictAuthorizations { get; set; }
        public DbSet<OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreScope> OpenIddictScopes { get; set; }
        public DbSet<OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreToken> OpenIddictTokens { get; set; }

        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options,
            ITenantProvider tenantProvider)
            : base(options)
        {
            _tenantProvider = tenantProvider;
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Apply all configurations from the assembly
            builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

            // Apply global query filters for tenant isolation
            ApplyTenantQueryFilters(builder);
        }

        /// <summary>
        /// Applies global query filters to all entities implementing ITenantResource.
        /// This ensures tenant isolation at the database level for both HTTP and worker contexts.
        /// </summary>
        private void ApplyTenantQueryFilters(ModelBuilder modelBuilder)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(ITenantResource).IsAssignableFrom(entityType.ClrType))
                {
                    var method = typeof(ApplicationDbContext)
                        .GetMethod(nameof(SetTenantQueryFilter), BindingFlags.NonPublic | BindingFlags.Instance)!
                        .MakeGenericMethod(entityType.ClrType);
                    
                    method.Invoke(this, new object[] { modelBuilder });
                }
            }
        }

        /// <summary>
        /// Sets the tenant query filter for a specific entity type.
        /// The filter uses the scoped ITenantProvider, which works in both HTTP and worker contexts.
        /// </summary>
        private void SetTenantQueryFilter<TEntity>(ModelBuilder modelBuilder)
            where TEntity : class, ITenantResource
        {
            modelBuilder.Entity<TEntity>().HasQueryFilter(e => 
                e.TenantId == _tenantProvider.TenantId);
        }
    }
}