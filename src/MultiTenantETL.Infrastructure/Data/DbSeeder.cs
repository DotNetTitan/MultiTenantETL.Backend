using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Identity;
using MultiTenantETL.Infrastructure.Persistence;
using OpenIddict.Abstractions;

namespace MultiTenantETL.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        await SeedRolesAsync(services);
        await SeedDefaultTenantAsync(services);
        await SeedAdminUserAsync(services);
        await SeedOAuthClientsAsync(services);
        
        Console.WriteLine("✅ Database seeding completed");
    }

    private static async Task SeedRolesAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();

        var roles = new[]
        {
            new ApplicationRole
            {
                Name = "SuperAdmin",
                Description = "System administrator with full access to all tenants and system configuration",
                Permissions = new List<string>
                {
                    "system.manage",
                    "tenants.create",
                    "tenants.delete",
                    "tenants.manage",
                    "users.manage",
                    "roles.manage"
                }
            },
            new ApplicationRole
            {
                Name = "TenantAdmin",
                Description = "Tenant administrator with full access within their tenant",
                Permissions = new List<string>
                {
                    "tenant.users.manage",
                    "tenant.settings.manage",
                    "tenant.data.manage",
                    "etl.manage"
                }
            },
            new ApplicationRole
            {
                Name = "User",
                Description = "Standard user with read and basic write access",
                Permissions = new List<string>
                {
                    "tenant.data.read",
                    "tenant.data.write",
                    "etl.view",
                    "etl.execute"
                }
            },
            new ApplicationRole
            {
                Name = "Viewer",
                Description = "Read-only access to tenant data",
                Permissions = new List<string>
                {
                    "tenant.data.read",
                    "etl.view"
                }
            }
        };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role.Name!))
            {
                await roleManager.CreateAsync(role);
                Console.WriteLine($"✅ Created role: {role.Name}");
            }
        }
    }

    private static async Task SeedDefaultTenantAsync(IServiceProvider services)
    {
        var dbContext = services.GetRequiredService<ApplicationDbContext>();

        if (!await dbContext.Tenants.AnyAsync())
        {
            var defaultTenant = new Tenant
            {
                Id = Guid.NewGuid(),
                Name = "Default Organization",
                Slug = "default",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            dbContext.Tenants.Add(defaultTenant);
            await dbContext.SaveChangesAsync();
            Console.WriteLine($"✅ Created default tenant: {defaultTenant.Name}");
        }
    }

    private static async Task SeedAdminUserAsync(IServiceProvider services)
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        var configuration = services.GetRequiredService<IConfiguration>();

        const string adminEmail = "admin@multitenant-etl.com";
        var adminPassword = configuration["Seeding:AdminPassword"] ?? "Admin@123456";

        var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
        if (existingAdmin == null)
        {
            var defaultTenant = await dbContext.Tenants.FirstOrDefaultAsync();

            var adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FirstName = "System",
                LastName = "Administrator",
                CurrentTenantId = defaultTenant?.Id,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            var result = await userManager.CreateAsync(adminUser, adminPassword);
            
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "SuperAdmin");

                if (defaultTenant != null)
                {
                    var userTenant = new UserTenant
                    {
                        UserId = adminUser.Id,
                        TenantId = defaultTenant.Id,
                        RoleCode = "SuperAdmin",
                        IsActive = true
                    };

                    dbContext.UserTenants.Add(userTenant);
                    await dbContext.SaveChangesAsync();
                }

                Console.WriteLine($"✅ Created admin user: {adminEmail}");
                Console.WriteLine($"   Password: {adminPassword}");
                Console.WriteLine($"   ⚠️  Please change this password in production!");
            }
            else
            {
                Console.WriteLine($"❌ Failed to create admin user:");
                foreach (var error in result.Errors)
                {
                    Console.WriteLine($"   - {error.Description}");
                }
            }
        }
    }

    private static async Task SeedOAuthClientsAsync(IServiceProvider services)
    {
        var applicationManager = services.GetRequiredService<IOpenIddictApplicationManager>();
        var configuration = services.GetRequiredService<IConfiguration>();
        
        var oauthClientSecret = configuration["Seeding:OAuthClientSecret"] ?? "postman-secret-key-change-in-production";

        if (await applicationManager.FindByClientIdAsync("multitenant-etl-spa") == null)
        {
            await applicationManager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = "multitenant-etl-spa",
                DisplayName = "MultiTenant ETL SPA",
                ClientType = OpenIddictConstants.ClientTypes.Public,
                ConsentType = OpenIddictConstants.ConsentTypes.Implicit,

                Permissions =
                {
                    OpenIddictConstants.Permissions.Endpoints.Authorization,
                    OpenIddictConstants.Permissions.Endpoints.Token,
                    OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                    OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                    OpenIddictConstants.Permissions.ResponseTypes.Code,
                    OpenIddictConstants.Permissions.Scopes.Email,
                    OpenIddictConstants.Permissions.Scopes.Profile,
                    OpenIddictConstants.Permissions.Scopes.Roles,
                    OpenIddictConstants.Permissions.Prefixes.Scope + "api"
                },

                RedirectUris =
                {
                    new Uri("http://localhost:5173/auth/callback"),
                    new Uri("https://app.example.com/auth/callback")
                },
                PostLogoutRedirectUris =
                {
                    new Uri("http://localhost:5173/"),
                    new Uri("https://app.example.com/")
                }
            });

            Console.WriteLine("✅ Created OAuth client: multitenant-etl-spa");
        }

        if (await applicationManager.FindByClientIdAsync("multitenant-etl-postman") == null)
        {
            await applicationManager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = "multitenant-etl-postman",
                ClientSecret = oauthClientSecret,
                DisplayName = "MultiTenant ETL API Testing Client",
                ClientType = OpenIddictConstants.ClientTypes.Confidential,
                ConsentType = OpenIddictConstants.ConsentTypes.Implicit,

                Permissions =
                {
                    OpenIddictConstants.Permissions.Endpoints.Token,
                    OpenIddictConstants.Permissions.GrantTypes.Password,
                    OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                    OpenIddictConstants.Permissions.Scopes.Email,
                    OpenIddictConstants.Permissions.Scopes.Profile,
                    OpenIddictConstants.Permissions.Scopes.Roles,
                    OpenIddictConstants.Permissions.Prefixes.Scope + "api",
                    OpenIddictConstants.Permissions.Prefixes.Scope + "offline_access"
                }
            });

            Console.WriteLine("✅ Created OAuth client: multitenant-etl-postman");
            Console.WriteLine("   Client Secret: postman-secret-key-change-in-production");
        }
    }
}
