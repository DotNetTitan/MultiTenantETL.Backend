using System.ComponentModel.DataAnnotations;

namespace MultiTenantETL.Application.Tenants.Models;

public record CreateTenantRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public required string Name { get; init; }

    [Required]
    [StringLength(50, MinimumLength = 2)]
    [RegularExpression(@"^[a-z0-9-]+$", ErrorMessage = "Slug must contain only lowercase letters, numbers, and hyphens")]
    public required string Slug { get; init; }
}

public record UpdateTenantRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
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
    [Required]
    public Guid UserId { get; init; }

    [Required]
    public Guid TenantId { get; init; }

    [Required]
    [StringLength(50)]
    public required string RoleCode { get; init; }
}

public record UpdateUserTenantRoleRequest
{
    [Required]
    [StringLength(50)]
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
