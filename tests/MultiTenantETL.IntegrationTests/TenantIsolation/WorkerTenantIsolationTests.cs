using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Domain.Enums;
using MultiTenantETL.Infrastructure.Identity;
using MultiTenantETL.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace MultiTenantETL.IntegrationTests.TenantIsolation;

/// <summary>
/// Integration tests verifying tenant isolation works correctly in worker/background job scenarios
/// using a real PostgreSQL database via Testcontainers
/// </summary>
public class WorkerTenantIsolationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres;
    private ServiceProvider? _serviceProvider;

    public WorkerTenantIsolationTests()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("tenant_isolation_test")
            .WithUsername("test")
            .WithPassword("test")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var services = new ServiceCollection();

        // Configure PostgreSQL database
        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.UseNpgsql(_postgres.GetConnectionString());
        });

        // Register tenant provider as scoped (critical for isolation)
        services.AddScoped<ITenantProvider, TenantProvider>();

        _serviceProvider = services.BuildServiceProvider();

        // Run migrations
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_serviceProvider != null)
        {
            await _serviceProvider.DisposeAsync();
        }
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task DbContext_WithTenantProvider_FiltersQueriesByTenant()
    {
        // Arrange - Create test data for two tenants
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();

        using (var setupScope = _serviceProvider!.CreateScope())
        {
            var setupContext = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var setupTenantProvider = setupScope.ServiceProvider.GetRequiredService<ITenantProvider>();

            var tenant1 = new Tenant
            {
                Id = tenant1Id,
                Name = "Tenant 1",
                Slug = "tenant1",
                Status = TenantStatus.Active,
                CreatedAt = DateTime.UtcNow
            };

            var tenant2 = new Tenant
            {
                Id = tenant2Id,
                Name = "Tenant 2",
                Slug = "tenant2",
                Status = TenantStatus.Active,
                CreatedAt = DateTime.UtcNow
            };

            setupContext.Tenants.AddRange(tenant1, tenant2);
            await setupContext.SaveChangesAsync();

            // Set tenant context for connector1
            setupTenantProvider.TenantId = tenant1Id;
            var connector1 = new Connector
            {
                Id = Guid.NewGuid(),
                TenantId = tenant1Id,
                Name = "Tenant 1 Connector",
                Type = "Database",
                Provider = "PostgreSQL",
                Direction = "Source",
                IsSource = true,
                IsDestination = false,
                RequiresCredentials = true,
                IsActive = true,
                ConfigJson = "{}",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };
            setupContext.Connectors.Add(connector1);
            await setupContext.SaveChangesAsync();

            // Switch tenant context for connector2
            setupTenantProvider.TenantId = tenant2Id;
            var connector2 = new Connector
            {
                Id = Guid.NewGuid(),
                TenantId = tenant2Id,
                Name = "Tenant 2 Connector",
                Type = "Database",
                Provider = "PostgreSQL",
                Direction = "Source",
                IsSource = true,
                IsDestination = false,
                RequiresCredentials = true,
                IsActive = true,
                ConfigJson = "{}",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };
            setupContext.Connectors.Add(connector2);
            await setupContext.SaveChangesAsync();
        }

        // Act - Create a new scope with tenant context set
        using var scope = _serviceProvider!.CreateScope();
        var tenantProvider = scope.ServiceProvider.GetRequiredService<ITenantProvider>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Set tenant context to tenant1
        tenantProvider.TenantId = tenant1Id;

        // Query connectors
        var connectors = await dbContext.Connectors.ToListAsync();

        // Assert - Should only see tenant1's connector
        connectors.Should().HaveCount(1);
        connectors[0].Name.Should().Be("Tenant 1 Connector");
        connectors[0].TenantId.Should().Be(tenant1Id);
    }

    [Fact]
    public async Task ConcurrentScopes_WithDifferentTenants_DoNotInterfere()
    {
        // Arrange - Create test data
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();

        using (var setupScope = _serviceProvider!.CreateScope())
        {
            var setupContext = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var setupTenantProvider = setupScope.ServiceProvider.GetRequiredService<ITenantProvider>();

            var tenant1 = new Tenant
            {
                Id = tenant1Id,
                Name = "Tenant A",
                Slug = "tenant-a",
                Status = TenantStatus.Active,
                CreatedAt = DateTime.UtcNow
            };

            var tenant2 = new Tenant
            {
                Id = tenant2Id,
                Name = "Tenant B",
                Slug = "tenant-b",
                Status = TenantStatus.Active,
                CreatedAt = DateTime.UtcNow
            };

            setupContext.Tenants.AddRange(tenant1, tenant2);
            await setupContext.SaveChangesAsync();

            // Set tenant context for connector1
            setupTenantProvider.TenantId = tenant1Id;
            var connector1 = new Connector
            {
                Id = Guid.NewGuid(),
                TenantId = tenant1Id,
                Name = "Connector A",
                Type = "Database",
                Provider = "PostgreSQL",
                Direction = "Source",
                IsSource = true,
                IsDestination = false,
                RequiresCredentials = true,
                IsActive = true,
                ConfigJson = "{}",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };
            setupContext.Connectors.Add(connector1);
            await setupContext.SaveChangesAsync();

            // Switch tenant context for connector2
            setupTenantProvider.TenantId = tenant2Id;
            var connector2 = new Connector
            {
                Id = Guid.NewGuid(),
                TenantId = tenant2Id,
                Name = "Connector B",
                Type = "Database",
                Provider = "PostgreSQL",
                Direction = "Source",
                IsSource = true,
                IsDestination = false,
                RequiresCredentials = true,
                IsActive = true,
                ConfigJson = "{}",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };
            setupContext.Connectors.Add(connector2);
            await setupContext.SaveChangesAsync();
        }

        // Act - Simulate concurrent worker jobs
        var tasks = new List<Task<string>>();

        // Job 1 - Tenant A
        tasks.Add(Task.Run(async () =>
        {
            using var scope = _serviceProvider!.CreateScope();
            var tenantProvider = scope.ServiceProvider.GetRequiredService<ITenantProvider>();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            tenantProvider.TenantId = tenant1Id;
            await Task.Delay(50); // Simulate work

            var connectors = await dbContext.Connectors.ToListAsync();
            return connectors.Single().Name;
        }));

        // Job 2 - Tenant B
        tasks.Add(Task.Run(async () =>
        {
            using var scope = _serviceProvider!.CreateScope();
            var tenantProvider = scope.ServiceProvider.GetRequiredService<ITenantProvider>();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            tenantProvider.TenantId = tenant2Id;
            await Task.Delay(50); // Simulate work

            var connectors = await dbContext.Connectors.ToListAsync();
            return connectors.Single().Name;
        }));

        var results = await Task.WhenAll(tasks);

        // Assert - Each job should only see its own tenant's data
        results.Should().Contain("Connector A");
        results.Should().Contain("Connector B");
        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task TenantProvider_NotSet_ReturnsNoData()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        using (var setupScope = _serviceProvider!.CreateScope())
        {
            var setupContext = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var setupTenantProvider = setupScope.ServiceProvider.GetRequiredService<ITenantProvider>();

            var tenant = new Tenant
            {
                Id = tenantId,
                Name = "Test Tenant",
                Slug = "test",
                Status = TenantStatus.Active,
                CreatedAt = DateTime.UtcNow
            };

            setupContext.Tenants.Add(tenant);
            await setupContext.SaveChangesAsync();

            // Set tenant context to insert connector
            setupTenantProvider.TenantId = tenantId;
            var connector = new Connector
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Test Connector",
                Type = "Database",
                Provider = "PostgreSQL",
                Direction = "Source",
                IsSource = true,
                IsDestination = false,
                RequiresCredentials = true,
                IsActive = true,
                ConfigJson = "{}",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };

            setupContext.Connectors.Add(connector);
            await setupContext.SaveChangesAsync();
        }

        // Act - Query without setting tenant context
        using var scope = _serviceProvider!.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // Note: TenantProvider.TenantId is null by default

        var connectors = await dbContext.Connectors.ToListAsync();

        // Assert - Should return no data (null != tenantId)
        connectors.Should().BeEmpty();
    }
}
