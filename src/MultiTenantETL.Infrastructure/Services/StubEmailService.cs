using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Interfaces;

namespace MultiTenantETL.Infrastructure.Services
{
    /// <summary>
    /// Stub implementation of IEmailService for development and testing.
    /// For production, use AzureCommunicationEmailService.
    /// </summary>
    public class StubEmailService : IEmailService
    {
        private readonly ILogger<StubEmailService> _logger;

        public StubEmailService(ILogger<StubEmailService> logger)
        {
            _logger = logger;
        }

        public Task SendEmailConfirmationAsync(string email, string firstName, string confirmationUrl)
        {
            _logger.LogInformation("📧 [STUB] Email Confirmation would be sent to {Email}", email);
            _logger.LogInformation("   Name: {FirstName}", firstName);
            _logger.LogInformation("   Confirmation URL: {Url}", confirmationUrl);
            return Task.CompletedTask;
        }

        public Task SendWelcomeEmailAsync(string email, string firstName)
        {
            _logger.LogInformation("📧 [STUB] Welcome Email would be sent to {Email}", email);
            _logger.LogInformation("   Name: {FirstName}", firstName);
            return Task.CompletedTask;
        }

        public Task SendPasswordResetAsync(string email, string firstName, string resetUrl)
        {
            _logger.LogInformation("📧 [STUB] Password Reset Email would be sent to {Email}", email);
            _logger.LogInformation("   Name: {FirstName}", firstName);
            _logger.LogInformation("   Reset URL: {Url}", resetUrl);
            return Task.CompletedTask;
        }

        public Task SendPasswordChangedNotificationAsync(string email, string firstName)
        {
            _logger.LogInformation("📧 [STUB] Password Changed Notification would be sent to {Email}", email);
            _logger.LogInformation("   Name: {FirstName}", firstName);
            return Task.CompletedTask;
        }
        
        public Task SendPipelineExecutionReportAsync(
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
            _logger.LogInformation("📧 [STUB] Pipeline Execution Report would be sent to {Email}", recipientEmail);
            _logger.LogInformation("   Pipeline: {PipelineName}", pipelineName);
            _logger.LogInformation("   Execution ID: {ExecutionId}", executionId);
            _logger.LogInformation("   Status: {Status}", executionStatus);
            _logger.LogInformation("   Records: {Processed} processed, {Succeeded} succeeded, {Failed} failed", 
                recordsProcessed, recordsSucceeded, recordsFailed);
            _logger.LogInformation("   Details URL: {Url}", executionDetailsUrl);
            return Task.CompletedTask;
        }
    }
}
