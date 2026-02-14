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
            color: #1a1a1a; 
            background-color: #f7f7f7;
            margin: 0; 
            padding: 40px 20px; 
        }}
        .container {{ 
            max-width: 600px; 
            margin: 0 auto; 
            background: white; 
            border-radius: 8px; 
            overflow: hidden; 
            box-shadow: 0 2px 8px rgba(0,0,0,0.06);
        }}
        .content {{ 
            padding: 50px 40px; 
            background: white;
        }}
        .content h2 {{
            color: #1a1a1a;
            font-size: 20px;
            margin-bottom: 16px;
            font-weight: 600;
        }}
        .content p {{
            color: #333;
            font-size: 15px;
            margin-bottom: 16px;
            line-height: 1.6;
        }}
        .content ul {{
            color: #333;
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
            margin: 28px 0;
        }}
        .button {{ 
            display: inline-block; 
            padding: 12px 28px; 
            background: #0066FF;
            color: white !important; 
            text-decoration: none; 
            border-radius: 6px; 
            font-weight: 500; 
            font-size: 15px;
            transition: background 0.2s;
        }}
        .button:hover {{
            background: #0052CC;
        }}
        .security-note {{ 
            background: #FFF8E1;
            border-left: 3px solid #FFA000; 
            padding: 16px; 
            margin: 24px 0; 
            border-radius: 4px;
            font-size: 14px;
            color: #5D4037;
        }}
        .security-note strong {{
            color: #5D4037;
            display: block;
            margin-bottom: 4px;
            font-weight: 600;
        }}
        .success-note {{
            background: #E8F5E9;
            border-left: 3px solid #4CAF50;
            padding: 16px;
            margin: 24px 0;
            border-radius: 4px;
            font-size: 14px;
            color: #1B5E20;
        }}
        .success-note strong {{
            color: #1B5E20;
            display: block;
            margin-bottom: 4px;
            font-weight: 600;
        }}
        .link-box {{
            background: #F5F5F5;
            padding: 16px;
            border-radius: 4px;
            margin: 20px 0;
            border: 1px solid #E0E0E0;
        }}
        .link-box p {{
            margin: 0;
            font-size: 13px;
            color: #666;
        }}
        .link-text {{
            color: #0066FF !important;
            word-break: break-all;
            font-size: 12px;
            font-family: 'Courier New', monospace;
            background: white;
            padding: 8px;
            border-radius: 4px;
            border: 1px solid #E0E0E0;
            display: block;
            margin-top: 8px;
        }}
        .footer {{ 
            background: #FAFAFA; 
            padding: 30px 40px; 
            text-align: center; 
            font-size: 13px; 
            color: #666; 
            border-top: 1px solid #E5E5E5;
        }}
        .footer p {{
            margin: 6px 0;
        }}
        .footer a {{
            color: #0066FF;
            text-decoration: none;
        }}
        .divider {{
            height: 1px;
            background: #E5E5E5;
            margin: 24px 0;
        }}
        @media only screen and (max-width: 600px) {{
            body {{
                padding: 20px 10px;
            }}
            .container {{
                border-radius: 6px;
            }}
            .content, .header, .footer {{
                padding-left: 24px;
                padding-right: 24px;
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
        <div class='content'>{content}</div>
        <div class='footer'>
            <p><strong>© 2025 MultiTenant ETL</strong> · All rights reserved</p>
            <p>If you didn't request this email, please ignore it or <a href='mailto:support@multitenantetl.com'>contact support</a></p>
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
        public static string GetWelcome(string firstName, string frontendUrl)
        {
            var loginUrl = $"{frontendUrl.TrimEnd('/')}/login";
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
                <a href='{loginUrl}' class='button'>Get Started</a>
            </div>
            <div class='divider'></div>
            <p style='text-align: center; color: #666; font-size: 14px;'>Need help getting started? Check out our <a href='#' style='color: #0066FF;'>documentation</a> or <a href='#' style='color: #0066FF;'>contact support</a>.</p>";
            
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
                If you didn't make this change, please <a href='mailto:support@multitenantetl.com' style='color: #5D4037; font-weight: 600;'>contact support immediately</a> to secure your account.
            </div>";
            
            return GetBaseTemplate(content);
        }
        
        /// <summary>
        /// Pipeline execution report with execution summary and statistics
        /// </summary>
        public static string GetPipelineExecutionReport(
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
            var statusColor = executionStatus.ToLower() switch
            {
                "completed" => "#4CAF50",
                "failed" => "#F44336",
                "cancelled" => "#FF9800",
                _ => "#9E9E9E"
            };
            
            var statusBgColor = executionStatus.ToLower() switch
            {
                "completed" => "#E8F5E9",
                "failed" => "#FFEBEE",
                "cancelled" => "#FFF3E0",
                _ => "#F5F5F5"
            };
            
            var statusIcon = executionStatus.ToLower() switch
            {
                "completed" => "✓",
                "failed" => "✗",
                "cancelled" => "⊘",
                _ => "•"
            };
            
            var durationText = duration.HasValue 
                ? $"{(int)duration.Value.TotalMinutes}m {duration.Value.Seconds}s"
                : "N/A";
                
            var endTimeText = endTime.HasValue 
                ? endTime.Value.ToString("MMMM dd, yyyy 'at' HH:mm 'UTC'")
                : "N/A";
            
            var errorSection = !string.IsNullOrEmpty(errorMessage) 
                ? $@"
            <div class='security-note'>
                <strong>Error Details</strong>
                {System.Net.WebUtility.HtmlEncode(errorMessage)}
            </div>" 
                : "";
            
            var content = $@"
            <h2>Pipeline Execution Report</h2>
            <div style='background: {statusBgColor}; border-left: 4px solid {statusColor}; padding: 16px; margin: 20px 0; border-radius: 4px;'>
                <div style='display: flex; align-items: center;'>
                    <span style='font-size: 24px; margin-right: 12px;'>{statusIcon}</span>
                    <div>
                        <strong style='color: {statusColor}; font-size: 16px; display: block;'>{executionStatus.ToUpper()}</strong>
                        <span style='color: #666; font-size: 14px;'>{pipelineName}</span>
                    </div>
                </div>
            </div>
            
            <div style='background: #F5F5F5; padding: 20px; border-radius: 6px; margin: 20px 0;'>
                <table style='width: 100%; border-collapse: collapse;'>
                    <tr>
                        <td style='padding: 8px 0; color: #666; font-size: 14px;'>Execution ID</td>
                        <td style='padding: 8px 0; color: #1a1a1a; font-size: 14px; text-align: right; font-family: monospace;'>{executionId}</td>
                    </tr>
                    <tr style='border-top: 1px solid #E0E0E0;'>
                        <td style='padding: 8px 0; color: #666; font-size: 14px;'>Started At</td>
                        <td style='padding: 8px 0; color: #1a1a1a; font-size: 14px; text-align: right;'>{startTime:MMMM dd, yyyy 'at' HH:mm 'UTC'}</td>
                    </tr>
                    <tr style='border-top: 1px solid #E0E0E0;'>
                        <td style='padding: 8px 0; color: #666; font-size: 14px;'>Completed At</td>
                        <td style='padding: 8px 0; color: #1a1a1a; font-size: 14px; text-align: right;'>{endTimeText}</td>
                    </tr>
                    <tr style='border-top: 1px solid #E0E0E0;'>
                        <td style='padding: 8px 0; color: #666; font-size: 14px;'>Duration</td>
                        <td style='padding: 8px 0; color: #1a1a1a; font-size: 14px; text-align: right;'>{durationText}</td>
                    </tr>
                </table>
            </div>
            
            <h2 style='font-size: 16px; margin: 24px 0 12px;'>Execution Statistics</h2>
            <div style='display: grid; grid-template-columns: repeat(3, 1fr); gap: 12px; margin: 20px 0;'>
                <div style='background: white; border: 1px solid #E0E0E0; padding: 16px; border-radius: 6px; text-align: center;'>
                    <div style='font-size: 24px; font-weight: 600; color: #0066FF; margin-bottom: 4px;'>{recordsProcessed:N0}</div>
                    <div style='font-size: 12px; color: #666; text-transform: uppercase; letter-spacing: 0.5px;'>Processed</div>
                </div>
                <div style='background: white; border: 1px solid #E0E0E0; padding: 16px; border-radius: 6px; text-align: center;'>
                    <div style='font-size: 24px; font-weight: 600; color: #4CAF50; margin-bottom: 4px;'>{recordsSucceeded:N0}</div>
                    <div style='font-size: 12px; color: #666; text-transform: uppercase; letter-spacing: 0.5px;'>Succeeded</div>
                </div>
                <div style='background: white; border: 1px solid #E0E0E0; padding: 16px; border-radius: 6px; text-align: center;'>
                    <div style='font-size: 24px; font-weight: 600; color: #F44336; margin-bottom: 4px;'>{recordsFailed:N0}</div>
                    <div style='font-size: 12px; color: #666; text-transform: uppercase; letter-spacing: 0.5px;'>Failed</div>
                </div>
            </div>
            {errorSection}
            <div class='button-wrapper'>
                <a href='{executionDetailsUrl}' class='button'>View Full Execution Details</a>
            </div>
            <div class='divider'></div>
            <p style='text-align: center; color: #666; font-size: 13px;'>This is an automated notification from your MultiTenant ETL pipeline.</p>";
            
            return GetBaseTemplate(content);
        }
    }
}
