using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Identity;
using MultiTenantETL.Infrastructure.Persistence;
using OpenIddict.Abstractions;
using Permissions = MultiTenantETL.Domain.Constants.Permissions;
using Roles = MultiTenantETL.Domain.Constants.Roles;

namespace MultiTenantETL.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var logger = services.GetRequiredService<ILogger<ApplicationDbContext>>();

        await SeedRolesAsync(services, logger);
        await SeedDefaultTenantAsync(services, logger);
        await SeedAdminUserAsync(services, logger);
        await SeedGuestDemoAsync(services, logger);
        await SeedOAuthClientsAsync(services, logger);

        logger.LogInformation("Database seeding completed");
    }

    private static async Task SeedRolesAsync(IServiceProvider services, ILogger logger)
    {
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();

        var roles = new[]
        {
            new ApplicationRole
            {
                Name = Roles.SuperAdmin,
                Description = "System administrator with full access to all tenants and system configuration",
                Permissions = new List<string>
                {
                    "*.*", // Wildcard - grants all permissions
                    Permissions.System.Manage,
                    Permissions.Tenants.All,
                    Permissions.Users.All,
                    Permissions.Roles.All,
                    Permissions.Connectors.All,
                    Permissions.Pipelines.All,
                    Permissions.Executions.All,
                    Permissions.Dashboard.All
                }
            },
            new ApplicationRole
            {
                Name = Roles.TenantAdmin,
                Description = "Tenant administrator with full access within their tenant",
                Permissions = new List<string>
                {
                    Permissions.Users.Manage,
                    Permissions.TenantSettings.Manage,
                    Permissions.TenantData.Manage,
                    Permissions.Connectors.All,
                    Permissions.Pipelines.All,
                    Permissions.Executions.All,
                    Permissions.Dashboard.Read,
                    Permissions.ETL.Manage
                }
            },
            new ApplicationRole
            {
                Name = Roles.User,
                Description = "Standard user with read and basic write access",
                Permissions = new List<string>
                {
                    Permissions.TenantData.Read,
                    Permissions.TenantData.Write,
                    Permissions.Connectors.Read,
                    Permissions.Connectors.Test,
                    Permissions.Pipelines.Read,
                    Permissions.Pipelines.Execute,
                    Permissions.Executions.View,
                    Permissions.Dashboard.Read,
                    Permissions.ETL.View,
                    Permissions.ETL.Execute
                }
            },
            new ApplicationRole
            {
                Name = Roles.Viewer,
                Description = "Read-only access to tenant data",
                Permissions = new List<string>
                {
                    Permissions.TenantData.Read,
                    Permissions.Connectors.Read,
                    Permissions.Pipelines.Read,
                    Permissions.Executions.View,
                    Permissions.Dashboard.Read,
                    Permissions.ETL.View
                }
            }
        };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role.Name!))
            {
                await roleManager.CreateAsync(role);
                logger.LogInformation("Created role: {RoleName}", role.Name);
            }
        }
    }

    private static async Task SeedDefaultTenantAsync(IServiceProvider services, ILogger logger)
    {
        var dbContext = services.GetRequiredService<ApplicationDbContext>();

        if (!await dbContext.Tenants.IgnoreQueryFilters().AnyAsync())
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
            logger.LogInformation("Created default tenant: {TenantName}", defaultTenant.Name);
        }
    }

    private static async Task SeedAdminUserAsync(IServiceProvider services, ILogger logger)
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        var configuration = services.GetRequiredService<IConfiguration>();

        const string adminEmail = "admin@multitenant-etl.com";
        const string adminPassword = "Admin@123456";

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
                await userManager.AddToRoleAsync(adminUser, Roles.SuperAdmin);

                if (defaultTenant != null)
                {
                    var userTenant = new UserTenant
                    {
                        UserId = adminUser.Id,
                        TenantId = defaultTenant.Id,
                        RoleCode = Roles.SuperAdmin,
                        IsActive = true
                    };

                    dbContext.UserTenants.Add(userTenant);
                    await dbContext.SaveChangesAsync();
                }

                logger.LogInformation("Created admin user: {AdminEmail}", adminEmail);
                logger.LogWarning("Admin password is set to default. Please change this password in production!");
            }
            else
            {
                logger.LogError("Failed to create admin user. Errors: {Errors}", 
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    private static async Task SeedOAuthClientsAsync(IServiceProvider services, ILogger logger)
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
                    OpenIddictConstants.Permissions.GrantTypes.Password,
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

            logger.LogInformation("Created OAuth client: multitenant-etl-spa");
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

            logger.LogInformation("Created OAuth client: multitenant-etl-postman");
        }
    }

    private static async Task SeedGuestDemoAsync(IServiceProvider services, ILogger logger)
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = services.GetRequiredService<ApplicationDbContext>();

        const string guestEmail = "guest@multitenant-etl.com";
        const string guestPassword = "Guest@123456";

        var existingGuest = await userManager.FindByEmailAsync(guestEmail);
        if (existingGuest == null)
        {
            // 1. Create Guest Tenant
            var guestTenant = new Tenant
            {
                Id = Guid.NewGuid(),
                Name = "Demo Organization",
                Slug = "demo",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            dbContext.Tenants.Add(guestTenant);
            await dbContext.SaveChangesAsync();

            // 2. Create Guest User with Viewer role
            var guestUser = new ApplicationUser
            {
                UserName = guestEmail,
                Email = guestEmail,
                EmailConfirmed = true,
                FirstName = "Guest",
                LastName = "User",
                CurrentTenantId = guestTenant.Id,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            var result = await userManager.CreateAsync(guestUser, guestPassword);

            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(guestUser, Roles.Viewer);

                var userTenant = new UserTenant
                {
                    UserId = guestUser.Id,
                    TenantId = guestTenant.Id,
                    RoleCode = Roles.Viewer,
                    IsActive = true
                };

                dbContext.UserTenants.Add(userTenant);
                await dbContext.SaveChangesAsync();

                // 3. Seed Demo Data for Guest Tenant
                await SeedDemoConnectorsAsync(dbContext, guestTenant.Id, guestUser.Id, logger);
                await SeedDemoPipelinesAsync(dbContext, guestTenant.Id, guestUser.Id, logger);
                await SeedDemoExecutionsAsync(dbContext, guestTenant.Id, logger);

                logger.LogInformation("Created guest demo account and tenant");
            }
            else
            {
                logger.LogError("Failed to create guest user. Errors: {Errors}",
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    private static async Task SeedDemoConnectorsAsync(ApplicationDbContext dbContext, Guid tenantId, Guid userId, ILogger logger)
    {
        var connectors = new[]
        {
            new Connector
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "PostgreSQL Sales Database",
                Type = "Database",
                Provider = "PostgreSQL",
                Description = "Production sales database with customer orders, products, and invoices",
                Direction = "Source",
                IsSource = true,
                IsDestination = false,
                RequiresCredentials = true,
                IsActive = true,
                ConfigJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    host = "sales-db.example.com",
                    port = 5432,
                    database = "sales_prod",
                    username = "readonly_user",
                    schema = "public",
                    useSsl = false,
                    password = "demo-password-encrypted",
                    connectionString = (string?)null,
                    useCustomConnectionString = false
                }),
                SchemaJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    fields = new[]
                    {
                        new { id = "field-pg-1", name = "order_id", type = "int", order = 1, nullable = false, required = true, description = "Order ID", isPrimaryKey = true },
                        new { id = "field-pg-2", name = "customer_id", type = "int", order = 2, nullable = false, required = true, description = "Customer ID", isPrimaryKey = false },
                        new { id = "field-pg-3", name = "order_date", type = "date", order = 3, nullable = false, required = true, description = "Order Date", isPrimaryKey = false },
                        new { id = "field-pg-4", name = "total_amount", type = "decimal", order = 4, nullable = false, required = true, description = "Total Amount", isPrimaryKey = false },
                        new { id = "field-pg-5", name = "order_status", type = "varchar", order = 5, nullable = false, required = true, description = "Order Status", isPrimaryKey = false }
                    }
                }),
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                UpdatedAt = DateTime.UtcNow.AddDays(-5),
                CreatedBy = userId
            },
            new Connector
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "MySQL Analytics Warehouse",
                Type = "Database",
                Provider = "MySQL",
                Description = "Data warehouse for analytics and reporting",
                Direction = "Destination",
                IsSource = false,
                IsDestination = true,
                RequiresCredentials = true,
                IsActive = true,
                ConfigJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    host = "analytics-warehouse.example.com",
                    port = 3306,
                    database = "analytics_dw",
                    username = "etl_writer",
                    password = "demo-password-encrypted",
                    useSsl = false,
                    connectionString = (string?)null,
                    useCustomConnectionString = false,
                    writeConfig = new
                    {
                        tableName = "fact_sales",
                        operation = "UPSERT",
                        batchSize = 1000,
                        primaryKeys = new[] { "sales_id" }
                    }
                }),
                SchemaJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    fields = new[]
                    {
                        new { id = "field-mysql-1", name = "sales_id", type = "int", order = 1, nullable = false, required = true, description = "Sales ID", isPrimaryKey = true },
                        new { id = "field-mysql-2", name = "customer_key", type = "int", order = 2, nullable = false, required = true, description = "Customer Key", isPrimaryKey = false },
                        new { id = "field-mysql-3", name = "order_date", type = "date", order = 3, nullable = false, required = true, description = "Order Date", isPrimaryKey = false },
                        new { id = "field-mysql-4", name = "sales_amount", type = "decimal", order = 4, nullable = false, required = true, description = "Sales Amount", isPrimaryKey = false },
                        new { id = "field-mysql-5", name = "status_code", type = "varchar", order = 5, nullable = false, required = true, description = "Status Code", isPrimaryKey = false }
                    }
                }),
                CreatedAt = DateTime.UtcNow.AddDays(-28),
                UpdatedAt = DateTime.UtcNow.AddDays(-3),
                CreatedBy = userId
            },
            new Connector
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Customer CSV Export",
                Type = "File",
                Provider = "CSV",
                Description = "Daily customer export from legacy CRM system",
                Direction = "Source",
                IsSource = true,
                IsDestination = false,
                RequiresCredentials = false,
                IsActive = true,
                ConfigJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    path = "/data/exports/customers",
                    filename = "customers_*.csv",
                    delimiter = ",",
                    encoding = "UTF-8",
                    hasHeader = true,
                    storageType = "Local"
                }),
                SchemaJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    fields = new[]
                    {
                        new { id = "field-csv-1", name = "customer_id", type = "int", order = 1, nullable = false, required = true, description = "Customer ID", isPrimaryKey = true },
                        new { id = "field-csv-2", name = "name", type = "varchar", order = 2, nullable = false, required = true, description = "Customer Name", isPrimaryKey = false },
                        new { id = "field-csv-3", name = "email", type = "varchar", order = 3, nullable = false, required = true, description = "Email", isPrimaryKey = false },
                        new { id = "field-csv-4", name = "phone", type = "varchar", order = 4, nullable = true, required = false, description = "Phone", isPrimaryKey = false },
                        new { id = "field-csv-5", name = "country", type = "varchar", order = 5, nullable = false, required = true, description = "Country", isPrimaryKey = false }
                    }
                }),
                CreatedAt = DateTime.UtcNow.AddDays(-25),
                UpdatedAt = DateTime.UtcNow.AddDays(-2),
                CreatedBy = userId
            },
            new Connector
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Salesforce REST API",
                Type = "API",
                Provider = "REST",
                Description = "Salesforce CRM API for accounts and opportunities",
                Direction = "Source",
                IsSource = true,
                IsDestination = false,
                RequiresCredentials = true,
                IsActive = true,
                ConfigJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    baseUrl = "https://api.salesforce.com",
                    authType = "OAuth2",
                    username = "demo@salesforce.com",
                    password = "demo-password",
                    authToken = (string?)null,
                    apiKeyHeader = (string?)null,
                    apiKeyValue = (string?)null,
                    headers = new { },
                    queryParameters = new { },
                    timeoutSeconds = 30,
                    useDynamicToken = false,
                    tokenEndpointUrl = (string?)null,
                    tokenEndpointMethod = "POST",
                    tokenEndpointHeadersText = (string?)null,
                    tokenEndpointBody = (string?)null,
                    tokenResponsePath = "access_token",
                    tokenExpirySeconds = (int?)null,
                    endpoints = new[]
                    {
                        new
                        {
                            id = "endpoint-sf-1",
                            path = "/services/data/v52.0/query",
                            method = "GET",
                            description = "SOQL Query",
                            requestSchema = "",
                            responseSchema = "{\"records\": [{\"Id\": \"\", \"Name\": \"\", \"Amount\": 0}]}",
                            requestDataPath = "",
                            responseDataPath = "records",
                            errorPath = "error.message"
                        }
                    },
                    writeConfig = new
                    {
                        requestFormat = "JSON",
                        batchSize = 100,
                        rootKey = (string?)null,
                        wrapInArray = false
                    }
                }),
                SchemaJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    fields = new[]
                    {
                        new { id = "field-sf-1", name = "Id", type = "varchar", order = 1, nullable = false, required = true, description = "Opportunity ID", isPrimaryKey = true },
                        new { id = "field-sf-2", name = "Name", type = "varchar", order = 2, nullable = false, required = true, description = "Opportunity Name", isPrimaryKey = false },
                        new { id = "field-sf-3", name = "Amount", type = "decimal", order = 3, nullable = true, required = false, description = "Amount", isPrimaryKey = false },
                        new { id = "field-sf-4", name = "CloseDate", type = "date", order = 4, nullable = false, required = true, description = "Close Date", isPrimaryKey = false },
                        new { id = "field-sf-5", name = "StageName", type = "varchar", order = 5, nullable = false, required = true, description = "Stage", isPrimaryKey = false }
                    }
                }),
                CreatedAt = DateTime.UtcNow.AddDays(-20),
                UpdatedAt = DateTime.UtcNow.AddDays(-1),
                CreatedBy = userId
            },
            new Connector
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "MongoDB User Events",
                Type = "Database",
                Provider = "MongoDB",
                Description = "User activity and event tracking database",
                Direction = "Source",
                IsSource = true,
                IsDestination = false,
                RequiresCredentials = true,
                IsActive = true,
                ConfigJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    host = "events-db.example.com",
                    port = 27017,
                    database = "user_events",
                    authDatabase = "admin",
                    username = "readonly",
                    password = "demo-password-encrypted",
                    connectionString = (string?)null,
                    useCustomConnectionString = false
                }),
                SchemaJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    fields = new[]
                    {
                        new { id = "field-mongo-1", name = "_id", type = "varchar", order = 1, nullable = false, required = true, description = "Document ID", isPrimaryKey = true },
                        new { id = "field-mongo-2", name = "user_id", type = "int", order = 2, nullable = false, required = true, description = "User ID", isPrimaryKey = false },
                        new { id = "field-mongo-3", name = "event_type", type = "varchar", order = 3, nullable = false, required = true, description = "Event Type", isPrimaryKey = false },
                        new { id = "field-mongo-4", name = "timestamp", type = "datetime", order = 4, nullable = false, required = true, description = "Timestamp", isPrimaryKey = false }
                    }
                }),
                CreatedAt = DateTime.UtcNow.AddDays(-22),
                UpdatedAt = DateTime.UtcNow.AddDays(-1),
                CreatedBy = userId
            }
        };

        dbContext.Connectors.AddRange(connectors);
        await dbContext.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} demo connectors", connectors.Length);
    }

    private static async Task SeedDemoPipelinesAsync(ApplicationDbContext dbContext, Guid tenantId, Guid userId, ILogger logger)
    {
        var connectors = await dbContext.Connectors.IgnoreQueryFilters().Where(c => c.TenantId == tenantId).ToListAsync();
        var sourcePostgres = connectors.First(c => c.Provider == "PostgreSQL");
        var destMysql = connectors.First(c => c.Provider == "MySQL");
        var sourceCsv = connectors.First(c => c.Provider == "CSV");
        var sourceSalesforce = connectors.First(c => c.Provider == "REST");

        var pipelines = new[]
        {
            new Pipeline
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Sales Orders to Analytics Warehouse",
                Description = "Daily synchronization of sales orders from production database to analytics warehouse",
                SourceConnectorId = sourcePostgres.Id,
                DestinationConnectorId = destMysql.Id,
                Status = "Active",
                IsActive = true,
                EmailNotificationsEnabled = true,
                NotificationEmailsJson = System.Text.Json.JsonSerializer.Serialize(new[] { "admin@demo.com" }),
                FieldMappingsJson = System.Text.Json.JsonSerializer.Serialize(new[]
                {
                    new { id = Guid.NewGuid().ToString(), order = 1, sourceFields = new[] { "order_id" }, transformations = new object[] { }, destinationField = "sales_id" },
                    new { id = Guid.NewGuid().ToString(), order = 2, sourceFields = new[] { "customer_id" }, transformations = new object[] { }, destinationField = "customer_key" },
                    new { id = Guid.NewGuid().ToString(), order = 3, sourceFields = new[] { "order_date" }, transformations = new object[] { }, destinationField = "order_date" },
                    new { id = Guid.NewGuid().ToString(), order = 4, sourceFields = new[] { "total_amount" }, transformations = new object[] { }, destinationField = "sales_amount" },
                    new { id = Guid.NewGuid().ToString(), order = 5, sourceFields = new[] { "order_status" }, transformations = new object[] { }, destinationField = "status_code" }
                }),
                LastRunAt = DateTime.UtcNow.AddHours(-8),
                LastRunStatus = "Completed",
                LastRunRecordsProcessed = 1247,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow.AddDays(-28),
                UpdatedAt = DateTime.UtcNow.AddDays(-2)
            },
            new Pipeline
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "CRM Customer Import",
                Description = "Import customer data from legacy CSV exports into analytics warehouse",
                SourceConnectorId = sourceCsv.Id,
                DestinationConnectorId = destMysql.Id,
                Status = "Active",
                IsActive = true,
                EmailNotificationsEnabled = true,
                NotificationEmailsJson = System.Text.Json.JsonSerializer.Serialize(new[] { "admin@demo.com" }),
                FieldMappingsJson = System.Text.Json.JsonSerializer.Serialize(new[]
                {
                    new { id = Guid.NewGuid().ToString(), order = 1, sourceFields = new[] { "customer_id" }, transformations = new object[] { }, destinationField = "customer_key" },
                    new { id = Guid.NewGuid().ToString(), order = 2, sourceFields = new[] { "name" }, transformations = new object[] { }, destinationField = "customer_name" },
                    new { id = Guid.NewGuid().ToString(), order = 3, sourceFields = new[] { "email" }, transformations = new object[] { }, destinationField = "email_address" },
                    new { id = Guid.NewGuid().ToString(), order = 4, sourceFields = new[] { "phone" }, transformations = new object[] { }, destinationField = "phone_number" },
                    new { id = Guid.NewGuid().ToString(), order = 5, sourceFields = new[] { "country" }, transformations = new object[] { }, destinationField = "country_code" }
                }),
                LastRunAt = DateTime.UtcNow.AddHours(-4),
                LastRunStatus = "Completed",
                LastRunRecordsProcessed = 892,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow.AddDays(-20),
                UpdatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new Pipeline
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Salesforce Opportunities Sync",
                Description = "Synchronize sales opportunities from Salesforce CRM to analytics database",
                SourceConnectorId = sourceSalesforce.Id,
                DestinationConnectorId = destMysql.Id,
                Status = "Active",
                IsActive = true,
                EmailNotificationsEnabled = true,
                NotificationEmailsJson = System.Text.Json.JsonSerializer.Serialize(new[] { "admin@demo.com" }),
                FieldMappingsJson = System.Text.Json.JsonSerializer.Serialize(new[]
                {
                    new { id = Guid.NewGuid().ToString(), order = 1, sourceFields = new[] { "Id" }, transformations = new object[] { }, destinationField = "opportunity_id" },
                    new { id = Guid.NewGuid().ToString(), order = 2, sourceFields = new[] { "Name" }, transformations = new object[] { }, destinationField = "opportunity_name" },
                    new { id = Guid.NewGuid().ToString(), order = 3, sourceFields = new[] { "Amount" }, transformations = new object[] { }, destinationField = "opportunity_value" },
                    new { id = Guid.NewGuid().ToString(), order = 4, sourceFields = new[] { "CloseDate" }, transformations = new object[] { }, destinationField = "expected_close_date" },
                    new { id = Guid.NewGuid().ToString(), order = 5, sourceFields = new[] { "StageName" }, transformations = new object[] { }, destinationField = "stage_code" }
                }),
                LastRunAt = DateTime.UtcNow.AddHours(-2),
                LastRunStatus = "Completed",
                LastRunRecordsProcessed = 532,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow.AddDays(-15),
                UpdatedAt = DateTime.UtcNow
            },
            new Pipeline
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Product Catalog Extract",
                Description = "Extract product catalog from sales database for data warehouse",
                SourceConnectorId = sourcePostgres.Id,
                DestinationConnectorId = destMysql.Id,
                Status = "Idle",
                IsActive = false,
                EmailNotificationsEnabled = true,
                NotificationEmailsJson = System.Text.Json.JsonSerializer.Serialize(new[] { "admin@demo.com" }),
                FieldMappingsJson = System.Text.Json.JsonSerializer.Serialize(new[]
                {
                    new { id = Guid.NewGuid().ToString(), order = 1, sourceFields = new[] { "product_id" }, transformations = new object[] { }, destinationField = "product_key" },
                    new { id = Guid.NewGuid().ToString(), order = 2, sourceFields = new[] { "product_name" }, transformations = new object[] { }, destinationField = "product_name" },
                    new { id = Guid.NewGuid().ToString(), order = 3, sourceFields = new[] { "category" }, transformations = new object[] { }, destinationField = "category_code" },
                    new { id = Guid.NewGuid().ToString(), order = 4, sourceFields = new[] { "price" }, transformations = new object[] { }, destinationField = "unit_price" }
                }),
                LastRunAt = DateTime.UtcNow.AddDays(-3),
                LastRunStatus = "Completed",
                LastRunRecordsProcessed = 2134,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow.AddDays(-25),
                UpdatedAt = DateTime.UtcNow.AddDays(-3)
            }
        };

        dbContext.Pipelines.AddRange(pipelines);
        await dbContext.SaveChangesAsync();

        // Add schedules for active pipelines
        var scheduledPipelines = pipelines.Where(p => p.Status == "Active").ToList();
        var schedules = new List<PipelineSchedule>();

        schedules.Add(new PipelineSchedule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PipelineId = scheduledPipelines[0].Id,
            CronExpression = "0 2 * * *", // Daily at 2 AM
            Timezone = "UTC",
            Description = "Daily at 2:00 AM UTC",
            IsActive = true,
            NextRunAt = DateTimeOffset.UtcNow.AddHours(18),
            CreatedAt = DateTime.UtcNow.AddDays(-28),
            CreatedBy = userId
        });

        schedules.Add(new PipelineSchedule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PipelineId = scheduledPipelines[1].Id,
            CronExpression = "0 */6 * * *", // Every 6 hours
            Timezone = "UTC",
            Description = "Every 6 hours",
            IsActive = true,
            NextRunAt = DateTimeOffset.UtcNow.AddHours(2),
            CreatedAt = DateTime.UtcNow.AddDays(-20),
            CreatedBy = userId
        });

        schedules.Add(new PipelineSchedule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PipelineId = scheduledPipelines[2].Id,
            CronExpression = "0 */4 * * *", // Every 4 hours
            Timezone = "UTC",
            Description = "Every 4 hours",
            IsActive = true,
            NextRunAt = DateTimeOffset.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow.AddDays(-15),
            CreatedBy = userId
        });

        dbContext.PipelineSchedules.AddRange(schedules);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Seeded {Count} demo pipelines with schedules", pipelines.Length);
    }

    private static async Task SeedDemoExecutionsAsync(ApplicationDbContext dbContext, Guid tenantId, ILogger logger)
    {
        var pipelines = await dbContext.Pipelines.IgnoreQueryFilters().Where(p => p.TenantId == tenantId).ToListAsync();

        var executions = new List<PipelineExecution>();

        // Completed executions
        foreach (var pipeline in pipelines.Take(3))
        {
            executions.Add(new PipelineExecution
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PipelineId = pipeline.Id,
                Status = Domain.Enums.ExecutionStatus.Completed,
                StartTime = DateTimeOffset.UtcNow.AddHours(-8),
                EndTime = DateTimeOffset.UtcNow.AddHours(-8).AddMinutes(12),
                Duration = TimeSpan.FromMinutes(12),
                RecordsProcessed = 1247,
                RecordsSucceeded = 1247,
                RecordsFailed = 0,
                ProgressPercent = 100,
                BatchCount = 2,
                TriggeredBy = "Scheduled",
                SummaryJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    summary = "Execution completed successfully",
                    duration = "12m 34s"
                }),
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-8)
            });
        }

        // Failed execution
        executions.Add(new PipelineExecution
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PipelineId = pipelines[1].Id,
            Status = Domain.Enums.ExecutionStatus.Failed,
            StartTime = DateTimeOffset.UtcNow.AddDays(-1).AddHours(-6),
            EndTime = DateTimeOffset.UtcNow.AddDays(-1).AddHours(-6).AddMinutes(3),
            Duration = TimeSpan.FromMinutes(3),
            RecordsProcessed = 234,
            RecordsSucceeded = 234,
            RecordsFailed = 1,
            ProgressPercent = 45,
            BatchCount = 1,
            ErrorMessage = "Connection timeout to destination database after 30 seconds",
            TriggeredBy = "Scheduled",
            SummaryJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                error = "Database connection error",
                details = "TCP connection timeout"
            }),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1).AddHours(-6)
        });

        // Running execution
        executions.Add(new PipelineExecution
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PipelineId = pipelines[0].Id,
            Status = Domain.Enums.ExecutionStatus.Running,
            StartTime = DateTimeOffset.UtcNow.AddMinutes(-5),
            EndTime = null,
            Duration = null,
            RecordsProcessed = 532,
            RecordsSucceeded = 532,
            RecordsFailed = 0,
            ProgressPercent = 60,
            BatchCount = 1,
            TriggeredBy = "Manual",
            SummaryJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                status = "In progress",
                progress = "60%"
            }),
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        });

        // More completed executions from different times
        executions.Add(new PipelineExecution
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PipelineId = pipelines[2].Id,
            Status = Domain.Enums.ExecutionStatus.Completed,
            StartTime = DateTimeOffset.UtcNow.AddHours(-4),
            EndTime = DateTimeOffset.UtcNow.AddHours(-4).AddMinutes(8),
            Duration = TimeSpan.FromMinutes(8),
            RecordsProcessed = 892,
            RecordsSucceeded = 892,
            RecordsFailed = 0,
            ProgressPercent = 100,
            BatchCount = 1,
            TriggeredBy = "Scheduled",
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-4)
        });

        executions.Add(new PipelineExecution
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PipelineId = pipelines[0].Id,
            Status = Domain.Enums.ExecutionStatus.Completed,
            StartTime = DateTimeOffset.UtcNow.AddDays(-2),
            EndTime = DateTimeOffset.UtcNow.AddDays(-2).AddMinutes(15),
            Duration = TimeSpan.FromMinutes(15),
            RecordsProcessed = 2134,
            RecordsSucceeded = 2134,
            RecordsFailed = 0,
            ProgressPercent = 100,
            BatchCount = 3,
            TriggeredBy = "Scheduled",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-2)
        });

        // Cancelled execution
        executions.Add(new PipelineExecution
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PipelineId = pipelines[1].Id,
            Status = Domain.Enums.ExecutionStatus.Cancelled,
            StartTime = DateTimeOffset.UtcNow.AddDays(-3).AddHours(-2),
            EndTime = DateTimeOffset.UtcNow.AddDays(-3).AddHours(-2).AddMinutes(5),
            Duration = TimeSpan.FromMinutes(5),
            RecordsProcessed = 156,
            RecordsSucceeded = 156,
            RecordsFailed = 0,
            ProgressPercent = 35,
            BatchCount = 1,
            ErrorMessage = "Execution cancelled by user",
            TriggeredBy = "Scheduled",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-3).AddHours(-2)
        });

        dbContext.PipelineExecutions.AddRange(executions);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Seeded {Count} demo executions", executions.Count);
    }
}
