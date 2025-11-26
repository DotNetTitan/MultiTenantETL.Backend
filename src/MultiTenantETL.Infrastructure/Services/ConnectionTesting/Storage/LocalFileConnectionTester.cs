using MultiTenantETL.Application.Connectors;
using MultiTenantETL.Application.Connectors.Models;

namespace MultiTenantETL.Infrastructure.Services.ConnectionTesting.Storage;

public class LocalFileConnectionTester
{
    public static ConnectionTestResult TestConnection(FileConfig config)
    {
        if (!File.Exists(config.Path) && !Directory.Exists(config.Path))
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = $"File or directory not found: {config.Path}"
            };
        }

        var isDirectory = Directory.Exists(config.Path);
        var details = new Dictionary<string, object>
        {
            ["Path"] = config.Path,
            ["Type"] = isDirectory ? "Directory" : "File",
            ["Exists"] = true
        };

        if (!isDirectory)
        {
            var fileInfo = new FileInfo(config.Path);
            details["Size"] = fileInfo.Length;
            details["LastModified"] = fileInfo.LastWriteTimeUtc;
        }

        return new ConnectionTestResult
        {
            Success = true,
            Message = "Successfully validated local file path",
            Details = details
        };
    }
}
