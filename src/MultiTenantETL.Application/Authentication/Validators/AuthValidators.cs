using FluentValidation;
using MultiTenantETL.Application.Authentication.Models;

namespace MultiTenantETL.Application.Authentication.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Please enter a valid email address");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First Name is required")
            .MinimumLength(2).WithMessage("First Name must be at least 2 characters")
            .MaximumLength(50).WithMessage("First Name must not exceed 50 characters");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last Name is required")
            .MinimumLength(2).WithMessage("Last Name must be at least 2 characters")
            .MaximumLength(50).WithMessage("Last Name must not exceed 50 characters");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(8).WithMessage("Must be at least 8 characters")
            .Matches("[A-Z]").WithMessage("Must contain an uppercase letter")
            .Matches("[a-z]").WithMessage("Must contain a lowercase letter")
            .Matches("[0-9]").WithMessage("Must contain a digit")
            .Matches("[^a-zA-Z0-9]").WithMessage("Must contain a special character");
    }
}

public class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Please enter a valid email address");
    }
}

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");

        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Reset token is required");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required")
            .MinimumLength(8).WithMessage("Must be at least 8 characters")
            .Matches("[A-Z]").WithMessage("Must contain an uppercase letter")
            .Matches("[a-z]").WithMessage("Must contain a lowercase letter")
            .Matches("[0-9]").WithMessage("Must contain a digit")
            .Matches("[^a-zA-Z0-9]").WithMessage("Must contain a special character");
    }
}

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Current password is required");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required")
            .MinimumLength(8).WithMessage("Must be at least 8 characters")
            .Matches("[A-Z]").WithMessage("Must contain an uppercase letter")
            .Matches("[a-z]").WithMessage("Must contain a lowercase letter")
            .Matches("[0-9]").WithMessage("Must contain a digit")
            .Matches("[^a-zA-Z0-9]").WithMessage("Must contain a special character");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("Please confirm your new password")
            .Equal(x => x.NewPassword).WithMessage("Passwords do not match");
    }
}
