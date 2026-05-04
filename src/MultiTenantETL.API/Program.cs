using AspNetCoreRateLimit;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MultiTenantETL.API.Middleware;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Interfaces;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Infrastructure.Authorization.Handlers;
using MultiTenantETL.Infrastructure.Authorization.Requirements;
using MultiTenantETL.Infrastructure.Configuration;
using MultiTenantETL.Infrastructure.Identity;
using MultiTenantETL.Infrastructure.Persistence;
using MultiTenantETL.Infrastructure.Scheduling;
using MultiTenantETL.Infrastructure.Security;
using MultiTenantETL.Infrastructure.Services;
using OpenIddict.Abstractions;
using Quartz;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults (includes OpenTelemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add Sentry
builder.WebHost.UseSentry();

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
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
        o => o.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));
});

// Configure shared Data Protection for container scaling
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDbContext>()
    .SetApplicationName("MultiTenantETL");

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "XSRF-TOKEN";
    options.Cookie.HttpOnly = false; // SPA must read the CSRF cookie and mirror it in header.
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

// Quartz.NET Scheduler - shared by OpenIddict and Pipeline Scheduling
builder.Services.AddQuartz(q =>
{
    q.UseSimpleTypeLoader();
    q.UseInMemoryStore();
    q.UseDefaultThreadPool(tp => tp.MaxConcurrency = 10);

    // Register the pipeline schedule job
    q.AddJob<PipelineScheduleJob>(opts => opts
        .WithIdentity("PipelineScheduleJobTemplate")
        .StoreDurably(true)
        .WithDescription("Template job for scheduled pipeline executions"));
});

// Add Quartz hosted service for background scheduling
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

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
    options.SignIn.RequireConfirmedEmail = builder.Configuration.GetValue<bool>("Authentication:RequireEmailConfirmation");
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure Identity application cookie for proper logout behavior
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = ".AspNetCore.Identity.Application";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.Path = "/";
    options.ExpireTimeSpan = TimeSpan.FromHours(1);
    options.SlidingExpiration = true;
    options.LoginPath = "/auth/login";
    options.LogoutPath = "/api/account/logout";
    options.Events.OnRedirectToLogin = context =>
    {
        // APIs should return 401 instead of redirecting to HTML login page.
        if (context.Request.Path.StartsWithSegments("/api") ||
            context.Request.Headers.Accept.Any(h => h.Contains("application/json", StringComparison.OrdinalIgnoreCase)))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api") ||
            context.Request.Headers.Accept.Any(h => h.Contains("application/json", StringComparison.OrdinalIgnoreCase)))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

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
        // options.AllowRefreshTokenFlow(); // Already allowed above

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
            options.AddEncryptionCertificate(LoadCertificate("EtlEncryption", "CN=ETL-Encryption", builder.Configuration));
            options.AddSigningCertificate(LoadCertificate("EtlSigning", "CN=ETL-Signing", builder.Configuration));
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
    options.DefaultScheme = "DynamicAuth";
    options.DefaultAuthenticateScheme = "DynamicAuth";
    options.DefaultChallengeScheme = "DynamicAuth";
})
.AddPolicyScheme("DynamicAuth", "Bearer or Cookie", options =>
{
    options.ForwardDefaultSelector = context =>
    {
        var authorization = context.Request.Headers.Authorization.ToString();

        // If bearer token is explicitly provided, validate as JWT/OIDC token.
        if (!string.IsNullOrWhiteSpace(authorization) &&
            authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return OpenIddict.Validation.AspNetCore.OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
        }

        // Otherwise, use Identity application cookie (BFF/session mode).
        return IdentityConstants.ApplicationScheme;
    };
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
    builder.Configuration.GetSection(MultiTenantETL.Infrastructure.Configuration.EtlSettings.SectionName));

// Configure Messaging Provider settings
builder.Services.Configure<MultiTenantETL.Infrastructure.Configuration.MessagingSettings>(
    builder.Configuration.GetSection(MultiTenantETL.Infrastructure.Configuration.MessagingSettings.SectionName));

// Configure RabbitMQ settings with Aspire connection string support
builder.Services.Configure<MultiTenantETL.Infrastructure.Configuration.RabbitMqSettings>(options =>
{
    builder.Configuration.GetSection("RabbitMq").Bind(options);
    // Check for Aspire-provided connection string
    var connectionString = builder.Configuration.GetConnectionString("RabbitMq");
    if (!string.IsNullOrEmpty(connectionString))
    {
        options.ConnectionString = connectionString;
    }
});

// Configure Azure Service Bus settings with Aspire connection string support
builder.Services.Configure<MultiTenantETL.Infrastructure.Configuration.ServiceBusSettings>(options =>
{
    builder.Configuration.GetSection("ServiceBus").Bind(options);
    // Check for Aspire-provided connection string
    var connectionString = builder.Configuration.GetConnectionString("ServiceBus");
    if (!string.IsNullOrEmpty(connectionString))
    {
        options.ConnectionString = connectionString;
    }
});

// Configure Azure Storage Queue settings with Aspire connection string support
builder.Services.Configure<MultiTenantETL.Infrastructure.Configuration.StorageQueueSettings>(options =>
{
    builder.Configuration.GetSection("StorageQueue").Bind(options);
    // Check for Aspire-provided connection string
    var connectionString = builder.Configuration.GetConnectionString("StorageQueue");
    if (!string.IsNullOrEmpty(connectionString))
    {
        options.ConnectionString = connectionString;
    }
});

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

        // OAuth/OpenIddict endpoints - need higher limits for authorization flow
        new RateLimitRule
        {
            Endpoint = "*:/connect/authorize",
            Period = "1m",
            Limit = 20  // OAuth flow can make multiple requests
        },
        new RateLimitRule
        {
            Endpoint = "POST:/connect/token",
            Period = "1m",
            Limit = 15  // Increased to handle token exchange + refresh attempts
        },
        new RateLimitRule
        {
            Endpoint = "POST:/connect/revoke",
            Period = "1m",
            Limit = 10  // Token revocation
        },

        // Login page - should not be rate limited (it's just HTML)
        new RateLimitRule
        {
            Endpoint = "GET:/auth/login",
            Period = "1m",
            Limit = 50
        },
        new RateLimitRule
        {
            Endpoint = "POST:/auth/login",
            Period = "1m",
            Limit = 15  // Actual login form submissions
        },

        // Account management endpoints
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
        new RateLimitRule
        {
            Endpoint = "POST:/api/account/logout",
            Period = "1m",
            Limit = 10  // Allow multiple logout attempts
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
    .Get<string[]>() ?? throw new InvalidOperationException("Cors:AllowedOrigins is not configured in appsettings.");

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            .WithExposedHeaders("Cache-Control", "Expires", "Pragma", "Age", "X-Logout-Success", "Clear-Site-Data");
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

// Tenant context provider (scoped per request/job)
builder.Services.AddScoped<ITenantProvider, TenantProvider>();

builder.Services.AddScoped<MultiTenantETL.Infrastructure.Services.Http.IHttpClientAuthenticator,
    MultiTenantETL.Infrastructure.Services.Http.HttpClientAuthenticator>();
builder.Services.AddSingleton<MultiTenantETL.Infrastructure.Services.Database.IDatabaseConnectionStringBuilder,
    MultiTenantETL.Infrastructure.Services.Database.DatabaseConnectionStringBuilder>();
builder.Services.AddSingleton<MultiTenantETL.Infrastructure.Services.Storage.IStorageClientFactory,
    MultiTenantETL.Infrastructure.Services.Storage.StorageClientFactory>();
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

// Azure Key Vault Configuration
builder.Services.Configure<MultiTenantETL.Infrastructure.Configuration.AzureKeyVaultSettings>(
    builder.Configuration.GetSection("AzureKeyVault"));

// Secret Storage Services (Azure Key Vault)
builder.Services.AddSingleton<MultiTenantETL.Application.Common.Interfaces.ISecretStorageService,
    MultiTenantETL.Infrastructure.Security.KeyVaultSecretStorageService>();
builder.Services.AddScoped<MultiTenantETL.Application.Common.Interfaces.ISecretResolver,
    MultiTenantETL.Infrastructure.Security.SecretResolver>();

// Connector Services
builder.Services.AddScoped<MultiTenantETL.Application.Connectors.IConnectorService,
    MultiTenantETL.Infrastructure.Services.ConnectorService>();
builder.Services.AddScoped<MultiTenantETL.Application.Connectors.IConnectionTester,
    MultiTenantETL.Infrastructure.Services.ConnectionTesting.ConnectionTester>();
builder.Services.AddScoped<MultiTenantETL.Application.Connectors.ISchemaDetector,
    MultiTenantETL.Infrastructure.Services.SchemaDetector>();

// Transformation Services
// Field Transformation Processor (for complex field mappings)
builder.Services.AddScoped<MultiTenantETL.Infrastructure.Transformations.FieldProcessors.IFieldTransformationProcessor,
    MultiTenantETL.Infrastructure.Transformations.FieldProcessors.FieldTransformationProcessor>();

// Pipeline Services
builder.Services.AddScoped<MultiTenantETL.Application.Pipelines.IPipelineService,
    MultiTenantETL.Infrastructure.Services.PipelineService>();

// Execution Services
builder.Services.AddScoped<MultiTenantETL.Application.Executions.IExecutionService,
    MultiTenantETL.Infrastructure.Services.ExecutionService>();

// Scheduling Services
builder.Services.AddScoped<MultiTenantETL.Application.Scheduling.IScheduleService,
    MultiTenantETL.Infrastructure.Scheduling.ScheduleService>();
builder.Services.AddHostedService<MultiTenantETL.Infrastructure.Scheduling.ScheduleInitializerService>();

// Messaging Services - register based on configuration
var messagingSettings = new MultiTenantETL.Infrastructure.Configuration.MessagingSettings();
builder.Configuration.GetSection(MultiTenantETL.Infrastructure.Configuration.MessagingSettings.SectionName).Bind(messagingSettings);

if (messagingSettings.UseServiceBus)
{
    builder.Services.AddSingleton<MultiTenantETL.Application.Messaging.IMessagePublisher,
        MultiTenantETL.Infrastructure.Messaging.ServiceBusPublisher>();
}
else if (messagingSettings.UseStorageQueue)
{
    builder.Services.AddSingleton<MultiTenantETL.Application.Messaging.IMessagePublisher,
        MultiTenantETL.Infrastructure.Messaging.StorageQueuePublisher>();
}
else
{
    // Default to RabbitMQ for local development
    builder.Services.AddSingleton<MultiTenantETL.Application.Messaging.IMessagePublisher,
        MultiTenantETL.Infrastructure.Messaging.RabbitMqPublisher>();
}

// Orchestration Services
builder.Services.AddScoped<MultiTenantETL.Application.Orchestration.IPipelineOrchestrator,
    MultiTenantETL.Infrastructure.Orchestration.PipelineOrchestrator>();
builder.Services.AddScoped<MultiTenantETL.Application.Orchestration.IFieldMappingService,
    MultiTenantETL.Infrastructure.Orchestration.FieldMappingService>();

// Data Readers
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataReaders.SqlServerDataReader>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataReaders.PostgreSqlDataReader>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataReaders.MySqlDataReader>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataReaders.OracleDataReader>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataReaders.MongoDbDataReader>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataReaders.CosmosDbDataReader>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataReaders.CsvDataReader>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataReaders.JsonDataReader>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataReaders.JsonLinesDataReader>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataReaders.RestApiDataReader>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataReaders.AzureBlobDataReader>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataReaders.SftpDataReader>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataReaders.FtpDataReader>();

// Data Writers
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataWriters.SqlServerDataWriter>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataWriters.PostgreSqlDataWriter>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataWriters.MySqlDataWriter>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataWriters.OracleDataWriter>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataWriters.MongoDbDataWriter>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataWriters.CosmosDbDataWriter>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataWriters.CsvDataWriter>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataWriters.JsonDataWriter>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataWriters.JsonLinesDataWriter>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataWriters.RestApiDataWriter>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataWriters.AzureBlobDataWriter>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataWriters.SftpDataWriter>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataWriters.FtpDataWriter>();

// Format Validation
builder.Services.AddScoped<MultiTenantETL.Application.Connectors.DataWriters.IFormatValidator,
    MultiTenantETL.Infrastructure.DataWriters.FormatValidator>();

// Data Reader Factories
builder.Services.AddScoped<MultiTenantETL.Application.DataAccess.IDataReaderFactory,
    MultiTenantETL.Infrastructure.DataAccess.Readers.DataReaderFactory>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataAccess.Readers.Database.IDatabaseDataReaderFactory,
    MultiTenantETL.Infrastructure.DataAccess.Readers.Database.DatabaseDataReaderFactory>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataAccess.Readers.File.IFileDataReaderFactory,
    MultiTenantETL.Infrastructure.DataAccess.Readers.File.FileDataReaderFactory>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataAccess.Readers.Api.IApiDataReaderFactory,
    MultiTenantETL.Infrastructure.DataAccess.Readers.Api.ApiDataReaderFactory>();

// Data Writer Factories
builder.Services.AddScoped<MultiTenantETL.Application.DataAccess.IDataWriterFactory,
    MultiTenantETL.Infrastructure.DataAccess.Writers.DataWriterFactory>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataAccess.Writers.Database.IDatabaseDataWriterFactory,
    MultiTenantETL.Infrastructure.DataAccess.Writers.Database.DatabaseDataWriterFactory>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataAccess.Writers.File.IFileDataWriterFactory,
    MultiTenantETL.Infrastructure.DataAccess.Writers.File.FileDataWriterFactory>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataAccess.Writers.Api.IApiDataWriterFactory,
    MultiTenantETL.Infrastructure.DataAccess.Writers.Api.ApiDataWriterFactory>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataAccess.Writers.Email.IEmailDataWriterFactory,
    MultiTenantETL.Infrastructure.DataAccess.Writers.Email.EmailDataWriterFactory>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataWriters.EmailDataWriter>();

// Connection Tester Services
builder.Services.AddScoped<MultiTenantETL.Infrastructure.Services.ConnectionTesting.Database.IDatabaseConnectionTester,
    MultiTenantETL.Infrastructure.Services.ConnectionTesting.Database.DatabaseConnectionTester>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.Services.ConnectionTesting.Storage.IStorageConnectionTester,
    MultiTenantETL.Infrastructure.Services.ConnectionTesting.Storage.StorageConnectionTester>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.Services.ConnectionTesting.Api.IApiConnectionTester,
    MultiTenantETL.Infrastructure.Services.ConnectionTesting.Api.ApiConnectionTester>();

// Storage Connection Testers
builder.Services.AddScoped<MultiTenantETL.Infrastructure.Services.ConnectionTesting.Storage.AzureBlobConnectionTester>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.Services.ConnectionTesting.Storage.FtpConnectionTester>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.Services.ConnectionTesting.Storage.SftpConnectionTester>();

// Global Exception Handler
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// FluentValidation - register all validators from Application assembly
builder.Services.AddValidatorsFromAssemblyContaining<MultiTenantETL.Application.Connectors.Validators.CreateConnectorRequestValidator>();

builder.Services.AddFluentValidationAutoValidation()
    .AddFluentValidationClientsideAdapters();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        JsonSerializerOptionsProvider.Configure(options.JsonSerializerOptions);
    });

// Add Razor Pages for OAuth login page
builder.Services.AddRazorPages();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure forwarded headers for Azure Container Apps proxy
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Azure Container Apps proxy configuration
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// Configure forwarded headers first so all subsequent middleware sees the correct scheme (HTTPS)
app.UseForwardedHeaders();

// Global exception handler - should be first to catch all exceptions
app.UseExceptionHandler();

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

// Static files for OAuth login page CSS
app.UseStaticFiles();

// Response caching - after CORS to cache responses with CORS headers
app.UseResponseCaching();

// Rate limiting - before authentication
// More lenient in development for testing, strict in production
if (!app.Environment.IsDevelopment())
{
    app.UseIpRateLimiting();
}

// Only use HTTPS redirection in production
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();

// Validate CSRF tokens for unsafe API/BFF requests.
app.UseApiAntiforgery();

// Populate tenant context from JWT claims (must be after authentication)
app.UseTenantContext();

app.UseAuthorization();

app.MapControllers();

// Map Razor Pages for OAuth login
app.MapRazorPages();

// Map Aspire default endpoints (health checks)
app.MapDefaultEndpoints();

// Apply migrations and seed database in development
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<Program>>();
        var dbContext = services.GetRequiredService<MultiTenantETL.Infrastructure.Persistence.ApplicationDbContext>();

        try
        {
            // Apply pending migrations (creates database if it doesn't exist)
            logger.LogInformation("Applying database migrations...");
            await dbContext.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied successfully");

            // Seed the database
            await MultiTenantETL.Infrastructure.Data.DbSeeder.SeedAsync(services);
            logger.LogInformation("Database seeding completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while migrating or seeding the database");
        }
    }
}


app.Run();

// Helper method to load certificate
static X509Certificate2 LoadCertificate(string configKey, string subjectName, IConfiguration configuration)
{
    // First, try to load from file if configured
    var certPath = configuration[$"Certificates:{configKey}:Path"];
    if (!string.IsNullOrEmpty(certPath))
    {
        var password = configuration[$"Certificates:{configKey}:Password"];
        if (File.Exists(certPath))
        {
            return new X509Certificate2(certPath, password);
        }
        else
        {
            throw new InvalidOperationException($"Certificate file '{certPath}' not found.");
        }
    }

    // Fallback to base64 PFX from environment/config
    var pfxBase64 = configuration[$"Certificates:{configKey}:PfxBase64"];
    if (!string.IsNullOrEmpty(pfxBase64))
    {
        var pfxBytes = Convert.FromBase64String(pfxBase64);
        var password = configuration[$"Certificates:{configKey}:Password"];
        return new X509Certificate2(pfxBytes, password);
    }

    // Fallback to certificate store (for backward compatibility)
    using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
    store.Open(OpenFlags.ReadOnly);

    var certificates = store.Certificates.Find(
        X509FindType.FindBySubjectDistinguishedName,
        subjectName,
        validOnly: false);

    if (certificates.Count == 0)
    {
        throw new InvalidOperationException($"Certificate '{subjectName}' not found in certificate store, file, or configuration.");
    }

    return certificates[0];
}
