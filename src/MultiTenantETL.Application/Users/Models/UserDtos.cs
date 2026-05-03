using System.ComponentModel.DataAnnotations;

namespace MultiTenantETL.Application.Users.Models;

public record UserResponse
{
    public Guid Id { get; init; }
    public required string Email { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public bool IsActive { get; init; }
    public bool EmailConfirmed { get; init; }
    public DateTime CreatedAt { get; init; }
    public Guid? CurrentTenantId { get; init; }
    public string? CurrentTenantName { get; init; }
    public List<string> Roles { get; init; } = new();
}

public record UserDetailResponse
{
    public Guid Id { get; init; }
    public required string Email { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public bool IsActive { get; init; }
    public bool EmailConfirmed { get; init; }
    public DateTime CreatedAt { get; init; }
    public Guid? CurrentTenantId { get; init; }
    public string? CurrentTenantName { get; init; }
    public List<UserTenantInfo> Tenants { get; init; } = new();
    public List<string> Roles { get; init; } = new();
}

public record UserTenantInfo
{
    public Guid TenantId { get; init; }
    public required string TenantName { get; init; }
    public required string RoleCode { get; init; }
    public bool IsActive { get; init; }
}

public record UpdateUserRequest
{
    public required string FirstName { get; init; }

    public required string LastName { get; init; }

    public required string Email { get; init; }
}

public record UpdateUserStatusRequest
{
    public bool IsActive { get; init; }
}

public record AssignRoleRequest
{
    public required string RoleName { get; init; }
}

public record RemoveRoleRequest
{
    public required string RoleName { get; init; }
}

public record AdminPasswordResetRequest
{
    [Required]
    [StringLength(100, MinimumLength = 8)]
    public required string NewPassword { get; init; }
}

public record UserSearchRequest
{
    public string? Email { get; init; }
    public string? Name { get; init; }
    public bool? IsActive { get; init; }
    public Guid? TenantId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public record PagedUserResponse
{
    public List<UserResponse> Users { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}
