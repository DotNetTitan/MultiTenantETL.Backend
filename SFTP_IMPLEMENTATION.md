# SFTP Support Implementation

## Summary
Added full SFTP (SSH File Transfer Protocol) support alongside existing FTP support for secure file transfers.

## Backend Changes

### 1. NuGet Package Added
- **SSH.NET v2024.1.0** - SFTP client library for .NET

### 2. Updated Files

#### `MultiTenantETL.Infrastructure.csproj`
- Added SSH.NET package reference

#### `ConnectionTester.cs`
- Added `using Renci.SshNet;` for SFTP support
- Added `TestSftpConnectionAsync()` method that:
  - Validates SFTP host, username, and password
  - Connects to SFTP server on port 22 (default) or custom port
  - Tests if the specified path exists
  - Returns connection details including protocol version and server version
  - Properly handles errors and disconnection

#### `ConnectorDtos.cs` (FileConfig)
- Added SFTP-specific fields:
  - `SftpHost` - SFTP server hostname
  - `SftpPort` - SFTP port (default: 22)
  - `SftpUsername` - SFTP username
  - `SftpPassword` - SFTP password

## Frontend Changes

### 1. Updated Files

#### `metadataService.js`
- Added 'SFTP' to File providers list: `['Local', 'FTP', 'SFTP', 'S3', 'AzureBlob']`

#### `ConnectorWizard.vue`
- Added SFTP configuration template section with fields:
  - SFTP Host (required)
  - Port (default: 22)
  - Username (required)
  - Password (required)
  - File Path (required)
- Updated `getDefaultConfig()` to include SFTP fields initialization

#### `en.json` (Localization)
- Added translation keys:
  - `sftpHost`: "SFTP Host"
  - `sftpHostPlaceholder`: "sftp.example.com"
  - `ftpHost`: "FTP Host" (added for consistency)

## How It Works

### Connection Test Flow
1. User selects "SFTP" as provider in connector wizard
2. Fills in SFTP host, port (optional, defaults to 22), username, password, and file path
3. Clicks "Test Connection"
4. Backend creates `SftpClient` with provided credentials
5. Attempts to connect to SFTP server
6. Verifies path exists (if provided)
7. Returns success/failure with connection details

### Key Differences: FTP vs SFTP
- **FTP**: Port 21, uses FluentFTP library, less secure
- **SFTP**: Port 22, uses SSH.NET library, encrypted SSH protocol, more secure

## Testing

To test SFTP connection:
1. Navigate to Connectors → Create New Connector
2. Select Type: "File"
3. Select Provider: "SFTP"
4. Fill in:
   - SFTP Host: your-sftp-server.com
   - Port: 22 (or custom)
   - Username: your-username
   - Password: your-password
   - File Path: /path/to/file.csv
5. Click "Test Connection"
6. Should see success message with server details

## Next Steps (Optional Enhancements)
- Add SSH key-based authentication support
- Add known_hosts verification
- Add connection timeout configuration
- Add support for SFTP-specific options (compression, cipher selection)
