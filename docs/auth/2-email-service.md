# 2. Email Service

**Purpose:** Azure Communication Services integration and email templates

---

## Email Service Interface

```csharp
// Application/Common/Interfaces/IEmailService.cs
public interface IEmailService
{
    Task SendEmailConfirmationAsync(string email, string firstName, string confirmationUrl);
    Task SendPasswordResetAsync(string email, string firstName, string resetUrl);
    Task SendWelcomeEmailAsync(string email, string firstName);
    Task SendPasswordChangedNotificationAsync(string email, string firstName);
}
```

---

## Azure Communication Services Implementation

```csharp
// Infrastructure/Email/AzureCommunicationEmailService.cs
using Azure;
using Azure.Communication.Email;

public class AzureCommunicationEmailService : IEmailService
{
    private readonly EmailClient _emailClient;
    private readonly string _fromEmail;
    private readonly ILogger<AzureCommunicationEmailService> _logger;
    private readonly IWebHostEnvironment _env;

    public AzureCommunicationEmailService(
        IConfiguration configuration,
        ILogger<AzureCommunicationEmailService> logger,
        IWebHostEnvironment env)
    {
        var connectionString = configuration["AzureCommunicationServices:ConnectionString"];
        _fromEmail = configuration["AzureCommunicationServices:FromEmail"];
        _emailClient = new EmailClient(connectionString);
        _logger = logger;
        _env = env;
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
        var htmlContent = EmailTemplates.GetWelcome(firstName);
        await SendEmailAsync(email, subject, htmlContent);
    }

    public async Task SendPasswordChangedNotificationAsync(string email, string firstName)
    {
        var subject = "Password Changed - MultiTenant ETL";
        var htmlContent = EmailTemplates.GetPasswordChanged(firstName);
        await SendEmailAsync(email, subject, htmlContent);
    }

    private async Task SendEmailAsync(string toEmail, string subject, string htmlContent)
    {
        try
        {
            // In development, log instead of sending
            if (_env.IsDevelopment())
            {
                _logger.LogInformation(
                    "EMAIL [DEV MODE]\nTo: {Email}\nSubject: {Subject}\nContent:\n{Content}",
                    toEmail, subject, htmlContent);
                return;
            }

            var emailMessage = new EmailMessage(
                senderAddress: _fromEmail,
                content: new EmailContent(subject)
                {
                    Html = htmlContent
                },
                recipients: new EmailRecipients(new List<EmailAddress>
                {
                    new EmailAddress(toEmail)
                }));

            EmailSendOperation emailSendOperation = await _emailClient.SendAsync(
                WaitUntil.Started,
                emailMessage);

            _logger.LogInformation(
                "Email sent successfully. MessageId: {MessageId}, To: {Email}",
                emailSendOperation.Id, toEmail);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, 
                "Azure Communication Services failed to send email to {Email}. Status: {Status}",
                toEmail, ex.Status);
            throw new Exception("Failed to send email", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending email to {Email}", toEmail);
            throw;
        }
    }
}
```

---

## Email Templates

```csharp
// Infrastructure/Email/EmailTemplates.cs
public static class EmailTemplates
{
    private static string GetBaseTemplate(string content)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{ 
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; 
            line-height: 1.6; 
            color: #333; 
            background-color: #f4f4f4; 
            margin: 0; 
            padding: 0; 
        }}
        .container {{ 
            max-width: 600px; 
            margin: 20px auto; 
            background: white; 
            border-radius: 8px; 
            overflow: hidden; 
            box-shadow: 0 2px 4px rgba(0,0,0,0.1); 
        }}
        .header {{ 
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); 
            color: white; 
            padding: 30px; 
            text-align: center; 
        }}
        .header h1 {{ margin: 0; font-size: 24px; }}
        .content {{ padding: 30px; }}
        .button {{ 
            display: inline-block; 
            padding: 12px 30px; 
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); 
            color: white; 
            text-decoration: none; 
            border-radius: 5px; 
            margin: 20px 0; 
            font-weight: bold; 
        }}
        .footer {{ 
            background: #f8f9fa; 
            padding: 20px; 
            text-align: center; 
            font-size: 12px; 
            color: #666; 
        }}
        .security-note {{ 
            background: #fff3cd; 
            border-left: 4px solid #ffc107; 
            padding: 15px; 
            margin: 20px 0; 
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'><h1>MultiTenant ETL</h1></div>
        <div class='content'>{content}</div>
        <div class='footer'>
            <p>&copy; 2025 MultiTenant ETL. All rights reserved.</p>
            <p>If you didn't request this email, please ignore it.</p>
        </div>
    </div>
</body>
</html>";
    }

    public static string GetEmailConfirmation(string firstName, string confirmationUrl)
    {
        var content = $@"
            <h2>Hi {firstName},</h2>
            <p>Thank you for registering with MultiTenant ETL!</p>
            <p>Please confirm your email address by clicking the button below:</p>
            <p style='text-align: center;'>
                <a href='{confirmationUrl}' class='button'>Confirm Email Address</a>
            </p>
            <div class='security-note'>
                <strong>Security Note:</strong> This link will expire in 24 hours.
            </div>
            <p>If the button doesn't work, copy and paste this link:</p>
            <p style='word-break: break-all; color: #667eea;'>{confirmationUrl}</p>";
        
        return GetBaseTemplate(content);
    }

    public static string GetPasswordReset(string firstName, string resetUrl)
    {
        var content = $@"
            <h2>Hi {firstName},</h2>
            <p>We received a request to reset your password.</p>
            <p>Click the button below to reset your password:</p>
            <p style='text-align: center;'>
                <a href='{resetUrl}' class='button'>Reset Password</a>
            </p>
            <div class='security-note'>
                <strong>Security Note:</strong> This link will expire in 1 hour. 
                If you didn't request a password reset, please ignore this email.
            </div>
            <p>If the button doesn't work, copy and paste this link:</p>
            <p style='word-break: break-all; color: #667eea;'>{resetUrl}</p>";
        
        return GetBaseTemplate(content);
    }

    public static string GetWelcome(string firstName)
    {
        var content = $@"
            <h2>Welcome, {firstName}! 🎉</h2>
            <p>Your email has been confirmed and your account is now active.</p>
            <p>You can now:</p>
            <ul>
                <li>Create and manage data connectors</li>
                <li>Build ETL pipelines</li>
                <li>Transform and map your data</li>
                <li>Schedule automated data flows</li>
            </ul>
            <p style='text-align: center;'>
                <a href='https://yourapp.com/login' class='button'>Get Started</a>
            </p>";
        
        return GetBaseTemplate(content);
    }

    public static string GetPasswordChanged(string firstName)
    {
        var content = $@"
            <h2>Hi {firstName},</h2>
            <p>Your password has been successfully changed.</p>
            <div class='security-note'>
                <strong>Security Alert:</strong> If you didn't make this change, 
                please contact support immediately.
            </div>
            <p>Changed at: <strong>{DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC</strong></p>";
        
        return GetBaseTemplate(content);
    }
}
```

---

## Service Registration

Add to Program.cs:

```csharp
// Register email service
builder.Services.AddSingleton<IEmailService, AzureCommunicationEmailService>();
```

---

## Testing Email Service

### Development Testing

Emails in development mode are logged to console instead of being sent:

```
info: AzureCommunicationEmailService[0]
      EMAIL [DEV MODE]
      To: test@example.com
      Subject: Confirm Your Email - MultiTenant ETL
      Content:
      <!DOCTYPE html>...
```

### Production Testing

Once you have Azure Communication Services configured:

```csharp
// Test controller
[HttpPost("test-email")]
public async Task<IActionResult> TestEmail([FromBody] string email)
{
    await _emailService.SendWelcomeEmailAsync(email, "Test User");
    return Ok(new { message = "Email sent" });
}
```

---

## Azure Communication Services Setup

### 1. Create Resource

```bash
# Azure CLI
az communication create \
  --name "multitenant-etl-emails" \
  --resource-group "your-resource-group" \
  --data-location "United States"
```

### 2. Get Connection String

```bash
az communication list-key \
  --name "multitenant-etl-emails" \
  --resource-group "your-resource-group"
```

### 3. Configure Email Domain

1. Go to Azure Portal > Communication Services
2. Navigate to "Email" > "Domains"
3. Add your domain or use Azure-managed domain
4. Verify domain ownership (if custom domain)

### 4. Update appsettings.json

```json
{
  "AzureCommunicationServices": {
    "ConnectionString": "endpoint=https://your-resource.communication.azure.com/;accesskey=YOUR_KEY",
    "FromEmail": "DoNotReply@yourdomain.com"
  }
}
```

---

## Checklist

- [ ] Email service interface created
- [ ] Azure Communication Services implementation complete
- [ ] Email templates created
- [ ] Service registered in Program.cs
- [ ] Azure Communication Services resource created
- [ ] Connection string configured
- [ ] Email domain verified
- [ ] Tested email sending in development (logs)
- [ ] Tested email sending in production (actual delivery)

---

**Next:** [3. Controllers](./3-controllers.md)
