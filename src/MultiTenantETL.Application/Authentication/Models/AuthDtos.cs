using System.ComponentModel.DataAnnotations;

namespace MultiTenantETL.Application.Authentication.Models
{
    public class RegisterRequest
    {
        public required string Email { get; set; }

        public required string Password { get; set; }

        public required string FirstName { get; set; }

        public required string LastName { get; set; }
    }

    public class ConfirmEmailRequest
    {
        public Guid UserId { get; set; }

        public required string Token { get; set; }
    }

    public class ForgotPasswordRequest
    {
        public required string Email { get; set; }
    }

    public class ResetPasswordRequest
    {
        public Guid UserId { get; set; }

        public required string Token { get; set; }

        public required string NewPassword { get; set; }
    }

    public class ChangePasswordRequest
    {
        public required string CurrentPassword { get; set; }

        public required string NewPassword { get; set; }

        public required string ConfirmPassword { get; set; }
    }

    public class SwitchTenantRequest
    {
        public Guid TenantId { get; set; }
    }
}
