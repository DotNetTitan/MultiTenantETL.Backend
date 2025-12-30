using FluentValidation;
using MultiTenantETL.Application.Tenants.Models;

namespace MultiTenantETL.Application.Tenants.Validators;

public class CreateTenantRequestValidator : AbstractValidator<CreateTenantRequest>
{
    public CreateTenantRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tenant Name is required")
            .MinimumLength(2).WithMessage("Tenant Name must be at least 2 characters")
            .MaximumLength(100).WithMessage("Tenant Name must not exceed 100 characters");

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Slug is required")
            .MinimumLength(2).WithMessage("Slug must be at least 2 characters")
            .MaximumLength(50).WithMessage("Slug must not exceed 50 characters")
            .Matches(@"^[a-z0-9-]+$").WithMessage("Slug must contain only lowercase letters, numbers, and hyphens");
    }
}

public class UpdateTenantRequestValidator : AbstractValidator<UpdateTenantRequest>
{
    public UpdateTenantRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tenant Name is required")
            .MinimumLength(2).WithMessage("Tenant Name must be at least 2 characters")
            .MaximumLength(100).WithMessage("Tenant Name must not exceed 100 characters");
    }
}

public class AddUserToTenantRequestValidator : AbstractValidator<AddUserToTenantRequest>
{
    public AddUserToTenantRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");

        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("Tenant ID is required");

        RuleFor(x => x.RoleCode)
            .NotEmpty().WithMessage("Role Code is required")
            .MaximumLength(50).WithMessage("Role Code must not exceed 50 characters");
    }
}
