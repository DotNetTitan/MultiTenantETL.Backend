namespace MultiTenantETL.Infrastructure.Services
{
    /// <summary>
    /// HTML email templates for authentication-related emails
    /// </summary>
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
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}
        body {{ 
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
            line-height: 1.6; 
            color: #2d3748; 
            background-color: #f5f7fa;
            margin: 0; 
            padding: 40px 20px; 
        }}
        .container {{ 
            max-width: 600px; 
            margin: 0 auto; 
            background: white; 
            border-radius: 12px; 
            overflow: hidden; 
            box-shadow: 0 4px 20px rgba(0,0,0,0.08);
            border: 1px solid #e2e8f0;
        }}
        .header {{ 
            background: linear-gradient(135deg, #2563eb 0%, #1e40af 100%); 
            color: white; 
            padding: 40px 30px; 
            text-align: center; 
        }}
        .header h1 {{ 
            margin: 0; 
            font-size: 28px; 
            font-weight: 700;
            letter-spacing: -0.5px;
        }}
        .header p {{
            margin: 8px 0 0 0;
            font-size: 14px;
            opacity: 0.95;
            font-weight: 400;
        }}
        .content {{ 
            padding: 40px 30px; 
            background: white;
        }}
        .content h2 {{
            color: #1a202c;
            font-size: 22px;
            margin-bottom: 16px;
            font-weight: 600;
        }}
        .content p {{
            color: #4a5568;
            font-size: 15px;
            margin-bottom: 16px;
            line-height: 1.7;
        }}
        .content ul {{
            color: #4a5568;
            font-size: 15px;
            margin: 20px 0;
            padding-left: 24px;
        }}
        .content li {{
            margin-bottom: 10px;
            line-height: 1.6;
        }}
        .button-wrapper {{
            text-align: center;
            margin: 32px 0;
        }}
        .button {{ 
            display: inline-block; 
            padding: 14px 32px; 
            background: #2563eb;
            color: white !important; 
            text-decoration: none; 
            border-radius: 6px; 
            font-weight: 600; 
            font-size: 15px;
            box-shadow: 0 2px 8px rgba(37, 99, 235, 0.3);
            transition: all 0.2s;
        }}
        .button:hover {{
            background: #1e40af;
            box-shadow: 0 4px 12px rgba(37, 99, 235, 0.4);
        }}
        .security-note {{ 
            background: #fef3c7;
            border-left: 4px solid #f59e0b; 
            padding: 16px 20px; 
            margin: 24px 0; 
            border-radius: 6px;
            font-size: 14px;
            color: #78350f;
        }}
        .security-note strong {{
            color: #78350f;
            display: block;
            margin-bottom: 4px;
            font-weight: 600;
        }}
        .success-note {{
            background: #d1fae5;
            border-left: 4px solid #10b981;
            padding: 16px 20px;
            margin: 24px 0;
            border-radius: 6px;
            font-size: 14px;
            color: #065f46;
        }}
        .success-note strong {{
            color: #065f46;
            display: block;
            margin-bottom: 4px;
            font-weight: 600;
        }}
        .link-box {{
            background: #f8fafc;
            padding: 16px;
            border-radius: 6px;
            margin: 20px 0;
            border: 1px solid #e2e8f0;
        }}
        .link-box p {{
            margin: 0;
            font-size: 13px;
            color: #64748b;
        }}
        .link-text {{
            color: #2563eb !important;
            word-break: break-all;
            font-size: 12px;
            font-family: 'Courier New', monospace;
            background: white;
            padding: 8px;
            border-radius: 4px;
            border: 1px solid #e2e8f0;
            display: block;
            margin-top: 8px;
        }}
        .footer {{ 
            background: #f8fafc; 
            padding: 30px; 
            text-align: center; 
            font-size: 13px; 
            color: #64748b; 
            border-top: 1px solid #e2e8f0;
        }}
        .footer p {{
            margin: 6px 0;
        }}
        .footer a {{
            color: #2563eb;
            text-decoration: none;
        }}
        .divider {{
            height: 1px;
            background: #e2e8f0;
            margin: 24px 0;
        }}
        @media only screen and (max-width: 600px) {{
            body {{
                padding: 20px 10px;
            }}
            .container {{
                border-radius: 8px;
            }}
            .content, .header, .footer {{
                padding-left: 20px;
                padding-right: 20px;
            }}
            .button {{
                display: block;
                width: 100%;
            }}
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>MultiTenant ETL</h1>
            <p>Your Complete Data Integration Platform</p>
        </div>
        <div class='content'>{content}</div>
        <div class='footer'>
            <p><strong>© 2025 MultiTenant ETL</strong> · All rights reserved</p>
            <p>If you didn't request this email, please ignore it or <a href='mailto:support@multitenanteti.com'>contact support</a></p>
        </div>
    </div>
</body>
</html>";
        }

        /// <summary>
        /// Email confirmation template with link to verify email address
        /// </summary>
        public static string GetEmailConfirmation(string firstName, string confirmationUrl)
        {
            var content = $@"
            <h2>Hi {firstName},</h2>
            <p>Thank you for registering with <strong>MultiTenant ETL</strong>. We're excited to have you on board.</p>
            <p>Please confirm your email address by clicking the button below:</p>
            <div class='button-wrapper'>
                <a href='{confirmationUrl}' class='button'>Confirm Email Address</a>
            </div>
            <div class='security-note'>
                <strong>Security Note</strong>
                This verification link will expire in <strong>24 hours</strong> for your security.
            </div>
            <div class='divider'></div>
            <div class='link-box'>
                <p style='margin-bottom: 8px;'><strong>Button not working?</strong> Copy and paste this link into your browser:</p>
                <p class='link-text'>{confirmationUrl}</p>
            </div>";
            
            return GetBaseTemplate(content);
        }

        /// <summary>
        /// Password reset email template with reset link
        /// </summary>
        public static string GetPasswordReset(string firstName, string resetUrl)
        {
            var content = $@"
            <h2>Hi {firstName},</h2>
            <p>We received a request to reset your password for your MultiTenant ETL account.</p>
            <p>Click the button below to create a new password:</p>
            <div class='button-wrapper'>
                <a href='{resetUrl}' class='button'>Reset Password</a>
            </div>
            <div class='security-note'>
                <strong>Security Note</strong>
                This password reset link will expire in <strong>1 hour</strong>. If you didn't request a password reset, you can safely ignore this email.
            </div>
            <div class='divider'></div>
            <div class='link-box'>
                <p style='margin-bottom: 8px;'><strong>Button not working?</strong> Copy and paste this link into your browser:</p>
                <p class='link-text'>{resetUrl}</p>
            </div>";
            
            return GetBaseTemplate(content);
        }

        /// <summary>
        /// Welcome email sent after successful email confirmation
        /// </summary>
        public static string GetWelcome(string firstName)
        {
            var content = $@"
            <h2>Welcome {firstName}</h2>
            <div class='success-note'>
                <strong>Email Confirmed</strong>
                Your account is now active and ready to use.
            </div>
            <p>You now have access to our complete data integration platform:</p>
            <ul>
                <li><strong>Data Connectors</strong> – Connect to multiple data sources</li>
                <li><strong>ETL Pipelines</strong> – Build powerful transformation workflows</li>
                <li><strong>Data Mapping</strong> – Transform and map your data</li>
                <li><strong>Automation</strong> – Schedule automated data flows</li>
            </ul>
            <div class='button-wrapper'>
                <a href='http://localhost:5173/login' class='button'>Get Started</a>
            </div>
            <div class='divider'></div>
            <p style='text-align: center; color: #718096; font-size: 14px;'>Need help getting started? Check out our <a href='#' style='color: #667eea;'>documentation</a> or <a href='#' style='color: #667eea;'>contact support</a>.</p>";
            
            return GetBaseTemplate(content);
        }

        /// <summary>
        /// Password changed notification for security
        /// </summary>
        public static string GetPasswordChanged(string firstName)
        {
            var content = $@"
            <h2>Hi {firstName},</h2>
            <div class='success-note'>
                <strong>Password Changed Successfully</strong>
                Your password has been updated.
            </div>
            <p><strong>When:</strong> {DateTime.UtcNow:MMMM dd, yyyy} at {DateTime.UtcNow:HH:mm} UTC</p>
            <div class='divider'></div>
            <div class='security-note'>
                <strong>Security Alert</strong>
                If you didn't make this change, please <a href='mailto:support@multitenanteti.com' style='color: #92400e; font-weight: 600;'>contact support immediately</a> to secure your account.
            </div>";
            
            return GetBaseTemplate(content);
        }
    }
}
