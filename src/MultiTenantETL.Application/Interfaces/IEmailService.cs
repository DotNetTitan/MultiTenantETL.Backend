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

        /// <summary>
        /// Send pipeline execution report to specified email address
        /// </summary>
        Task SendPipelineExecutionReportAsync(
            string recipientEmail,
            string pipelineName,
            string executionId,
            string executionStatus,
            DateTimeOffset startTime,
            DateTimeOffset? endTime,
            TimeSpan? duration,
            long recordsProcessed,
            long recordsSucceeded,
            long recordsFailed,
            string? errorMessage,
            string executionDetailsUrl);

        /// <summary>
        /// Send data export email with file attachment. Used by the Email connector destination.
        /// Exactly one email is sent per pipeline execution containing all exported data as a file attachment.
        /// </summary>
        Task<bool> SendDataExportEmailAsync(
            List<string> recipients,
            List<string>? ccRecipients,
            string subject,
            string htmlBody,
            string attachmentFileName,
            string attachmentMediaType,
            byte[] attachmentContent);
    }
}
