using System.ComponentModel.DataAnnotations;

namespace MultiTenantETL.Application.Authentication.Models
{
    public class RegisterRequest
    {
        [Required, EmailAddress]
        public required string Email { get; set; }

        [Required, MinLength(8)]
        public required string Password { get; set; }

        [Required, MinLength(2), MaxLength(50)]
        public required string FirstName { get; set; }

        [Required, MinLength(2), MaxLength(50)]
        public required string LastName { get; set; }
    }

    public class ConfirmEmailRequest
    {
        [Required]
        public Guid UserId { get; set; }

        [Required]
        public required string Token { get; set; }
    }

    public class ForgotPasswordRequest
    {
        [Required, EmailAddress]
        public required string Email { get; set; }
    }

    public class ResetPasswordRequest
    {
        [Required]
        public Guid UserId { get; set; }

        [Required]
        public required string Token { get; set; }

        [Required, MinLength(8)]
        public required string NewPassword { get; set; }
    }

    public class ChangePasswordRequest
    {
        [Required]
        public required string CurrentPassword { get; set; }

        [Required, MinLength(8)]
        public required string NewPassword { get; set; }

        [Required, Compare(nameof(NewPassword), ErrorMessage = "The new password and confirmation password do not match.")]
        public required string ConfirmPassword { get; set; }
    }

    public class SwitchTenantRequest
    {
        [Required]
        public Guid TenantId { get; set; }
    }
}
