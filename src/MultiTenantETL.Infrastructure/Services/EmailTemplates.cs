using MultiTenantETL.Domain.Constants;
using System.Globalization;

namespace MultiTenantETL.Infrastructure.Services
{
    /// <summary>
    /// HTML email templates and shared email utility helpers
    /// </summary>
    public static class EmailTemplates
    {
        private static string HtmlEncode(string? value) => System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
        private static string AttrEncode(string? value) => HtmlEncode(value).Replace("'", "&#39;");

        private const string AppName = "MultiTenant ETL";
        private static int CurrentYear => DateTime.UtcNow.Year;

        private const string AppBg = "#0F1115";
        private const string Surface = "#14171C";
        private const string Card = "#171B22";
        private const string Border = "#232A33";
        private const string TextStrong = "#E6EDF6";
        private const string Text = "#D7E0EA";
        private const string TextMuted = "#9AA4B2";
        private const string Primary = "#38BDF8";
        private const string Success = "#34D399";
        private const string MonoFont = "ui-monospace,SFMono-Regular,Menlo,Consolas,Courier New,monospace";
        private const string SansFont = "Roboto,-apple-system,BlinkMacSystemFont,Segoe UI,Arial,sans-serif";

        private static string P(string text) =>
            $"<div style='color:{Text};font-family:{SansFont};font-size:13px;line-height:20px;margin:0 0 10px 0;'>{HtmlEncode(text)}</div>";

        private static string Muted(string text) =>
            $"<div style='color:{TextMuted};font-family:{SansFont};font-size:12px;line-height:18px;margin:0 0 10px 0;'>{HtmlEncode(text)}</div>";

        private static string Button(string url, string label) =>
            $@"<a href='{AttrEncode(url)}'
               style='display:inline-block;background:#262B33;border:1px solid #343C47;border-radius:6px;color:{TextStrong};text-decoration:none;font-family:{SansFont};font-size:12px;font-weight:800;letter-spacing:0.9px;text-transform:uppercase;padding:10px 16px;'>
                {HtmlEncode(label)}
            </a>";

        private static string CardBlock(string title, string bodyHtml) =>
            $@"
<table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%' style='background:{Card};border:1px solid {Border};border-radius:12px;'>
  <tr>
    <td style='padding:16px;'>
      <div style='color:#C9D1DB;font-family:{SansFont};font-size:12px;font-weight:800;letter-spacing:1px;text-transform:uppercase;margin:0 0 10px 0;'>
        {HtmlEncode(title)}
      </div>
      {bodyHtml}
    </td>
  </tr>
</table>";

        private static string CodeBlock(string text) =>
            $"<div style='margin-top:8px;padding:10px 12px;background:#11151B;border:1px solid {Border};border-radius:10px;color:{Text};font-family:{MonoFont};font-size:12px;line-height:18px;word-break:break-all;'>{HtmlEncode(text)}</div>";

        /// <summary>
        /// Returns the file extension for a given attachment format.
        /// Delegates to <see cref="MetadataConstants.FileFormats.GetExtension"/> as the single source of truth.
        /// </summary>
        public static string GetFileExtension(string? format)
        {
            return MetadataConstants.FileFormats.GetExtension(format);
        }

        /// <summary>
        /// Builds the full export filename including a UTC timestamp and the correct extension.
        /// Example: "data-export_20260216_143022.csv"
        /// </summary>
        public static string BuildExportFileName(string? baseFileName, string? format)
        {
            var safeName = string.IsNullOrWhiteSpace(baseFileName) ? "data-export" : baseFileName;
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            var extension = GetFileExtension(format);
            return $"{safeName}_{timestamp}{extension}";
        }

        /// <summary>
        /// Generates the complete HTML preview for a data-export email using sample data.
        /// This is the single source of truth for email previews; both the API preview
        /// endpoint and the actual email writer should produce identical output.
        /// </summary>
        public static string GenerateDataExportPreviewHtml(
            string? bodyMessage,
            string? attachmentFormat,
            string? attachmentFileName,
            int sampleRowCount = 1234)
        {
            var format = attachmentFormat ?? MetadataConstants.FileFormats.DefaultFormat;
            var fileName = BuildExportFileName(attachmentFileName, format);
            return GetDataExportEmail(bodyMessage, sampleRowCount, format, fileName);
        }

        private static string GetDashboardTemplate(string content)
        {
            // Dark, "dashboard-like" layout using tables for broad email client support.
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <meta name='color-scheme' content='dark light'>
    <meta name='supported-color-schemes' content='dark light'>
</head>
<body style='margin:0;padding:0;background:{AppBg};'>
    <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%' style='background:{AppBg};'>
        <tr>
            <td align='center' style='padding:28px 12px;'>
                <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='680' style='max-width:680px;width:100%;'>
                    <tr>
                        <td style='background:{Surface};border:1px solid {Border};border-radius:12px;overflow:hidden;'>
                            <div style='display:none;max-height:0;overflow:hidden;mso-hide:all;'>
                                Pipeline execution status update
                            </div>
                            <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%'>
                                <tr>
                                    <td style='padding:26px 24px 10px 24px;'>
                                        {content}
                                    </td>
                                </tr>
                                <tr>
                                    <td style='padding:16px 24px 22px 24px;border-top:1px solid {Border};color:{TextMuted};font-family:{SansFont};font-size:12px;line-height:18px;text-align:center;'>
                                        <div style='color:#C9D1DB;font-weight:600;'>© {CurrentYear} {AppName}</div>
                                        <div style='margin-top:6px;'>
                                            If you didn't request this email, please ignore it or
                                            <a href='mailto:support@multitenantetl.com' style='color:{Primary};text-decoration:none;'>contact support</a>.
                                        </div>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
        }

        /// <summary>
        /// Email confirmation template with link to verify email address
        /// </summary>
        public static string GetEmailConfirmation(string firstName, string confirmationUrl)
        {
            var header = $@"
                <div style='padding-bottom:18px;border-left:4px solid {Primary};padding-left:14px;'>
                    <div style='color:{TextMuted};font-family:{SansFont};font-size:11px;letter-spacing:1.2px;text-transform:uppercase;line-height:16px;'>
                        Account verification
                    </div>
                    <div style='margin-top:6px;color:{TextStrong};font-family:{SansFont};font-size:22px;line-height:28px;font-weight:800;'>
                        Hi {HtmlEncode(firstName)}
                    </div>
                </div>";

            var body = $@"
                {P("Thank you for registering with MultiTenant ETL.")}
                {Muted("Please confirm your email address by clicking the button below.")}
                <div style='text-align:center;margin:16px 0 10px 0;'>
                    {Button(confirmationUrl, "Confirm Email Address")}
                </div>
                {CardBlock("Security note", Muted("This verification link will expire in 24 hours for your security."))}
                <div style='height:1px;background:{Border};margin:16px 0;'></div>
                {CardBlock("Button not working?", Muted("Copy and paste this link into your browser:") + CodeBlock(confirmationUrl))}";

            return GetDashboardTemplate(header + body);
        }

        /// <summary>
        /// Password reset email template with reset link
        /// </summary>
        public static string GetPasswordReset(string firstName, string resetUrl)
        {
            var header = $@"
                <div style='padding-bottom:18px;border-left:4px solid {Primary};padding-left:14px;'>
                    <div style='color:{TextMuted};font-family:{SansFont};font-size:11px;letter-spacing:1.2px;text-transform:uppercase;line-height:16px;'>
                        Password reset
                    </div>
                    <div style='margin-top:6px;color:{TextStrong};font-family:{SansFont};font-size:22px;line-height:28px;font-weight:800;'>
                        Hi {HtmlEncode(firstName)}
                    </div>
                </div>";

            var body = $@"
                {P("We received a request to reset your password for your MultiTenant ETL account.")}
                {Muted("Click the button below to create a new password.")}
                <div style='text-align:center;margin:16px 0 10px 0;'>
                    {Button(resetUrl, "Reset Password")}
                </div>
                {CardBlock("Security note", Muted("This password reset link will expire in 1 hour. If you didn't request a password reset, you can safely ignore this email."))}
                <div style='height:1px;background:{Border};margin:16px 0;'></div>
                {CardBlock("Button not working?", Muted("Copy and paste this link into your browser:") + CodeBlock(resetUrl))}";

            return GetDashboardTemplate(header + body);
        }

        /// <summary>
        /// Welcome email sent after successful email confirmation
        /// </summary>
        public static string GetWelcome(string firstName, string frontendUrl)
        {
            var loginUrl = $"{frontendUrl.TrimEnd('/')}/login";
            var header = $@"
                <div style='padding-bottom:18px;border-left:4px solid {Primary};padding-left:14px;'>
                    <div style='color:{TextMuted};font-family:{SansFont};font-size:11px;letter-spacing:1.2px;text-transform:uppercase;line-height:16px;'>
                        Welcome
                    </div>
                    <div style='margin-top:6px;color:{TextStrong};font-family:{SansFont};font-size:22px;line-height:28px;font-weight:800;'>
                        Welcome {HtmlEncode(firstName)}
                    </div>
                </div>";

            var features = $@"
                <div style='color:{Text};font-family:{SansFont};font-size:13px;line-height:20px;margin:0;'>
                    <div style='margin:0 0 8px 0;'><strong>Data Connectors</strong> – Connect to multiple data sources</div>
                    <div style='margin:0 0 8px 0;'><strong>ETL Pipelines</strong> – Build powerful transformation workflows</div>
                    <div style='margin:0 0 8px 0;'><strong>Data Mapping</strong> – Transform and map your data</div>
                    <div style='margin:0;'><strong>Automation</strong> – Schedule automated data flows</div>
                </div>";

            var body = $@"
                {CardBlock("Email confirmed", $"<div style='color:{Success};font-family:{SansFont};font-weight:900;font-size:12px;letter-spacing:1px;text-transform:uppercase;margin:0 0 8px 0;'>Active</div>" + Muted("Your account is now ready to use."))}
                <div style='height:12px;'></div>
                {CardBlock("What you can do", features)}
                <div style='text-align:center;margin:18px 0 10px 0;'>
                    {Button(loginUrl, "Get Started")}
                </div>
                <div style='height:1px;background:{Border};margin:16px 0;'></div>
                <div style='text-align:center;color:{TextMuted};font-family:{SansFont};font-size:12px;line-height:18px;'>
                    Need help getting started? Contact <a href='mailto:support@multitenantetl.com' style='color:{Primary};text-decoration:none;'>support</a>.
                </div>";

            return GetDashboardTemplate(header + body);
        }

        /// <summary>
        /// Password changed notification for security
        /// </summary>
        public static string GetPasswordChanged(string firstName)
        {
            var header = $@"
                <div style='padding-bottom:18px;border-left:4px solid {Primary};padding-left:14px;'>
                    <div style='color:{TextMuted};font-family:{SansFont};font-size:11px;letter-spacing:1.2px;text-transform:uppercase;line-height:16px;'>
                        Security notification
                    </div>
                    <div style='margin-top:6px;color:{TextStrong};font-family:{SansFont};font-size:22px;line-height:28px;font-weight:800;'>
                        Hi {HtmlEncode(firstName)}
                    </div>
                </div>";

            var whenUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);

            var body = $@"
                {CardBlock("Password changed", $"<div style='color:{Success};font-family:{SansFont};font-weight:900;font-size:12px;letter-spacing:1px;text-transform:uppercase;margin:0 0 8px 0;'>Successful</div>" + Muted("Your password has been updated."))}
                <div style='height:12px;'></div>
                {CardBlock("When", CodeBlock(whenUtc))}
                <div style='height:12px;'></div>
                {CardBlock("Security alert", Muted("If you didn't make this change, contact support immediately to secure your account.") +
                                     $"<div style='margin-top:10px;'><a href='mailto:support@multitenantetl.com' style='color:{Primary};text-decoration:none;font-family:{SansFont};font-weight:700;'>support@multitenantetl.com</a></div>")}";

            return GetDashboardTemplate(header + body);
        }

        /// <summary>
        /// Role changed notification for security
        /// </summary>
        public static string GetRoleChanged(string firstName, string oldRole, string newRole, string changedBy)
        {
            var header = $@"
                <div style='padding-bottom:18px;border-left:4px solid {Primary};padding-left:14px;'>
                    <div style='color:{TextMuted};font-family:{SansFont};font-size:11px;letter-spacing:1.2px;text-transform:uppercase;line-height:16px;'>
                        Access change
                    </div>
                    <div style='margin-top:6px;color:{TextStrong};font-family:{SansFont};font-size:22px;line-height:28px;font-weight:800;'>
                        Hi {HtmlEncode(firstName)}
                    </div>
                </div>";

            var whenUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);

            var body = $@"
                {CardBlock("Role updated", $@"<div style='color:{Success};font-family:{SansFont};font-weight:900;font-size:12px;letter-spacing:1px;text-transform:uppercase;margin:0 0 8px 0;'>Updated</div>" + Muted("Your role has been changed."))}
                <div style='height:12px;'></div>
                <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%'>
                    <tr>
                        <td style='padding:10px 0;border-top:1px solid {Border};color:{TextMuted};font-family:{SansFont};font-size:12px;'>New role</td>
                        <td align='right' style='padding:10px 0;border-top:1px solid {Border};color:{Success};font-family:{SansFont};font-size:14px;font-weight:700;'>{HtmlEncode(newRole)}</td>
                    </tr>
                    <tr>
                        <td style='padding:10px 0;border-top:1px solid {Border};color:{TextMuted};font-family:{SansFont};font-size:12px;'>Changed by</td>
                        <td align='right' style='padding:10px 0;border-top:1px solid {Border};color:{Text};font-family:{SansFont};font-size:12px;'>{HtmlEncode(changedBy)}</td>
                    </tr>
                    <tr>
                        <td style='padding:10px 0;border-top:1px solid {Border};color:{TextMuted};font-family:{SansFont};font-size:12px;'>When</td>
                        <td align='right' style='padding:10px 0;border-top:1px solid {Border};color:{Text};font-family:{SansFont};font-size:12px;'>{HtmlEncode(whenUtc)}</td>
                    </tr>
                </table>
                <div style='height:12px;'></div>
                {CardBlock("Security alert", Muted("If you didn't expect this change, contact support immediately.") +
                                     $"<div style='margin-top:10px;'><a href='mailto:support@multitenantetl.com' style='color:{Primary};text-decoration:none;font-family:{SansFont};font-weight:700;'>support@multitenantetl.com</a></div>")}";

            return GetDashboardTemplate(header + body);
        }

        /// <summary>
        /// Tenant changed notification for security
        /// </summary>
        public static string GetTenantChanged(string firstName, string oldTenant, string newTenant, string changedBy)
        {
            var header = $@"
                <div style='padding-bottom:18px;border-left:4px solid {Primary};padding-left:14px;'>
                    <div style='color:{TextMuted};font-family:{SansFont};font-size:11px;letter-spacing:1.2px;text-transform:uppercase;line-height:16px;'>
                        Access change
                    </div>
                    <div style='margin-top:6px;color:{TextStrong};font-family:{SansFont};font-size:22px;line-height:28px;font-weight:800;'>
                        Hi {HtmlEncode(firstName)}
                    </div>
                </div>";

            var whenUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);

            var body = $@"
                {CardBlock("Tenant updated", $@"<div style='color:{Success};font-family:{SansFont};font-weight:900;font-size:12px;letter-spacing:1px;text-transform:uppercase;margin:0 0 8px 0;'>Updated</div>" + Muted("You have been added to a new tenant."))}
                <div style='height:12px;'></div>
                <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%'>
                    <tr>
                        <td style='padding:10px 0;border-top:1px solid {Border};color:{TextMuted};font-family:{SansFont};font-size:12px;'>New tenant</td>
                        <td align='right' style='padding:10px 0;border-top:1px solid {Border};color:{Success};font-family:{SansFont};font-size:14px;font-weight:700;'>{HtmlEncode(newTenant)}</td>
                    </tr>
                    <tr>
                        <td style='padding:10px 0;border-top:1px solid {Border};color:{TextMuted};font-family:{SansFont};font-size:12px;'>Changed by</td>
                        <td align='right' style='padding:10px 0;border-top:1px solid {Border};color:{Text};font-family:{SansFont};font-size:12px;'>{HtmlEncode(changedBy)}</td>
                    </tr>
                    <tr>
                        <td style='padding:10px 0;border-top:1px solid {Border};color:{TextMuted};font-family:{SansFont};font-size:12px;'>When</td>
                        <td align='right' style='padding:10px 0;border-top:1px solid {Border};color:{Text};font-family:{SansFont};font-size:12px;'>{HtmlEncode(whenUtc)}</td>
                    </tr>
                </table>
                <div style='height:12px;'></div>
                {CardBlock("Security alert", Muted("If you didn't expect this change, contact support immediately.") +
                                     $"<div style='margin-top:10px;'><a href='mailto:support@multitenantetl.com' style='color:{Primary};text-decoration:none;font-family:{SansFont};font-weight:700;'>support@multitenantetl.com</a></div>")}";

            return GetDashboardTemplate(header + body);
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
            _ = errorMessage;

            var statusLower = (executionStatus ?? string.Empty).Trim().ToLowerInvariant();

            var statusColor = statusLower switch
            {
                "completed" => "#34D399",
                "succeeded" => "#34D399",
                "success" => "#34D399",
                "failed" => "#F87171",
                "cancelled" => "#FBBF24",
                "canceled" => "#FBBF24",
                _ => "#9AA4B2"
            };

            var statusPillBg = statusLower switch
            {
                "completed" => "#0F2A1E",
                "succeeded" => "#0F2A1E",
                "success" => "#0F2A1E",
                "failed" => "#2B1214",
                "cancelled" => "#2B2312",
                "canceled" => "#2B2312",
                _ => "#1A1F26"
            };

            var durationText = duration.HasValue
                ? $"{(int)duration.Value.TotalMinutes:00}:{duration.Value.Seconds:00}.{duration.Value.Milliseconds:000}"
                : "N/A";

            var endTimeText = endTime.HasValue
                ? endTime.Value.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture)
                : "N/A";

            var processedSafe = Math.Max(0, recordsProcessed);
            var succeededSafe = Math.Max(0, recordsSucceeded);
            var failedSafe = Math.Max(0, recordsFailed);

            var successRate = processedSafe > 0 ? (double)succeededSafe / processedSafe * 100d : 0d;
            if (successRate < 0) successRate = 0;
            if (successRate > 100) successRate = 100;

            var failedRate = processedSafe > 0 ? (double)failedSafe / processedSafe * 100d : 0d;
            if (failedRate < 0) failedRate = 0;
            if (failedRate > 100) failedRate = 100;

            var successWidthPct = (int)Math.Round(successRate, MidpointRounding.AwayFromZero);
            if (successWidthPct < 0) successWidthPct = 0;
            if (successWidthPct > 100) successWidthPct = 100;
            var remainingWidthPct = 100 - successWidthPct;

            var startedUtc = startTime.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);
            var execIdEncoded = HtmlEncode(executionId);
            var pipelineEncoded = HtmlEncode(pipelineName);
            var statusEncoded = HtmlEncode(executionStatus);
            var detailsUrlEncoded = HtmlEncode(executionDetailsUrl);

            var content = $@"
                <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%'>
                    <tr>
                        <td style='padding-bottom:18px;border-left:4px solid #38BDF8;padding-left:14px;'>
                            <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%'>
                                <tr>
                                    <td style='padding-top:6px;color:#E6EDF6;font-family:Roboto,-apple-system,BlinkMacSystemFont,Segoe UI,Arial,sans-serif;font-size:22px;line-height:28px;font-weight:700;'>
                                        {pipelineEncoded}
                                    </td>
                                    <td align='right' style='padding-top:6px;'>
                                        <span style='display:inline-block;background:{statusPillBg};border:1px solid #232A33;color:{statusColor};font-family:Roboto,-apple-system,BlinkMacSystemFont,Segoe UI,Arial,sans-serif;font-size:11px;letter-spacing:0.8px;text-transform:uppercase;font-weight:700;padding:8px 10px;border-radius:999px;'>
                                            {statusEncoded}
                                        </span>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                </table>

                <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%'>
                    <tr>
                        <td style='padding-left:14px;padding-top:2px;'>
                <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%'>
                    <tr>
                        <td valign='top' width='60%' style='padding-right:10px;'>
                            <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%' style='background:#171B22;border:1px solid #232A33;border-radius:12px;'>
                                <tr>
                                    <td style='padding:16px 16px 12px 16px;'>
                                        <div style='color:#C9D1DB;font-family:Roboto,-apple-system,BlinkMacSystemFont,Segoe UI,Arial,sans-serif;font-size:12px;font-weight:700;letter-spacing:1px;text-transform:uppercase;'>
                                            Execution summary
                                        </div>
                                    </td>
                                </tr>
                                <tr>
                                    <td style='padding:0 16px 16px 16px;'>
                                        <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%'>
                                            <tr>
                                                <td style='padding:10px 0;border-top:1px solid #232A33;color:#9AA4B2;font-family:Roboto,-apple-system,BlinkMacSystemFont,Segoe UI,Arial,sans-serif;font-size:12px;'>Execution ID</td>
                                                <td align='right' style='padding:10px 0;border-top:1px solid #232A33;color:#D7E0EA;font-family:ui-monospace,SFMono-Regular,Menlo,Consolas,Courier New,monospace;font-size:12px;'>{execIdEncoded}</td>
                                            </tr>
                                            <tr>
                                                <td style='padding:10px 0;border-top:1px solid #232A33;color:#9AA4B2;font-family:Roboto,-apple-system,BlinkMacSystemFont,Segoe UI,Arial,sans-serif;font-size:12px;'>Started at</td>
                                                <td align='right' style='padding:10px 0;border-top:1px solid #232A33;color:#D7E0EA;font-family:Roboto,-apple-system,BlinkMacSystemFont,Segoe UI,Arial,sans-serif;font-size:12px;'>{HtmlEncode(startedUtc)}</td>
                                            </tr>
                                            <tr>
                                                <td style='padding:10px 0;border-top:1px solid #232A33;color:#9AA4B2;font-family:Roboto,-apple-system,BlinkMacSystemFont,Segoe UI,Arial,sans-serif;font-size:12px;'>Completed at</td>
                                                <td align='right' style='padding:10px 0;border-top:1px solid #232A33;color:#D7E0EA;font-family:Roboto,-apple-system,BlinkMacSystemFont,Segoe UI,Arial,sans-serif;font-size:12px;'>{HtmlEncode(endTimeText)}</td>
                                            </tr>
                                            <tr>
                                                <td style='padding:10px 0;border-top:1px solid #232A33;color:#9AA4B2;font-family:Roboto,-apple-system,BlinkMacSystemFont,Segoe UI,Arial,sans-serif;font-size:12px;'>Duration</td>
                                                <td align='right' style='padding:10px 0;border-top:1px solid #232A33;color:#E6EDF6;font-family:Roboto,-apple-system,BlinkMacSystemFont,Segoe UI,Arial,sans-serif;font-size:18px;font-weight:800;letter-spacing:0.3px;'>{HtmlEncode(durationText)}</td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                            </table>
                        </td>
                        <td valign='top' width='40%' style='padding-left:10px;'>
                            <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%' style='background:#171B22;border:1px solid #232A33;border-radius:12px;'>
                                <tr>
                                    <td style='padding:16px 16px 10px 16px;'>
                                        <div style='color:#9AA4B2;font-family:Roboto,-apple-system,BlinkMacSystemFont,Segoe UI,Arial,sans-serif;font-size:11px;letter-spacing:1.1px;text-transform:uppercase;font-weight:700;'>
                                            Total processed
                                        </div>
                                    </td>
                                </tr>
                                <tr>
                                    <td style='padding:0 16px 10px 16px;color:#E6EDF6;font-family:Roboto,-apple-system,BlinkMacSystemFont,Segoe UI,Arial,sans-serif;font-size:34px;line-height:38px;font-weight:900;letter-spacing:0.5px;'>
                                        {processedSafe:N0}
                                        <span style='color:#9AA4B2;font-size:12px;font-weight:700;letter-spacing:1px;text-transform:uppercase;'>records</span>
                                    </td>
                                </tr>
                                <tr>
                                    <td style='padding:0 16px 14px 16px;'>
                                        <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%' style='background:#11151B;border:1px solid #232A33;border-radius:999px;'>
                                            <tr>
                                                <td width='{successWidthPct}%' style='background:#38BDF8;border-radius:999px;height:8px;font-size:0;line-height:0;'>&nbsp;</td>
                                                <td width='{remainingWidthPct}%' style='height:8px;font-size:0;line-height:0;'>&nbsp;</td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                                <tr>
                                    <td style='padding:0 16px 16px 16px;'>
                                        <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%'>
                                            <tr>
                                                <td valign='top' width='50%' style='padding-right:8px;'>
                                                    <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%' style='background:#14171C;border:1px solid #232A33;border-radius:10px;'>
                                                        <tr>
                                                            <td style='padding:12px;'>
                                                                <div style='color:#34D399;font-family:Roboto,-apple-system,BlinkMacSystemFont,Segoe UI,Arial,sans-serif;font-size:11px;letter-spacing:1px;text-transform:uppercase;font-weight:800;'>Success</div>
                                                                <div style='margin-top:6px;color:#E6EDF6;font-family:Roboto,-apple-system,BlinkMacSystemFont,Segoe UI,Arial,sans-serif;font-size:18px;font-weight:900;'>
                                                                    {succeededSafe:N0}
                                                                </div>
                                                                <div style='margin-top:2px;color:#9AA4B2;font-family:Roboto,-apple-system,BlinkMacSystemFont,Segoe UI,Arial,sans-serif;font-size:11px;'>
                                                                    {successRate:0.##}%
                                                                </div>
                                                            </td>
                                                        </tr>
                                                    </table>
                                                </td>
                                                <td valign='top' width='50%' style='padding-left:8px;'>
                                                    <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%' style='background:#14171C;border:1px solid #232A33;border-radius:10px;'>
                                                        <tr>
                                                            <td style='padding:12px;'>
                                                                <div style='color:#F87171;font-family:Roboto,-apple-system,BlinkMacSystemFont,Segoe UI,Arial,sans-serif;font-size:11px;letter-spacing:1px;text-transform:uppercase;font-weight:800;'>Failed</div>
                                                                <div style='margin-top:6px;color:#E6EDF6;font-family:Roboto,-apple-system,BlinkMacSystemFont,Segoe UI,Arial,sans-serif;font-size:18px;font-weight:900;'>
                                                                    {failedSafe:N0}
                                                                </div>
                                                                <div style='margin-top:2px;color:#9AA4B2;font-family:Roboto,-apple-system,BlinkMacSystemFont,Segoe UI,Arial,sans-serif;font-size:11px;'>
                                                                    {failedRate:0.##}%
                                                                </div>
                                                            </td>
                                                        </tr>
                                                    </table>
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                </table>

                <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%' style='margin-top:18px;'>
                    <tr>
                        <td align='center' style='padding:14px 0 6px 0;'>
                            {Button(executionDetailsUrl, "View execution details")}
                        </td>
                    </tr>
                    <tr>
                        <td align='center' style='padding-top:10px;color:#9AA4B2;font-family:Roboto,-apple-system,BlinkMacSystemFont,Segoe UI,Arial,sans-serif;font-size:12px;line-height:18px;'>
                            Automated notification from your MultiTenant ETL pipeline.
                        </td>
                    </tr>
                </table>
                        </td>
                    </tr>
                </table>";

            return GetDashboardTemplate(content);
        }

        /// <summary>
        /// Data export email template with summary information.
        /// The email body contains only a summary; all data rows are in the file attachment.
        /// </summary>
        public static string GetDataExportEmail(
            string? bodyMessage,
            int totalRows,
            string attachmentFormat,
            string fileName)
        {
            var timestampUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);

            var header = $@"
                <div style='padding-bottom:18px;border-left:4px solid {Primary};padding-left:14px;'>
                    <div style='color:{TextMuted};font-family:{SansFont};font-size:11px;letter-spacing:1.2px;text-transform:uppercase;line-height:16px;'>
                        Data export
                    </div>
                    <div style='margin-top:6px;color:{TextStrong};font-family:{SansFont};font-size:22px;line-height:28px;font-weight:800;'>
                        Export report
                    </div>
                </div>";

            var customMessage = !string.IsNullOrWhiteSpace(bodyMessage)
                ? CardBlock("Message", P(bodyMessage))
                : string.Empty;

            var summaryBody = $@"
                <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%'>
                    <tr>
                        <td style='padding:10px 0;border-top:1px solid {Border};color:{TextMuted};font-family:{SansFont};font-size:12px;'>Total rows exported</td>
                        <td align='right' style='padding:10px 0;border-top:1px solid {Border};color:{TextStrong};font-family:{SansFont};font-size:16px;font-weight:900;'>{Math.Max(0, totalRows):N0}</td>
                    </tr>
                    <tr>
                        <td style='padding:10px 0;border-top:1px solid {Border};color:{TextMuted};font-family:{SansFont};font-size:12px;'>Attachment format</td>
                        <td align='right' style='padding:10px 0;border-top:1px solid {Border};color:{Text};font-family:{SansFont};font-size:12px;'>{HtmlEncode(attachmentFormat)}</td>
                    </tr>
                    <tr>
                        <td style='padding:10px 0;border-top:1px solid {Border};color:{TextMuted};font-family:{SansFont};font-size:12px;'>Filename</td>
                        <td align='right' style='padding:10px 0;border-top:1px solid {Border};color:{Text};font-family:{MonoFont};font-size:12px;'>{HtmlEncode(fileName)}</td>
                    </tr>
                    <tr>
                        <td style='padding:10px 0;border-top:1px solid {Border};color:{TextMuted};font-family:{SansFont};font-size:12px;'>Exported at</td>
                        <td align='right' style='padding:10px 0;border-top:1px solid {Border};color:{Text};font-family:{SansFont};font-size:12px;'>{HtmlEncode(timestampUtc)}</td>
                    </tr>
                </table>";

            var body = $@"
                {CardBlock("Status", $"<div style='color:{Success};font-family:{SansFont};font-weight:900;font-size:12px;letter-spacing:1px;text-transform:uppercase;margin:0 0 8px 0;'>Export complete</div>" + Muted("Your data export has completed. The file is attached to this email."))}
                {(string.IsNullOrEmpty(customMessage) ? "" : "<div style='height:12px;'></div>" + customMessage)}
                <div style='height:12px;'></div>
                {CardBlock("Summary", summaryBody)}
                <div style='height:1px;background:{Border};margin:16px 0;'></div>
                <div style='text-align:center;color:{TextMuted};font-family:{SansFont};font-size:12px;line-height:18px;'>
                    Automated data export from your MultiTenant ETL pipeline.
                </div>";

            var content = header + $@"
                <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%'>
                    <tr>
                        <td style='padding-left:14px;padding-top:2px;'>
                            {body}
                        </td>
                    </tr>
                </table>";

            return GetDashboardTemplate(content);
        }
    }
}
