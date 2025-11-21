namespace MultiTenantETL.Application.Interfaces
{
    /// <summary>
    /// Email service interface for sending authentication-related emails
    /// </summary>
    public interface IEmailService
    {
        /// <summary>
        /// Send email confirmation link to newly registered user
        /// </summary>
        Task SendEmailConfirmationAsync(string email, string firstName, string confirmationUrl);

        /// <summary>
        /// Send welcome email after successful email confirmation
        /// </summary>
        Task SendWelcomeEmailAsync(string email, string firstName);

        /// <summary>
        /// Send password reset link to user
        /// </summary>
        Task SendPasswordResetAsync(string email, string firstName, string resetUrl);

        /// <summary>
        /// Send notification that password was changed
        /// </summary>
        Task SendPasswordChangedNotificationAsync(string email, string firstName);
    }
}
