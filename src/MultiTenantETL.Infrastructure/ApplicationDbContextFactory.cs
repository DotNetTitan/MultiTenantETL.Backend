using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Infrastructure.Identity;
using MultiTenantETL.Infrastructure.Persistence;

namespace MultiTenantETL.Infrastructure;

/// <summary>
/// Design-time DbContext factory for EF Core migrations.
/// This factory is used when running migrations from the command line.
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .AddUserSecrets<ApplicationDbContextFactory>(optional: true)
            .Build();

        // Build DbContext options
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        optionsBuilder.UseNpgsql(connectionString);

        // Create a dummy tenant provider for design-time (filters won't apply)
        var tenantProvider = new DesignTimeTenantProvider();

        return new ApplicationDbContext(optionsBuilder.Options, tenantProvider);
    }

    /// <summary>
    /// Dummy tenant provider for design-time operations.
    /// Sets TenantId to null so filters don't apply during migrations.
    /// </summary>
    private class DesignTimeTenantProvider : ITenantProvider
    {
        public Guid? TenantId { get; set; } = null; // No tenant filtering in design-time
        public string? CorrelationId { get; set; } = null;
    }
}