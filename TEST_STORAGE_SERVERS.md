# Test Storage Servers for Connector Testing

This Docker Compose setup provides FTP, SFTP, and S3-compatible storage servers for testing file connectors.

## Quick Start

### Start All Test Servers
```bash
docker-compose -f docker-compose.test-storage.yml up -d
```

### Stop All Test Servers
```bash
docker-compose -f docker-compose.test-storage.yml down
```

### Stop and Remove All Data
```bash
docker-compose -f docker-compose.test-storage.yml down -v
```

## Server Details

### 1. FTP Server
- **Host**: `localhost`
- **Port**: `21`
- **Username**: `testuser`
- **Password**: `testpass123`
- **Protocol**: FTP (Plain)
- **Test Path**: `/upload/test.csv`

**Test in Connector:**
```
Type: File
Provider: FTP
FTP Host: localhost
FTP Port: 21
Username: testuser
Password: testpass123
File Path: /upload/test.csv
```

### 2. SFTP Server
- **Host**: `localhost`
- **Port**: `2222`
- **Username**: `testuser`
- **Password**: `testpass123`
- **Protocol**: SFTP (SSH)
- **Test Path**: `/upload/test.csv`

**Test in Connector:**
```
Type: File
Provider: SFTP
SFTP Host: localhost
SFTP Port: 2222
Username: testuser
Password: testpass123
File Path: /upload/test.csv
```

### 3. MinIO (S3-Compatible)
- **Endpoint**: `http://localhost:9000`
- **Console**: `http://localhost:9001`
- **Access Key**: `minioadmin`
- **Secret Key**: `minioadmin123`
- **Region**: `us-east-1` (default)
- **Test Bucket**: `test-bucket`
- **Test File**: `data/sample.csv` (auto-created)

**Test in Connector:**
```
Type: File
Provider: S3
Bucket Name: test-bucket
Region: us-east-1
Access Key ID: minioadmin
Secret Access Key: minioadmin123
S3 Key (File Path): data/sample.csv
```

**MinIO Console Access:**
- URL: http://localhost:9001
- Username: minioadmin
- Password: minioadmin123

## Upload Test Files

### FTP
```bash
# Using curl
curl -T test.csv ftp://localhost:21/upload/ --user testuser:testpass123

# Using lftp
lftp -u testuser,testpass123 localhost -e "put test.csv; bye"
```

### SFTP
```bash
# Using sftp command
sftp -P 2222 testuser@localhost
# Password: testpass123
# Then: put test.csv upload/test.csv

# Using scp
scp -P 2222 test.csv testuser@localhost:/upload/test.csv
```

### MinIO (S3)
```bash
# Using AWS CLI (configure with endpoint)
aws configure set aws_access_key_id minioadmin
aws configure set aws_secret_access_key minioadmin123
aws --endpoint-url http://localhost:9000 s3 cp test.csv s3://test-bucket/data/test.csv

# Or use MinIO Console at http://localhost:9001
```

## Verify Servers are Running

```bash
# Check all containers
docker-compose -f docker-compose.test-storage.yml ps

# Check FTP
curl ftp://localhost:21 --user testuser:testpass123

# Check SFTP
ssh -p 2222 testuser@localhost
# Password: testpass123

# Check MinIO
curl http://localhost:9000/minio/health/live
```

## View Logs

```bash
# All services
docker-compose -f docker-compose.test-storage.yml logs -f

# Specific service
docker-compose -f docker-compose.test-storage.yml logs -f ftp
docker-compose -f docker-compose.test-storage.yml logs -f sftp
docker-compose -f docker-compose.test-storage.yml logs -f minio
```

## Troubleshooting

### FTP Connection Issues
- Make sure ports 21 and 21100-21110 are not in use
- Check Windows Firewall settings
- Try passive mode in your FTP client

### SFTP Connection Issues
- Port 2222 must be available
- On first connection, accept the host key
- Use password authentication (not key-based)

### MinIO/S3 Connection Issues
- Verify MinIO is healthy: `docker-compose -f docker-compose.test-storage.yml ps`
- Check MinIO logs: `docker-compose -f docker-compose.test-storage.yml logs minio`
- Access console at http://localhost:9001 to verify bucket exists

### Reset Everything
```bash
# Stop and remove all containers and volumes
docker-compose -f docker-compose.test-storage.yml down -v

# Start fresh
docker-compose -f docker-compose.test-storage.yml up -d
```

## Sample Test Data

The MinIO setup automatically creates a sample CSV file at `test-bucket/data/sample.csv` with this content:

```csv
id,name,email
1,John Doe,john@example.com
2,Jane Smith,jane@example.com
```

You can create similar files for FTP and SFTP testing.

## Production Notes

⚠️ **These servers are for TESTING ONLY!**
- Default credentials are insecure
- No SSL/TLS encryption (except SFTP)
- No authentication hardening
- Not suitable for production use

For production, use:
- Proper FTP/SFTP servers with SSL/TLS
- AWS S3 or Azure Blob Storage
- Strong passwords and key-based authentication
- Network security and firewall rules
