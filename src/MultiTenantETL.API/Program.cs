using System.Security.Cryptography.X509Certificates;
using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MultiTenantETL.API.Middleware;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Interfaces;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Infrastructure.Authorization.Handlers;
using MultiTenantETL.Infrastructure.Authorization.Requirements;
using MultiTenantETL.Infrastructure.Identity;
using MultiTenantETL.Infrastructure.Persistence;
using MultiTenantETL.Infrastructure.Security;
using MultiTenantETL.Infrastructure.Services;
using OpenIddict.Abstractions;

var builder = WebApplication.CreateBuilder(args);

// Configure logging to suppress watch debug logs
builder.Logging.AddFilter("Microsoft.AspNetCore.Watch", LogLevel.None);

// Response Caching
builder.Services.AddResponseCaching();

// Configure Kestrel limits for DDoS protection
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    // Limit request body size to 30MB (adjust based on your file upload needs)
    serverOptions.Limits.MaxRequestBodySize = 30 * 1024 * 1024;
    
    // Limit maximum concurrent connections
    serverOptions.Limits.MaxConcurrentConnections = 100;
    serverOptions.Limits.MaxConcurrentUpgradedConnections = 100;
    
    // Request line and header limits
    serverOptions.Limits.MaxRequestLineSize = 8 * 1024; // 8KB
    serverOptions.Limits.MaxRequestHeadersTotalSize = 32 * 1024; // 32KB
    serverOptions.Limits.MaxRequestHeaderCount = 100;
    
    // Timeout configurations
    serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
    serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
    
    // Minimum data rate to prevent slow-read attacks (Slowloris)
    serverOptions.Limits.MinRequestBodyDataRate = new Microsoft.AspNetCore.Server.Kestrel.Core.MinDataRate(
        bytesPerSecond: 240, // 240 bytes/sec minimum
        gracePeriod: TimeSpan.FromSeconds(10)
    );
    
    serverOptions.Limits.MinResponseDataRate = new Microsoft.AspNetCore.Server.Kestrel.Core.MinDataRate(
        bytesPerSecond: 240,
        gracePeriod: TimeSpan.FromSeconds(10)
    );
});

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false; // Set to true in production
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure token lifespan
builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromHours(24); // Email confirmation tokens
});

// OpenIddict
builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
            .UseDbContext<ApplicationDbContext>();
        options.UseQuartz();
    })
    .AddServer(options =>
    {
        // Endpoints
        options.SetTokenEndpointUris("/connect/token")
               .SetAuthorizationEndpointUris("/connect/authorize")
               .SetRevocationEndpointUris("/connect/revoke");

        // Flows
        options.AllowPasswordFlow()
               .AllowRefreshTokenFlow()
               .AllowAuthorizationCodeFlow()
               .RequireProofKeyForCodeExchange();

        // Scopes
        options.RegisterScopes(
            OpenIddictConstants.Scopes.OpenId,
            OpenIddictConstants.Scopes.Email,
            OpenIddictConstants.Scopes.Profile,
            OpenIddictConstants.Scopes.Roles,
            OpenIddictConstants.Scopes.OfflineAccess,  // Required for refresh tokens
            "api"
        );
        
        // Allow offline_access scope to be granted without explicit consent
        options.AllowRefreshTokenFlow();

        // Register claims to include in tokens
        options.RegisterClaims(
            OpenIddictConstants.Claims.Role,
            System.Security.Claims.ClaimTypes.Role
        );

        // Certificates
        if (builder.Environment.IsDevelopment())
        {
            options.AddDevelopmentEncryptionCertificate()
                   .AddDevelopmentSigningCertificate();
        }
        else
        {
            // Production certificates
            options.AddEncryptionCertificate(LoadCertificate("CN=ETL-Encryption"));
            options.AddSigningCertificate(LoadCertificate("CN=ETL-Signing"));
        }

        // ASP.NET Core integration
        options.UseAspNetCore()
               .EnableTokenEndpointPassthrough()
               .EnableAuthorizationEndpointPassthrough();

        // Token lifetimes
        options.SetAccessTokenLifetime(TimeSpan.FromMinutes(15));
        options.SetRefreshTokenLifetime(TimeSpan.FromDays(7));
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

// Authentication & Authorization
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = OpenIddict.Validation.AspNetCore.OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
    options.DefaultAuthenticateScheme = OpenIddict.Validation.AspNetCore.OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIddict.Validation.AspNetCore.OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
});

// Configure Identity options to map role claims correctly
builder.Services.Configure<IdentityOptions>(options =>
{
    // Map role claim type for authorization
    options.ClaimsIdentity.RoleClaimType = System.Security.Claims.ClaimTypes.Role;
    options.ClaimsIdentity.UserNameClaimType = OpenIddictConstants.Claims.Name;
    options.ClaimsIdentity.UserIdClaimType = OpenIddictConstants.Claims.Subject;
});

// Authorization with policies
builder.Services.AddAuthorization(options =>
{
    // Tenant resource policy - ensures users only access their tenant's data
    options.AddPolicy(Policies.TenantResource, policy =>
    {
        policy.Requirements.Add(new TenantResourceRequirement());
    });

    // Role-based policy shortcuts
    options.AddPolicy(Policies.RequireSuperAdmin, policy =>
    {
        policy.RequireRole(Roles.SuperAdmin);
    });

    options.AddPolicy(Policies.RequireTenantAdmin, policy =>
    {
        policy.RequireRole(Roles.SuperAdmin, Roles.TenantAdmin);
    });
});

// Register authorization handlers
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, TenantResourceAuthorizationHandler>();

// Configure Azure Communication Services settings
builder.Services.Configure<MultiTenantETL.Infrastructure.Configuration.AzureCommunicationSettings>(
    builder.Configuration.GetSection("AzureCommunicationServices"));

// Configure ETL settings
builder.Services.Configure<MultiTenantETL.Infrastructure.Configuration.EtlSettings>(
    builder.Configuration.GetSection("Etl"));

// Rate Limiting
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(options =>
{
    options.EnableEndpointRateLimiting = true;
    options.StackBlockedRequests = false;
    options.HttpStatusCode = 429;
    options.RealIpHeader = "X-Real-IP";
    options.ClientIdHeader = "X-ClientId";

    options.GeneralRules = new List<RateLimitRule>
    {
        // Metadata endpoint - permissive but protected (public configuration data)
        new RateLimitRule
        {
            Endpoint = "GET:/api/metadata/*",
            Period = "1m",
            Limit = 200  // Per IP: allows legitimate use, blocks abuse
        },

        // Authentication endpoints - very strict
        new RateLimitRule
        {
            Endpoint = "POST:/connect/token",
            Period = "1m",
            Limit = 5  // 5 login attempts per minute
        },
        new RateLimitRule
        {
            Endpoint = "POST:/api/account/register",
            Period = "1h",
            Limit = 3  // 3 registrations per hour
        },
        new RateLimitRule
        {
            Endpoint = "POST:/api/account/forgot-password",
            Period = "15m",
            Limit = 3  // 3 password reset requests per 15 min
        },

        // Write operations - moderate
        new RateLimitRule
        {
            Endpoint = "POST:*",
            Period = "1m",
            Limit = 30
        },
        new RateLimitRule
        {
            Endpoint = "PUT:*",
            Period = "1m",
            Limit = 30
        },
        new RateLimitRule
        {
            Endpoint = "DELETE:*",
            Period = "1m",
            Limit = 20
        },

        // Read operations - permissive
        new RateLimitRule
        {
            Endpoint = "GET:*",
            Period = "1m",
            Limit = 100
        }
    };
});

builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();

// CORS Configuration
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? new[] { "http://localhost:5173" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            .WithExposedHeaders("Cache-Control", "Expires", "Pragma", "Age");
    });
});

// Email Service
var useStubEmailService = builder.Configuration.GetValue<bool>("EmailService:UseStub", true);
if (useStubEmailService)
{
    builder.Services.AddScoped<IEmailService, StubEmailService>();
}
else
{
    builder.Services.AddScoped<IEmailService, AzureCommunicationEmailService>();
}

// Custom Services
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient(); // For API connector testing
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.Interfaces.IClaimsService,
    MultiTenantETL.Infrastructure.Services.ClaimsService>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.Interfaces.ITenantService,
    MultiTenantETL.Infrastructure.Services.TenantService>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.Interfaces.IUserService,
    MultiTenantETL.Infrastructure.Services.UserService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddSingleton<IInputSanitizer, InputSanitizer>();
builder.Services.AddSingleton<IMetadataService, MetadataService>();
builder.Services.AddSingleton<IEncryptionService, MultiTenantETL.Infrastructure.Security.EncryptionService>();

// Connector Services
builder.Services.AddScoped<MultiTenantETL.Application.Connectors.IConnectorService,
    MultiTenantETL.Infrastructure.Services.ConnectorService>();
builder.Services.AddScoped<MultiTenantETL.Application.Connectors.IConnectionTester,
    MultiTenantETL.Infrastructure.Services.ConnectionTesting.ConnectionTester>();
builder.Services.AddScoped<MultiTenantETL.Application.Connectors.ISchemaDetector,
    MultiTenantETL.Infrastructure.Services.SchemaDetector>();

// Transformation Services
builder.Services.AddScoped<MultiTenantETL.Application.Transformations.ITransformationService,
    MultiTenantETL.Infrastructure.Services.TransformationService>();

// Pipeline Services
builder.Services.AddScoped<MultiTenantETL.Application.Pipelines.IPipelineService,
    MultiTenantETL.Infrastructure.Services.PipelineService>();

// Execution Services
builder.Services.AddScoped<MultiTenantETL.Application.Executions.IExecutionService,
    MultiTenantETL.Infrastructure.Services.ExecutionService>();

// Data Readers
builder.Services.AddScoped<MultiTenantETL.Application.Connectors.DataReaders.IDataReader,
    MultiTenantETL.Infrastructure.DataReaders.SqlServerDataReader>();
builder.Services.AddScoped<MultiTenantETL.Application.Connectors.DataReaders.IDataReader,
    MultiTenantETL.Infrastructure.DataReaders.PostgreSqlDataReader>();
builder.Services.AddScoped<MultiTenantETL.Application.Connectors.DataReaders.IDataReader,
    MultiTenantETL.Infrastructure.DataReaders.MySqlConnectorDataReader>();
builder.Services.AddScoped<MultiTenantETL.Application.Connectors.DataReaders.IDataReader,
    MultiTenantETL.Infrastructure.DataReaders.CsvDataReader>();
builder.Services.AddScoped<MultiTenantETL.Application.Connectors.DataReaders.IDataReader,
    MultiTenantETL.Infrastructure.DataReaders.JsonDataReader>();
builder.Services.AddScoped<MultiTenantETL.Application.Connectors.DataReaders.IDataReader,
    MultiTenantETL.Infrastructure.DataReaders.NdjsonDataReader>();

// Data Writers
builder.Services.AddScoped<MultiTenantETL.Application.Connectors.DataWriters.IDataWriter,
    MultiTenantETL.Infrastructure.DataWriters.SqlServerDataWriter>();
builder.Services.AddScoped<MultiTenantETL.Application.Connectors.DataWriters.IDataWriter,
    MultiTenantETL.Infrastructure.DataWriters.PostgreSqlDataWriter>();
builder.Services.AddScoped<MultiTenantETL.Application.Connectors.DataWriters.IDataWriter,
    MultiTenantETL.Infrastructure.DataWriters.MySqlConnectorDataWriter>();
builder.Services.AddScoped<MultiTenantETL.Application.Connectors.DataWriters.IDataWriter,
    MultiTenantETL.Infrastructure.DataWriters.CsvDataWriter>();
builder.Services.AddScoped<MultiTenantETL.Application.Connectors.DataWriters.IDataWriter,
    MultiTenantETL.Infrastructure.DataWriters.JsonDataWriter>();
builder.Services.AddScoped<MultiTenantETL.Application.Connectors.DataWriters.IDataWriter,
    MultiTenantETL.Infrastructure.DataWriters.NdjsonDataWriter>();

// Data Reader/Writer Factories
builder.Services.AddScoped<MultiTenantETL.Infrastructure.Services.DataReaderFactory>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.Services.DataWriterFactory>();

// Connection Tester Services
builder.Services.AddScoped<MultiTenantETL.Infrastructure.Services.ConnectionTesting.Database.IDatabaseConnectionTester,
    MultiTenantETL.Infrastructure.Services.ConnectionTesting.Database.DatabaseConnectionTester>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.Services.ConnectionTesting.Storage.IStorageConnectionTester,
    MultiTenantETL.Infrastructure.Services.ConnectionTesting.Storage.StorageConnectionTester>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.Services.ConnectionTesting.Api.IApiConnectionTester,
    MultiTenantETL.Infrastructure.Services.ConnectionTesting.Api.ApiConnectionTester>();

// Storage Connection Testers
builder.Services.AddScoped<MultiTenantETL.Infrastructure.Services.ConnectionTesting.Storage.AzureBlobConnectionTester>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.Services.ConnectionTesting.Storage.S3ConnectionTester>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.Services.ConnectionTesting.Storage.FtpConnectionTester>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.Services.ConnectionTesting.Storage.SftpConnectionTester>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Security Headers - should be early in pipeline
app.UseSecurityHeaders();

// CORS - must be before response caching to ensure headers are always present
app.UseCors("AllowFrontend");

// Response caching - after CORS to cache responses with CORS headers
app.UseResponseCaching();

// Rate limiting - before authentication
app.UseIpRateLimiting();

// Only use HTTPS redirection in production
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

// Seed database in development
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<Program>>();
        try
        {
            await MultiTenantETL.Infrastructure.Data.DbSeeder.SeedAsync(services);
            logger.LogInformation("Database seeding completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding the database");
        }
    }
}


app.Run();

// Helper method to load certificate
static X509Certificate2 LoadCertificate(string subjectName)
{
    using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
    store.Open(OpenFlags.ReadOnly);
    
    var certificates = store.Certificates.Find(
        X509FindType.FindBySubjectDistinguishedName,
        subjectName,
        validOnly: false);
    
    if (certificates.Count == 0)
    {
        throw new InvalidOperationException($"Certificate '{subjectName}' not found in certificate store.");
    }
    
    return certificates[0];
}
