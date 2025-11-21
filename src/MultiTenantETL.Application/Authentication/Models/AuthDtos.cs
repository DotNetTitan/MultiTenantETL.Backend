using System.ComponentModel.DataAnnotations;

namespace MultiTenantETL.Application.Authentication.Models
{
    public class RegisterRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; }

        [Required, MinLength(8)]
        public string Password { get; set; }

        [Required, MinLength(2), MaxLength(50)]
        public string FirstName { get; set; }

        [Required, MinLength(2), MaxLength(50)]
        public string LastName { get; set; }
    }

    public class ConfirmEmailRequest
    {
        [Required]
        public Guid UserId { get; set; }

        [Required]
        public string Token { get; set; }
    }

    public class ForgotPasswordRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; }
    }

    public class ResetPasswordRequest
    {
        [Required]
        public Guid UserId { get; set; }

        [Required]
        public string Token { get; set; }

        [Required, MinLength(8)]
        public string NewPassword { get; set; }
    }

    public class ChangePasswordRequest
    {
        [Required]
        public string CurrentPassword { get; set; }

        [Required, MinLength(8)]
        public string NewPassword { get; set; }

        [Required, Compare(nameof(NewPassword), ErrorMessage = "The new password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }
    }

    public class SwitchTenantRequest
    {
        [Required]
        public Guid TenantId { get; set; }
    }
}
