# Clean Architecture Structure

**Purpose:** Organize authentication/authorization code following Clean Architecture principles

---

## Overview

Clean Architecture separates concerns into layers with strict dependency rules:
- **Domain** → No dependencies (pure business logic)
- **Application** → Depends on Domain only
- **Infrastructure** → Depends on Domain & Application
- **Presentation (API)** → Depends on all layers (composition root)

### Pragmatic Decision: Authentication in Infrastructure

**Why authentication entities live in Infrastructure:**

ASP.NET Core Identity is deeply coupled to Entity Framework and the framework itself. Rather than forcing awkward abstractions, we pragmatically accept that:

- **`ApplicationUser`**, **`ApplicationRole`**, and **`UserTenant`** are Infrastructure concerns
- They extend framework classes (`IdentityUser<Guid>`, `IdentityRole<Guid>`)
- Authentication is infrastructure-level, not domain logic
- This reduces complexity and follows the framework's natural design

**What stays in Domain:**
- Pure business entities (`Tenant`, `Pipeline`, etc.)
- Business rules and validations
- Domain interfaces (`ITenantResource`)

This pragmatic approach balances architectural purity with real-world maintainability.

---

## Project Structure

```
MultiTenantETL/
├── src/
│   ├── MultiTenantETL.Domain/
│   │   ├── Entities/
│   │   │   ├── Tenant.cs
│   │   │   ├── Pipeline.cs
│   │   │   ├── Connector.cs
│   │   │   └── ...
│   │   ├── ValueObjects/
│   │   │   ├── Email.cs
│   │   │   └── Permission.cs
│   │   ├── Enums/
│   │   │   ├── AuthErrorCode.cs
│   │   │   └── PipelineStatus.cs
│   │   ├── Exceptions/
│   │   │   ├── DomainException.cs
│   │   │   ├── UnauthorizedException.cs
│   │   │   └── TenantAccessDeniedException.cs
│   │   └── Interfaces/
│   │       └── ITenantResource.cs
│   │
│   ├── MultiTenantETL.Application/
│   │   ├── Common/
│   │   │   ├── Interfaces/
│   │   │   │   ├── IApplicationDbContext.cs
│   │   │   │   ├── ICurrentUserService.cs
│   │   │   │   ├── IEmailService.cs
│   │   │   │   └── IDateTimeProvider.cs
│   │   │   ├── Models/
│   │   │   │   ├── ErrorResponse.cs
│   │   │   │   ├── Result.cs
│   │   │   │   └── PaginatedList.cs
│   │   │   └── Behaviors/
│   │   │       ├── ValidationBehavior.cs
│   │   │       └── LoggingBehavior.cs
│   │   ├── Authentication/
│   │   │   ├── Commands/
│   │   │   │   ├── Login/
│   │   │   │   │   ├── LoginCommand.cs
│   │   │   │   │   ├── LoginCommandHandler.cs
│   │   │   │   │   └── LoginCommandValidator.cs
│   │   │   │   ├── Register/
│   │   │   │   │   ├── RegisterCommand.cs
│   │   │   │   │   ├── RegisterCommandHandler.cs
│   │   │   │   │   └── RegisterCommandValidator.cs
│   │   │   │   ├── ConfirmEmail/
│   │   │   │   ├── ResetPassword/
│   │   │   │   ├── ChangePassword/
│   │   │   │   └── SwitchTenant/
│   │   │   ├── Queries/
│   │   │   │   └── GetCurrentUser/
│   │   │   │       ├── GetCurrentUserQuery.cs
│   │   │   │       └── GetCurrentUserQueryHandler.cs
│   │   │   └── Services/
│   │   │       ├── ITokenService.cs
│   │   │       └── IJwtClaimsBuilder.cs
│   │   ├── Authorization/
│   │   │   ├── Requirements/
│   │   │   │   ├── PermissionRequirement.cs
│   │   │   │   └── TenantResourceRequirement.cs
│   │   │   └── Handlers/
│   │   │       ├── PermissionAuthorizationHandler.cs
│   │   │       └── TenantResourceAuthorizationHandler.cs
│   │   ├── Users/
│   │   │   ├── Commands/
│   │   │   │   ├── CreateUser/
│   │   │   │   ├── UpdateUser/
│   │   │   │   ├── DeleteUser/
│   │   │   │   └── AssignRole/
│   │   │   └── Queries/
│   │   │       ├── GetUsers/
│   │   │       └── GetUserById/
│   │   └── Pipelines/
│   │       ├── Commands/
│   │       └── Queries/
│   │
│   ├── MultiTenantETL.Infrastructure/
│   │   ├── Persistence/
│   │   │   ├── ApplicationDbContext.cs
│   │   │   ├── Configurations/
│   │   │   │   ├── UserConfiguration.cs
│   │   │   │   ├── RoleConfiguration.cs
│   │   │   │   └── UserTenantConfiguration.cs
│   │   │   ├── Interceptors/
│   │   │   │   └── AuditableEntityInterceptor.cs
│   │   │   └── Migrations/
│   │   ├── Identity/
│   │   │   ├── ApplicationUser.cs
│   │   │   ├── ApplicationRole.cs
│   │   │   ├── UserTenant.cs
│   │   │   ├── IdentityService.cs
│   │   │   └── CurrentUserService.cs
│   │   ├── Email/
│   │   │   ├── AzureCommunicationEmailService.cs
│   │   │   └── EmailTemplates.cs
│   │   ├── Security/
│   │   │   ├── TokenService.cs
│   │   │   ├── JwtClaimsBuilder.cs
│   │   │   └── InputSanitizer.cs
│   │   ├── Data/
│   │   │   ├── DbSeeder.cs
│   │   │   └── RoleSeeder.cs
│   │   └── DependencyInjection.cs
│   │
│   └── MultiTenantETL.API/
│       ├── Controllers/
│       │   ├── AuthenticationController.cs
│       │   ├── AccountController.cs
│       │   ├── UsersController.cs
│       │   └── PipelinesController.cs
│       ├── Middleware/
│       │   ├── SecurityHeadersMiddleware.cs
│       │   ├── ExceptionHandlingMiddleware.cs
│       │   └── TenantMiddleware.cs
│       ├── Filters/
│       │   └── ApiExceptionFilterAttribute.cs
│       ├── Extensions/
│       │   └── ServiceCollectionExtensions.cs
│       ├── appsettings.json
│       ├── appsettings.Development.json
│       ├── Program.cs
│       └── DependencyInjection.cs
```

---

## Layer Details

### 1. Domain Layer (MultiTenantETL.Domain)

**No dependencies** - Pure business logic

```csharp
// Entities/Tenant.cs
namespace MultiTenantETL.Domain.Entities
{
    public class Tenant : ITenantResource
    {
        public Guid Id { get; private set; }
        public string Name { get; private set; }
        public string Slug { get; private set; }
        public bool IsActive { get; private set; }
        public DateTime CreatedAt { get; private set; }
        
        // ITenantResource implementation
        public Guid TenantId => Id;
        
        private Tenant() { } // For EF Core
        
        public static Tenant Create(string name, string slug)
        {
            return new Tenant
            {
                Id = Guid.NewGuid(),
                Name = name,
                Slug = slug,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
        }
        
        public void Deactivate() => IsActive = false;
    }
}

// ValueObjects/Email.cs
namespace MultiTenantETL.Domain.ValueObjects
{
    public record Email
    {
        public string Value { get; }
        
        private Email(string value) => Value = value;
        
        public static Result<Email> Create(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return Result<Email>.Failure("Email is required");
            
            if (!IsValidEmail(email))
                return Result<Email>.Failure("Invalid email format");
            
            return Result<Email>.Success(new Email(email));
        }
        
        private static bool IsValidEmail(string email) => 
            new EmailAddressAttribute().IsValid(email);
    }
}

// Enums/AuthErrorCode.cs
namespace MultiTenantETL.Domain.Enums
{
    public enum AuthErrorCode
    {
        InvalidCredentials,
        AccountLocked,
        EmailAlreadyExists,
        // ... rest
    }
}

// Exceptions/UnauthorizedException.cs
namespace MultiTenantETL.Domain.Exceptions
{
    public class UnauthorizedException : Exception
    {
        public UnauthorizedException(string message) : base(message) { }
    }
}

// Interfaces/ITenantResource.cs
namespace MultiTenantETL.Domain.Interfaces
{
    public interface ITenantResource
    {
        Guid TenantId { get; }
    }
}
```

---

### 2. Application Layer (MultiTenantETL.Application)

**Depends on:** Domain only

```csharp
// Common/Interfaces/IApplicationDbContext.cs
namespace MultiTenantETL.Application.Common.Interfaces
{
    public interface IApplicationDbContext
    {
        DbSet<User> Users { get; }
        DbSet<Role> Roles { get; }
        DbSet<UserTenant> UserTenants { get; }
        DbSet<Pipeline> Pipelines { get; }
        
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}

// Common/Interfaces/ICurrentUserService.cs
namespace MultiTenantETL.Application.Common.Interfaces
{
    public interface ICurrentUserService
    {
        Guid GetUserId();
        Guid GetTenantId();
        string GetRole();
        List<string> GetPermissions();
        bool HasPermission(string permission);
    }
}

// Common/Interfaces/IEmailService.cs
namespace MultiTenantETL.Application.Common.Interfaces
{
    public interface IEmailService
    {
        Task SendEmailConfirmationAsync(string email, string firstName, string confirmationUrl);
        Task SendPasswordResetAsync(string email, string firstName, string resetUrl);
        Task SendWelcomeEmailAsync(string email, string firstName);
    }
}

// Common/Models/Result.cs
namespace MultiTenantETL.Application.Common.Models
{
    public class Result
    {
        public bool IsSuccess { get; }
        public string Error { get; }
        
        protected Result(bool isSuccess, string error)
        {
            IsSuccess = isSuccess;
            Error = error;
        }
        
        public static Result Success() => new(true, null);
        public static Result Failure(string error) => new(false, error);
    }
    
    public class Result<T> : Result
    {
        public T Value { get; }
        
        private Result(bool isSuccess, T value, string error) 
            : base(isSuccess, error)
        {
            Value = value;
        }
        
        public static Result<T> Success(T value) => new(true, value, null);
        public static new Result<T> Failure(string error) => new(false, default, error);
    }
}

// Authentication/Commands/Register/RegisterCommand.cs
namespace MultiTenantETL.Application.Authentication.Commands.Register
{
    public record RegisterCommand : IRequest<Result<Guid>>
    {
        public string Email { get; init; }
        public string Password { get; init; }
        public string FirstName { get; init; }
        public string LastName { get; init; }
    }
}

// Authentication/Commands/Register/RegisterCommandHandler.cs
namespace MultiTenantETL.Application.Authentication.Commands.Register
{
    public class RegisterCommandHandler : IRequestHandler<RegisterCommand, Result<Guid>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IEmailService _emailService;
        
        public RegisterCommandHandler(
            IApplicationDbContext context,
            IPasswordHasher passwordHasher,
            IEmailService emailService)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _emailService = emailService;
        }
        
        public async Task<Result<Guid>> Handle(
            RegisterCommand request, 
            CancellationToken cancellationToken)
        {
            // Check if email exists
            var emailExists = await _context.Users
                .AnyAsync(u => u.Email.Value == request.Email, cancellationToken);
            
            if (emailExists)
                return Result<Guid>.Failure("Email already registered");
            
            // Create user
            var emailResult = Email.Create(request.Email);
            if (!emailResult.IsSuccess)
                return Result<Guid>.Failure(emailResult.Error);
            
            var user = User.Create(emailResult.Value, request.FirstName, request.LastName);
            
            _context.Users.Add(user);
            await _context.SaveChangesAsync(cancellationToken);
            
            // Send confirmation email
            await _emailService.SendEmailConfirmationAsync(
                user.Email.Value, 
                user.FirstName, 
                "confirmation-url");
            
            return Result<Guid>.Success(user.Id);
        }
    }
}

// Authentication/Commands/Register/RegisterCommandValidator.cs
namespace MultiTenantETL.Application.Authentication.Commands.Register
{
    public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
    {
        public RegisterCommandValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress();
            
            RuleFor(x => x.Password)
                .NotEmpty()
                .MinimumLength(8)
                .Matches(@"[A-Z]").WithMessage("Password must contain uppercase")
                .Matches(@"[a-z]").WithMessage("Password must contain lowercase")
                .Matches(@"\d").WithMessage("Password must contain digit");
            
            RuleFor(x => x.FirstName)
                .NotEmpty()
                .MinimumLength(2)
                .MaximumLength(50);
        }
    }
}
```

---

### 3. Infrastructure Layer (MultiTenantETL.Infrastructure)

**Depends on:** Domain + Application

```csharp
// Persistence/ApplicationDbContext.cs
namespace MultiTenantETL.Infrastructure.Persistence
{
    public class ApplicationDbContext : DbContext, IApplicationDbContext
    {
        private readonly ICurrentUserService _currentUser;
        
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options,
            ICurrentUserService currentUser) 
            : base(options)
        {
            _currentUser = currentUser;
        }
        
        public DbSet<User> Users => Set<User>();
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<UserTenant> UserTenants => Set<UserTenant>();
        public DbSet<Pipeline> Pipelines => Set<Pipeline>();
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
            
            // Apply tenant filter
            ApplyTenantFilter(modelBuilder);
        }
        
        private void ApplyTenantFilter(ModelBuilder modelBuilder)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(ITenantResource).IsAssignableFrom(entityType.ClrType))
                {
                    var tenantId = _currentUser.GetTenantId();
                    var parameter = Expression.Parameter(entityType.ClrType, "e");
                    var filter = Expression.Lambda(
                        Expression.Equal(
                            Expression.Property(parameter, nameof(ITenantResource.TenantId)),
                            Expression.Constant(tenantId)),
                        parameter);
                    
                    modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
                }
            }
        }
    }
}

// Identity/CurrentUserService.cs
namespace MultiTenantETL.Infrastructure.Identity
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        
        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }
        
        private ClaimsPrincipal User => _httpContextAccessor.HttpContext?.User;
        
        public Guid GetUserId()
        {
            var userIdClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }
        
        // ... other methods
    }
}

// Email/AzureCommunicationEmailService.cs
namespace MultiTenantETL.Infrastructure.Email
{
    public class AzureCommunicationEmailService : IEmailService
    {
        // Implementation from file 2
    }
}

// DependencyInjection.cs
namespace MultiTenantETL.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Database
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));
            
            services.AddScoped<IApplicationDbContext>(provider => 
                provider.GetRequiredService<ApplicationDbContext>());
            
            // Identity
            services.AddScoped<ICurrentUserService, CurrentUserService>();
            
            // Email
            services.AddSingleton<IEmailService, AzureCommunicationEmailService>();
            
            return services;
        }
    }
}
```

---

### 4. API/Presentation Layer (MultiTenantETL.API)

**Depends on:** All layers

```csharp
// Controllers/AuthenticationController.cs
namespace MultiTenantETL.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthenticationController : ControllerBase
    {
        private readonly ISender _mediator;
        
        public AuthenticationController(ISender mediator)
        {
            _mediator = mediator;
        }
        
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterCommand command)
        {
            var result = await _mediator.Send(command);
            
            if (!result.IsSuccess)
                return BadRequest(new { error = result.Error });
            
            return Ok(new { userId = result.Value });
        }
    }
}

// Program.cs
namespace MultiTenantETL.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            
            // Add layers
            builder.Services.AddApplication();
            builder.Services.AddInfrastructure(builder.Configuration);
            builder.Services.AddPresentation();
            
            var app = builder.Build();
            
            app.UseHttpsRedirection();
            app.UseCors("AllowFrontend");
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            
            app.Run();
        }
    }
}
```

---

## Dependency Injection Per Layer

### Application Layer

```csharp
// Application/DependencyInjection.cs
namespace MultiTenantETL.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            // MediatR
            services.AddMediatR(cfg => 
                cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
            
            // FluentValidation
            services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
            
            // Behaviors
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            
            return services;
        }
    }
}
```

### Infrastructure Layer

See above - adds DbContext, Identity, Email, etc.

### API Layer

```csharp
// API/DependencyInjection.cs
namespace MultiTenantETL.API
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddPresentation(this IServiceCollection services)
        {
            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();
            
            // CORS
            services.AddCors(options =>
            {
                options.AddPolicy("AllowFrontend", policy =>
                {
                    policy.WithOrigins("http://localhost:5173")
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
                });
            });
            
            return services;
        }
    }
}
```

---

## Benefits

✅ **Testability** - Easy to unit test each layer
✅ **Maintainability** - Clear separation of concerns
✅ **Flexibility** - Easy to swap implementations (e.g., different email provider)
✅ **Independence** - Domain doesn't depend on frameworks
✅ **Clean Dependencies** - Dependencies point inward only

---

See specific implementation files for code details:
- [1-setup.md](./1-setup.md) - Models & DbContext
- [2-email-service.md](./2-email-service.md) - Email implementation
- [3-controllers.md](./3-controllers.md) - Controllers & DTOs
- [4-roles-claims.md](./4-roles-claims.md) - Roles & permissions
- [5-security.md](./5-security.md) - Security features
- [6-authorization.md](./6-authorization.md) - Authorization handlers
