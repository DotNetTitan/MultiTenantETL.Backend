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