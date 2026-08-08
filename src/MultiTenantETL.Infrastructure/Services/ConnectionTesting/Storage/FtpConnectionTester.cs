using FluentFTP;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors;
using MultiTenantETL.Application.Connectors.Models;
using MultiTenantETL.Infrastructure.Security;

namespace MultiTenantETL.Infrastructure.Services.ConnectionTesting.Storage;

public class FtpConnectionTester
{
    private readonly ISsrfGuard _ssrfGuard;
    private readonly ILogger<FtpConnectionTester> _logger;

    public FtpConnectionTester(ILogger<FtpConnectionTester> logger, ISsrfGuard ssrfGuard)
    {
        _logger = logger;
        _ssrfGuard = ssrfGuard;
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(FileConfig config)
    {
        // Validate required FTP fields
        if (string.IsNullOrEmpty(config.FtpHost))
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = "FTP Host is required"
            };
        }

        if (string.IsNullOrEmpty(config.FtpUsername))
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = "FTP Username is required"
            };
        }

        if (string.IsNullOrEmpty(config.FtpPassword))
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = "FTP Password is required"
            };
        }

        try
        {
            var port = config.FtpPort ?? 21;

            _ssrfGuard.ValidateHost(config.FtpHost);

            using var ftpClient = new AsyncFtpClient(config.FtpHost, config.FtpUsername, config.FtpPassword, port);

            // Connect to FTP server
            await ftpClient.Connect();

            if (!ftpClient.IsConnected)
            {
                return new ConnectionTestResult
                {
                    Success = false,
                    Message = "Failed to connect to FTP server"
                };
            }

            // Test if path exists (if provided)
            bool pathExists = false;
            string pathType = "Unknown";

            if (!string.IsNullOrEmpty(config.Path))
            {
                var fileExists = await ftpClient.FileExists(config.Path);
                var dirExists = await ftpClient.DirectoryExists(config.Path);
                pathExists = fileExists || dirExists;
                pathType = fileExists ? "File" : dirExists ? "Directory" : "Not Found";
            }

            var details = new Dictionary<string, object>
            {
                ["Host"] = config.FtpHost ?? "Unknown",
                ["Port"] = port,
                ["IsConnected"] = ftpClient.IsConnected,
                ["ServerType"] = ftpClient.ServerType.ToString(),
                ["Path"] = config.Path ?? "Not specified",
                ["PathExists"] = pathExists,
                ["PathType"] = pathType
            };

            await ftpClient.Disconnect();

            return new ConnectionTestResult
            {
                Success = true,
                Message = $"Successfully connected to FTP server at {config.FtpHost}:{port}",
                Details = details
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FTP connection test failed");

            var errorMessage = GetRootErrorMessage(ex);

            return new ConnectionTestResult
            {
                Success = false,
                Message = $"FTP connection failed: {errorMessage}"
            };
        }
    }

    private static string GetRootErrorMessage(Exception ex)
    {
        var innermost = ex;
        while (innermost.InnerException != null)
        {
            innermost = innermost.InnerException;
        }
        return innermost.Message;
    }
}
