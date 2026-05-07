using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Interfaces;
using MultiTenantETL.Infrastructure.Configuration;

namespace MultiTenantETL.Infrastructure.Services
{
    /// <summary>
    /// Production implementation of IEmailService using Azure Communication Services
    /// </summary>
    public class AzureCommunicationEmailService : IEmailService
    {
        private readonly EmailClient _emailClient;
        private readonly string _senderAddress;
        private readonly string _frontendUrl;
        private readonly ILogger<AzureCommunicationEmailService> _logger;

        public AzureCommunicationEmailService(
            IOptions<AzureCommunicationSettings> settings,
            IConfiguration configuration,
            ILogger<AzureCommunicationEmailService> logger)
        {
            _logger = logger;

            if (settings.Value.ConnectionString == null)
            {
                throw new ArgumentNullException(nameof(settings.Value.ConnectionString),
                    "Azure Communication Services connection string is not configured");
            }

            if (settings.Value.SenderEmailAddress == null)
            {
                throw new ArgumentNullException(nameof(settings.Value.SenderEmailAddress),
                    "Sender email address is not configured");
            }

            _emailClient = new EmailClient(settings.Value.ConnectionString);
            _senderAddress = settings.Value.SenderEmailAddress;
            _frontendUrl = configuration["AppSettings:FrontendUrl"]
                ?? throw new InvalidOperationException("AppSettings:FrontendUrl is not configured.");
        }

        public async Task SendEmailConfirmationAsync(string email, string firstName, string confirmationUrl)
        {
            var subject = "Confirm Your Email - MultiTenant ETL";
            var htmlContent = EmailTemplates.GetEmailConfirmation(firstName, confirmationUrl);
            await SendEmailAsync(email, subject, htmlContent);
        }

        public async Task SendPasswordResetAsync(string email, string firstName, string resetUrl)
        {
            var subject = "Reset Your Password - MultiTenant ETL";
            var htmlContent = EmailTemplates.GetPasswordReset(firstName, resetUrl);
            await SendEmailAsync(email, subject, htmlContent);
        }

        public async Task SendWelcomeEmailAsync(string email, string firstName)
        {
            var subject = "Welcome to MultiTenant ETL!";
            var htmlContent = EmailTemplates.GetWelcome(firstName, _frontendUrl);
            await SendEmailAsync(email, subject, htmlContent);
        }

        public async Task SendPasswordChangedNotificationAsync(string email, string firstName)
        {
            var subject = "Password Changed - MultiTenant ETL";
            var htmlContent = EmailTemplates.GetPasswordChanged(firstName);
            await SendEmailAsync(email, subject, htmlContent);
        }

        public async Task SendRoleChangedNotificationAsync(string email, string firstName, string oldRole, string newRole, string changedBy)
        {
            var subject = "Your Role Has Been Changed - MultiTenant ETL";
            var htmlContent = EmailTemplates.GetRoleChanged(firstName, oldRole, newRole, changedBy);
            await SendEmailAsync(email, subject, htmlContent);
        }

        public async Task SendTenantChangedNotificationAsync(string email, string firstName, string oldTenant, string newTenant, string changedBy)
        {
            var subject = "Your Tenant Has Been Changed - MultiTenant ETL";
            var htmlContent = EmailTemplates.GetTenantChanged(firstName, oldTenant, newTenant, changedBy);
            await SendEmailAsync(email, subject, htmlContent);
        }

        public async Task SendPipelineExecutionReportAsync(
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
            string executionDetailsUrl)
        {
            var subject = $"Pipeline Execution Report: {pipelineName} - {executionStatus}";
            var htmlContent = EmailTemplates.GetPipelineExecutionReport(
                pipelineName,
                executionId,
                executionStatus,
                startTime,
                endTime,
                duration,
                recordsProcessed,
                recordsSucceeded,
                recordsFailed,
                errorMessage,
                executionDetailsUrl);
            await SendEmailAsync(recipientEmail, subject, htmlContent);
        }

        /// <inheritdoc/>
        public async Task<bool> SendDataExportEmailAsync(
            List<string> recipients,
            List<string>? ccRecipients,
            string subject,
            string htmlBody,
            string attachmentFileName,
            string attachmentMediaType,
            byte[] attachmentContent)
        {
            try
            {
                if (recipients == null || recipients.Count == 0)
                {
                    _logger.LogError("Cannot send data export email: no recipients specified");
                    return false;
                }

                var toRecipients = recipients
                    .Select(r => new EmailAddress(r))
                    .ToList();

                var ccList = ccRecipients?
                    .Where(cc => !string.IsNullOrWhiteSpace(cc))
                    .Select(cc => new EmailAddress(cc))
                    .ToList() ?? new List<EmailAddress>();

                var emailRecipients = new EmailRecipients(toRecipients, ccList);

                var emailMessage = new EmailMessage(
                    senderAddress: _senderAddress,
                    recipients: emailRecipients,
                    content: new EmailContent(subject)
                    {
                        Html = htmlBody
                    }
                );

                var attachment = new EmailAttachment(
                    attachmentFileName,
                    attachmentMediaType,
                    new BinaryData(attachmentContent));
                emailMessage.Attachments.Add(attachment);

                await _emailClient.SendAsync(WaitUntil.Started, emailMessage);

                _logger.LogInformation(
                    "Data export email sent to {RecipientCount} recipients with attachment '{FileName}' ({Size} bytes)",
                    recipients.Count, attachmentFileName, attachmentContent.Length);
                return true;
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Azure Communication Services error sending data export email: {ErrorCode} - {Message}",
                    ex.ErrorCode, ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send data export email to {RecipientCount} recipients", recipients.Count);
                return false;
            }
        }

        private async Task<bool> SendEmailAsync(string to, string subject, string htmlContent)
        {
            try
            {
                if (string.IsNullOrEmpty(to))
                {
                    _logger.LogError("Cannot send email: recipient email address is null or empty");
                    return false;
                }

                var emailMessage = new EmailMessage(
                    senderAddress: _senderAddress,
                    recipients: new EmailRecipients(new List<EmailAddress> { new EmailAddress(to) }),
                    content: new EmailContent(subject)
                    {
                        Html = htmlContent
                    }
                );

                await _emailClient.SendAsync(WaitUntil.Started, emailMessage);

                _logger.LogInformation("Email sent successfully to {Recipient} with subject '{Subject}'", to, subject);
                return true;
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Azure Communication Services error: {ErrorCode} - {Message}",
                    ex.ErrorCode, ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email} with subject '{Subject}'", to, subject);
                return false;
            }
        }
    }
}