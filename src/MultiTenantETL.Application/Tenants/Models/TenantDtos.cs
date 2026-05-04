namespace MultiTenantETL.Application.Tenants.Models;

public record CreateTenantRequest
{
    public required string Name { get; init; }

    public required string Slug { get; init; }
}

public record UpdateTenantRequest
{
    public required string Name { get; init; }

    public bool? IsActive { get; init; }
}

public record TenantResponse
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record UserTenantResponse
{
    public Guid TenantId { get; init; }
    public required string TenantName { get; init; }
    public required string TenantSlug { get; init; }
    public required string RoleCode { get; init; }
    public bool IsActive { get; init; }
    public bool IsCurrent { get; init; }
}

public record AddUserToTenantRequest
{
    public Guid UserId { get; init; }

    public Guid TenantId { get; init; }

    public required string RoleCode { get; init; }
}

public record UpdateUserTenantRoleRequest
{
    public required string RoleCode { get; init; }
}

public record TenantUserResponse
{
    public Guid UserId { get; init; }
    public required string Email { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string RoleCode { get; init; }
    public bool IsActive { get; init; }
}
