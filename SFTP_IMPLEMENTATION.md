# SFTP & FTP Data Reader/Writer Implementation

## Summary
Completed full SFTP and FTP support for both reading and writing data files. Both protocols now support CSV, JSON, and JSONL formats with automatic format detection.

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

## Data Reader/Writer Implementation

### New Classes Added

#### SFTP Support
- **`SftpDataReader.cs`** - Reads files from SFTP servers
  - Downloads file to memory stream
  - Delegates to format-specific readers (CSV, JSON, JSONL)
  - Supports schema detection
  - Connection testing
  
- **`SftpDataWriter.cs`** - Writes data to SFTP servers
  - Buffers data to memory stream
  - Uploads on disposal
  - Supports CSV and JSONL formats
  - Automatic format detection from file extension

#### FTP Support
- **`FtpDataReader.cs`** - Reads files from FTP servers
  - Uses FluentFTP async client
  - Downloads file to memory stream
  - Delegates to format-specific readers
  - Supports schema detection
  - Connection testing

- **`FtpDataWriter.cs`** - Writes data to FTP servers
  - Buffers data to memory stream
  - Uploads on disposal with overwrite
  - Supports CSV and JSONL formats
  - Automatic format detection from file extension

### Factory Integration

#### `FileDataReaderFactory.cs`
- Added SFTP and FTP reader injection
- Updated `CreateReader()` to return appropriate reader based on provider
- SFTP and FTP readers handle their own format detection

#### `FileDataWriterFactory.cs`
- Added SFTP and FTP writer injection
- Updated `CreateWriter()` to return appropriate writer based on provider
- SFTP and FTP writers handle their own format detection

### Dependency Injection (Program.cs)
- Registered `SftpDataReader` as scoped service
- Registered `FtpDataReader` as scoped service
- Registered `SftpDataWriter` as scoped service
- Registered `FtpDataWriter` as scoped service

## Configuration Format

### SFTP Config
```json
{
  "Host": "sftp.example.com",
  "Port": 22,
  "Username": "user",
  "Password": "pass",
  "FilePath": "/path/to/file.csv",
  "Format": "csv"  // optional, auto-detected from extension
}
```

### FTP Config
```json
{
  "Host": "ftp.example.com",
  "Port": 21,
  "Username": "user",
  "Password": "pass",
  "FilePath": "/path/to/file.csv",
  "Format": "csv"  // optional, auto-detected from extension
}
```

## Supported Formats
- **CSV** - Comma-separated values with header detection
- **JSON** - JSON arrays (read only, JSONL recommended for write)
- **JSONL/NDJSON** - JSON Lines (newline-delimited JSON)

## How It Works

### Reading Flow
1. Factory creates appropriate reader based on connector provider
2. Reader connects to SFTP/FTP server
3. Downloads file to memory stream
4. Determines format from config or file extension
5. Delegates to format-specific reader (CsvDataReader, JsonDataReader, JsonLinesDataReader)
6. Streams data in batches
7. Disconnects on completion

### Writing Flow
1. Factory creates appropriate writer based on connector provider
2. Writer buffers data to memory stream
3. Formats data as CSV or JSONL
4. On disposal, connects to SFTP/FTP server
5. Uploads buffered file
6. Disconnects

## Build Status
✅ Build succeeded with 44 warnings (all pre-existing nullable reference warnings)

## Next Steps (Optional Enhancements)
- Add SSH key-based authentication for SFTP
- Add known_hosts verification for SFTP
- Add connection timeout configuration
- Add support for SFTP-specific options (compression, cipher selection)
- Add FTP SSL/TLS support (FTPS)
- Add progress reporting for large file transfers
