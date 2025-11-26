using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors;
using MultiTenantETL.Application.Connectors.Models;
using Renci.SshNet;

namespace MultiTenantETL.Infrastructure.Services.ConnectionTesting.Storage;

public class SftpConnectionTester
{
    private readonly ILogger<SftpConnectionTester> _logger;

    public SftpConnectionTester(ILogger<SftpConnectionTester> logger)
    {
        _logger = logger;
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(FileConfig config)
    {
        // Validate required SFTP fields
        if (string.IsNullOrEmpty(config.SftpHost))
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = "SFTP Host is required"
            };
        }

        if (string.IsNullOrEmpty(config.SftpUsername))
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = "SFTP Username is required"
            };
        }

        if (string.IsNullOrEmpty(config.SftpPassword))
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = "SFTP Password is required"
            };
        }

        try
        {
            var port = config.SftpPort ?? 22;
            
            using var sftpClient = new SftpClient(config.SftpHost, port, config.SftpUsername, config.SftpPassword);
            
            // Connect to SFTP server
            await Task.Run(() => sftpClient.Connect());
            
            if (!sftpClient.IsConnected)
            {
                return new ConnectionTestResult
                {
                    Success = false,
                    Message = "Failed to connect to SFTP server"
                };
            }

            // Test if path exists (if provided)
            bool pathExists = false;
            string pathType = "Unknown";
            
            if (!string.IsNullOrEmpty(config.Path))
            {
                pathExists = sftpClient.Exists(config.Path);
                if (pathExists)
                {
                    var attrs = sftpClient.GetAttributes(config.Path);
                    pathType = attrs.IsDirectory ? "Directory" : "File";
                }
                else
                {
                    pathType = "Not Found";
                }
            }

            var details = new Dictionary<string, object>
            {
                ["Host"] = config.SftpHost,
                ["Port"] = port,
                ["IsConnected"] = sftpClient.IsConnected,
                ["ProtocolVersion"] = sftpClient.ProtocolVersion,
                ["ServerVersion"] = sftpClient.ConnectionInfo.ServerVersion,
                ["Path"] = config.Path ?? "Not specified",
                ["PathExists"] = pathExists,
                ["PathType"] = pathType
            };

            sftpClient.Disconnect();

            return new ConnectionTestResult
            {
                Success = true,
                Message = $"Successfully connected to SFTP server at {config.SftpHost}:{port}",
                Details = details
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SFTP connection test failed");
            
            var errorMessage = GetRootErrorMessage(ex);
            
            return new ConnectionTestResult
            {
                Success = false,
                Message = $"SFTP connection failed: {errorMessage}"
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
