// Mock data for connectors
export const mockConnectors = [
  {
    id: '1',
    name: 'SQL Server - Sales',
    description: 'Main sales database',
    type: 'Database',
    provider: 'SQL Server',
    direction: 'both',
    createdAt: new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString(),
    config: {
      server: 'sales-db.example.com',
      port: '1433',
      database: 'SalesDB',
      username: 'sa',
      password: '***',
      writeConfig: {
        tableName: 'Orders',
        operation: 'UPSERT',
        primaryKeys: ['OrderId'],
        batchSize: 1000
      }
    },
    database: {
      provider: 'SQL Server',
      server: 'sales-db.example.com',
      port: '1433',
      databaseName: 'SalesDB'
    },
    isSource: true,
    isDestination: true,
    requiresCredentials: true,
    schema: {
      fields: [
        { name: 'OrderId', type: 'int', required: true, nullable: false, description: 'Unique order identifier' },
        { name: 'CustomerId', type: 'int', required: true, nullable: false, description: 'Customer reference' },
        { name: 'OrderDate', type: 'datetime', required: true, nullable: false, description: 'Date order was placed' },
        { name: 'TotalAmount', type: 'decimal', length: '18,2', required: true, nullable: false, description: 'Total order amount' },
        { name: 'Status', type: 'varchar', length: '50', required: true, nullable: false, description: 'Order status' }
      ],
      version: 1,
      isManual: true,
      lastModified: new Date(Date.now() - 5 * 24 * 60 * 60 * 1000).toISOString()
    }
  },
  {
    id: '2',
    name: 'SFTP - Customer Files',
    description: 'SFTP server containing customer data files',
    type: 'File',
    provider: 'SFTP',
    direction: 'source',
    createdAt: new Date(Date.now() - 25 * 24 * 60 * 60 * 1000).toISOString(),
    config: {
      format: 'CSV',
      path: '/customers/data',
      delimiter: ',',
      hasHeader: true
    },
    file: {
      storageType: 'SFTP',
      path: '/customers/data',
      fileType: 'CSV',
      delimiter: ','
    },
    isSource: true,
    isDestination: false,
    requiresCredentials: true,
    schema: {
      fields: [
        { name: 'customer_id', type: 'string', required: true, nullable: false, description: 'Customer ID' },
        { name: 'first_name', type: 'string', required: true, nullable: false, description: 'First name' },
        { name: 'last_name', type: 'string', required: true, nullable: false, description: 'Last name' },
        { name: 'email', type: 'string', required: true, nullable: false, description: 'Email address' },
        { name: 'phone', type: 'string', required: false, nullable: true, description: 'Phone number' }
      ],
      version: 1,
      isManual: true,
      lastModified: new Date(Date.now() - 10 * 24 * 60 * 60 * 1000).toISOString()
    }
  },
  {
    id: '3',
    name: 'ERP API',
    description: 'REST API for the ERP system',
    type: 'API',
    provider: 'REST',
    direction: 'both',
    createdAt: new Date(Date.now() - 20 * 24 * 60 * 60 * 1000).toISOString(),
    config: {
      url: 'https://erp.example.com/api/v1',
      authType: 'Bearer',
      token: '***',
      endpoints: [
        { id: '1', method: 'GET', path: '/products', responseDataPath: 'data' },
        { id: '2', method: 'POST', path: '/products', requestDataPath: 'product', responseDataPath: 'data' }
      ],
      writeConfig: {
        requestFormat: 'JSON',
        wrapInArray: false,
        rootKey: 'product',
        batchSize: 100
      }
    },
    api: {
      baseUrl: 'https://erp.example.com/api/v1',
      authType: 'Bearer Token',
      dataFormat: 'JSON'
    },
    isSource: true,
    isDestination: true,
    requiresCredentials: true,
    schema: {
      fields: [
        { name: 'id', type: 'integer', required: true, nullable: false, description: 'Record ID' },
        { name: 'product_code', type: 'string', required: true, nullable: false, description: 'Product code' },
        { name: 'quantity', type: 'integer', required: true, nullable: false, description: 'Quantity in stock' },
        { name: 'price', type: 'decimal', required: true, nullable: false, description: 'Unit price' },
        { name: 'last_updated', type: 'timestamp', required: false, nullable: true, description: 'Last update timestamp' }
      ],
      version: 1,
      isManual: false,
      lastModified: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000).toISOString()
    }
  },
  {
    id: '4',
    name: 'Analytics DB',
    description: 'PostgreSQL database for analytics data',
    type: 'Database',
    createdAt: new Date(Date.now() - 15 * 24 * 60 * 60 * 1000).toISOString(),
    database: {
      provider: 'PostgreSQL',
      server: 'analytics-db.example.com',
      port: '5432',
      databaseName: 'analytics'
    },
    isSource: true,
    isDestination: true,
    requiresCredentials: true
  },
  {
    id: '5',
    name: 'Data Warehouse',
    description: 'Central data warehouse',
    type: 'Database',
    provider: 'SQL Server',
    direction: 'both',
    createdAt: new Date(Date.now() - 10 * 24 * 60 * 60 * 1000).toISOString(),
    config: {
      server: 'dw.example.com',
      port: '1433',
      database: 'DataWarehouse',
      username: 'dw_user',
      password: '***',
      writeConfig: {
        tableName: 'FactSales',
        operation: 'BULK_INSERT',
        batchSize: 5000
      }
    },
    database: {
      provider: 'SQL Server',
      server: 'dw.example.com',
      port: '1433',
      databaseName: 'DataWarehouse'
    },
    isSource: true,
    isDestination: true,
    requiresCredentials: true,
    schema: {
      fields: [
        { name: 'SaleId', type: 'bigInteger', required: true, nullable: false, description: 'Sale identifier' },
        { name: 'ProductId', type: 'integer', required: true, nullable: false, description: 'Product reference' },
        { name: 'SaleDate', type: 'date', required: true, nullable: false, description: 'Sale date' },
        { name: 'Amount', type: 'decimal', required: true, nullable: false, description: 'Sale amount' }
      ],
      version: 1,
      isManual: false,
      lastModified: new Date(Date.now() - 1 * 24 * 60 * 60 * 1000).toISOString()
    }
  },
  {
    id: '6',
    name: 'Customer Database',
    description: 'MySQL database for customer information',
    type: 'Database',
    provider: 'MySQL',
    direction: 'both',
    createdAt: new Date(Date.now() - 8 * 24 * 60 * 60 * 1000).toISOString(),
    config: {
      server: 'customer-db.example.com',
      port: '3306',
      database: 'customers',
      username: 'app_user',
      password: '***',
      writeConfig: {
        tableName: 'customers',
        operation: 'UPDATE',
        primaryKeys: ['customer_id'],
        batchSize: 500
      }
    },
    database: {
      provider: 'MySQL',
      server: 'customer-db.example.com',
      port: '3306',
      databaseName: 'customers'
    },
    isSource: true,
    isDestination: true,
    requiresCredentials: true,
    schema: {
      fields: [
        { name: 'customer_id', type: 'integer', required: true, nullable: false, description: 'Customer ID' },
        { name: 'first_name', type: 'string', required: true, nullable: false, description: 'First name' },
        { name: 'last_name', type: 'string', required: true, nullable: false, description: 'Last name' },
        { name: 'email', type: 'string', required: true, nullable: false, description: 'Email address' },
        { name: 'created_at', type: 'dateTime', required: true, nullable: false, description: 'Account creation date' }
      ],
      version: 1,
      isManual: false,
      lastModified: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000).toISOString()
    }
  },
  {
    id: '7',
    name: 'E-commerce Platform',
    description: 'REST API for e-commerce platform',
    type: 'API',
    createdAt: new Date(Date.now() - 5 * 24 * 60 * 60 * 1000).toISOString(),
    api: {
      baseUrl: 'https://ecommerce.example.com/api',
      authType: 'API Key',
      dataFormat: 'JSON'
    },
    isSource: false,
    isDestination: true,
    requiresCredentials: true
  },
  {
    id: '8',
    name: 'Reporting System',
    description: 'Data warehouse for reporting',
    type: 'Database',
    createdAt: new Date(Date.now() - 3 * 24 * 60 * 60 * 1000).toISOString(),
    database: {
      provider: 'SQL Server',
      server: 'reporting.example.com',
      port: '1433',
      databaseName: 'Reporting'
    },
    isSource: false,
    isDestination: true,
    requiresCredentials: true
  },
  {
    id: '9',
    name: 'Oracle ERP Database',
    description: 'Oracle database for enterprise resource planning',
    type: 'Database',
    provider: 'Oracle',
    direction: 'source',
    createdAt: new Date(Date.now() - 50 * 24 * 60 * 60 * 1000).toISOString(),
    database: {
      provider: 'Oracle',
      server: 'oracle-erp.example.com',
      port: '1521',
      databaseName: 'ORCL'
    },
    isSource: true,
    isDestination: false,
    requiresCredentials: true
  },
  {
    id: '10',
    name: 'Local CSV Files',
    description: 'Local file system for CSV data files',
    type: 'File',
    provider: 'Local',
    direction: 'both',
    createdAt: new Date(Date.now() - 40 * 24 * 60 * 60 * 1000).toISOString(),
    config: {
      format: 'CSV',
      path: 'C:/data/exports',
      delimiter: ',',
      hasHeader: true,
      writeConfig: {
        filenamePattern: 'export_{date}.csv',
        includeHeaders: true,
        columnOrder: ['id', 'name', 'value', 'date']
      }
    },
    file: {
      storageType: 'Local',
      path: 'C:/data/exports',
      fileType: 'CSV',
      delimiter: ','
    },
    isSource: true,
    isDestination: true,
    requiresCredentials: false,
    schema: {
      fields: [
        { name: 'id', type: 'integer', required: true, nullable: false, description: 'Record ID' },
        { name: 'name', type: 'string', required: true, nullable: false, description: 'Record name' },
        { name: 'value', type: 'decimal', required: false, nullable: true, description: 'Numeric value' },
        { name: 'date', type: 'date', required: true, nullable: false, description: 'Record date' }
      ],
      version: 1,
      isManual: true,
      lastModified: new Date(Date.now() - 14 * 24 * 60 * 60 * 1000).toISOString()
    }
  },
  {
    id: '11',
    name: 'AWS S3 Bucket',
    description: 'Amazon S3 bucket for data lake storage',
    type: 'File',
    provider: 'S3',
    direction: 'both',
    createdAt: new Date(Date.now() - 35 * 24 * 60 * 60 * 1000).toISOString(),
    config: {
      format: 'JSON',
      bucket: 'my-data-lake',
      region: 'us-east-1',
      path: '/raw-data'
    },
    file: {
      storageType: 'S3',
      path: '/raw-data',
      fileType: 'JSON'
    },
    isSource: true,
    isDestination: true,
    requiresCredentials: true
  },
  {
    id: '12',
    name: 'GraphQL API',
    description: 'GraphQL API for product catalog',
    type: 'API',
    provider: 'GraphQL',
    direction: 'source',
    createdAt: new Date(Date.now() - 28 * 24 * 60 * 60 * 1000).toISOString(),
    config: {
      url: 'https://catalog.example.com/graphql',
      authType: 'OAuth2',
      query: '{ products { id name price } }'
    },
    api: {
      baseUrl: 'https://catalog.example.com/graphql',
      authType: 'OAuth2',
      dataFormat: 'JSON'
    },
    isSource: true,
    isDestination: false,
    requiresCredentials: true
  },
  {
    id: '13',
    name: 'FTP Server - Reports',
    description: 'FTP server for report files',
    type: 'File',
    provider: 'FTP',
    direction: 'destination',
    createdAt: new Date(Date.now() - 45 * 24 * 60 * 60 * 1000).toISOString(),
    config: {
      format: 'Excel',
      ftpHost: 'ftp.reports.example.com',
      ftpPort: '21',
      ftpUsername: 'reports_user',
      ftpPassword: '***',
      path: '/reports/monthly',
      writeConfig: {
        filenamePattern: 'report_{date}.xlsx',
        sheetName: 'Data',
        startCell: 'A1'
      }
    },
    file: {
      storageType: 'FTP',
      path: '/reports/monthly',
      fileType: 'Excel'
    },
    isSource: false,
    isDestination: true,
    requiresCredentials: true,
    schema: {
      fields: [
        { name: 'ReportDate', type: 'date', required: true, nullable: false, description: 'Report date' },
        { name: 'Revenue', type: 'decimal', required: true, nullable: false, description: 'Total revenue' },
        { name: 'Orders', type: 'integer', required: true, nullable: false, description: 'Number of orders' }
      ],
      version: 1,
      isManual: true,
      lastModified: new Date(Date.now() - 7 * 24 * 60 * 60 * 1000).toISOString()
    }
  },
  {
    id: '14',
    name: 'Azure Blob Storage',
    description: 'Azure blob storage for data archives',
    type: 'File',
    provider: 'Azure Blob',
    direction: 'both',
    createdAt: new Date(Date.now() - 55 * 24 * 60 * 60 * 1000).toISOString(),
    config: {
      format: 'JSON',
      azureAccountName: 'mydatalake',
      azureContainer: 'archives',
      azureAccountKey: '***',
      path: '/data/exports',
      writeConfig: {
        filenamePattern: 'export_{timestamp}.json',
        structure: 'array'
      }
    },
    file: {
      storageType: 'Azure Blob',
      path: '/data/exports',
      fileType: 'JSON'
    },
    isSource: true,
    isDestination: true,
    requiresCredentials: true,
    schema: {
      fields: [
        { name: 'id', type: 'string', required: true, nullable: false, description: 'Record ID' },
        { name: 'data', type: 'json', required: true, nullable: false, description: 'JSON data' },
        { name: 'timestamp', type: 'timestamp', required: true, nullable: false, description: 'Record timestamp' }
      ],
      version: 1,
      isManual: false,
      lastModified: new Date(Date.now() - 3 * 24 * 60 * 60 * 1000).toISOString()
    }
  },
  {
    id: '15',
    name: 'Google Cloud Storage',
    description: 'GCS bucket for data lake',
    type: 'File',
    provider: 'Google Cloud Storage',
    direction: 'both',
    createdAt: new Date(Date.now() - 65 * 24 * 60 * 60 * 1000).toISOString(),
    config: {
      format: 'Parquet',
      gcsBucket: 'my-data-lake',
      gcsProjectId: 'my-project-123',
      gcsCredentials: '***',
      path: '/raw-data/events',
      writeConfig: {
        filenamePattern: 'events_{date}_{time}.parquet',
        compression: 'snappy'
      }
    },
    file: {
      storageType: 'Google Cloud Storage',
      path: '/raw-data/events',
      fileType: 'Parquet'
    },
    isSource: true,
    isDestination: true,
    requiresCredentials: true,
    schema: {
      fields: [
        { name: 'event_id', type: 'string', required: true, nullable: false, description: 'Event ID' },
        { name: 'event_type', type: 'string', required: true, nullable: false, description: 'Event type' },
        { name: 'user_id', type: 'string', required: false, nullable: true, description: 'User ID' },
        { name: 'event_data', type: 'json', required: false, nullable: true, description: 'Event payload' },
        { name: 'created_at', type: 'timestamp', required: true, nullable: false, description: 'Event timestamp' }
      ],
      version: 1,
      isManual: false,
      lastModified: new Date(Date.now() - 1 * 24 * 60 * 60 * 1000).toISOString()
    }
  },
  {
    id: '16',
    name: 'SOAP Web Service',
    description: 'Legacy SOAP API for inventory system',
    type: 'API',
    provider: 'SOAP',
    direction: 'source',
    createdAt: new Date(Date.now() - 75 * 24 * 60 * 60 * 1000).toISOString(),
    config: {
      url: 'https://inventory.example.com/soap/v1',
      authType: 'Basic',
      username: 'api_user',
      password: '***',
      wsdlUrl: 'https://inventory.example.com/soap/v1?wsdl'
    },
    api: {
      baseUrl: 'https://inventory.example.com/soap/v1',
      authType: 'Basic',
      dataFormat: 'XML'
    },
    isSource: true,
    isDestination: false,
    requiresCredentials: true,
    schema: {
      fields: [
        { name: 'ItemId', type: 'string', required: true, nullable: false, description: 'Item identifier' },
        { name: 'ItemName', type: 'string', required: true, nullable: false, description: 'Item name' },
        { name: 'Quantity', type: 'integer', required: true, nullable: false, description: 'Stock quantity' },
        { name: 'Location', type: 'string', required: false, nullable: true, description: 'Warehouse location' }
      ],
      version: 1,
      isManual: true,
      lastModified: new Date(Date.now() - 20 * 24 * 60 * 60 * 1000).toISOString()
    }
  },
  {
    id: '17',
    name: 'API Key Protected API',
    description: 'Third-party API with API key authentication',
    type: 'API',
    provider: 'REST',
    direction: 'source',
    createdAt: new Date(Date.now() - 33 * 24 * 60 * 60 * 1000).toISOString(),
    config: {
      url: 'https://api.thirdparty.com/v2',
      authType: 'API Key',
      apiKey: '***',
      apiKeyHeader: 'X-API-Key',
      endpoints: [
        { id: '1', method: 'GET', path: '/customers', responseDataPath: 'results' }
      ]
    },
    api: {
      baseUrl: 'https://api.thirdparty.com/v2',
      authType: 'API Key',
      dataFormat: 'JSON'
    },
    isSource: true,
    isDestination: false,
    requiresCredentials: true,
    schema: {
      fields: [
        { name: 'customer_id', type: 'string', required: true, nullable: false, description: 'Customer ID' },
        { name: 'company_name', type: 'string', required: true, nullable: false, description: 'Company name' },
        { name: 'contact_email', type: 'string', required: false, nullable: true, description: 'Contact email' }
      ],
      version: 1,
      isManual: false,
      lastModified: new Date(Date.now() - 4 * 24 * 60 * 60 * 1000).toISOString()
    }
  },
  {
    id: '18',
    name: 'XML Data Files',
    description: 'Local XML files for legacy system integration',
    type: 'File',
    provider: 'Local',
    direction: 'source',
    createdAt: new Date(Date.now() - 90 * 24 * 60 * 60 * 1000).toISOString(),
    config: {
      format: 'XML',
      path: 'C:/data/imports/xml',
      xmlRootElement: 'Records',
      xmlRecordElement: 'Record'
    },
    file: {
      storageType: 'Local',
      path: 'C:/data/imports/xml',
      fileType: 'XML'
    },
    isSource: true,
    isDestination: false,
    requiresCredentials: false,
    schema: {
      fields: [
        { name: 'RecordId', type: 'string', required: true, nullable: false, description: 'Record ID' },
        { name: 'RecordType', type: 'string', required: true, nullable: false, description: 'Record type' },
        { name: 'Data', type: 'textLong', required: false, nullable: true, description: 'Record data' }
      ],
      version: 1,
      isManual: true,
      lastModified: new Date(Date.now() - 60 * 24 * 60 * 60 * 1000).toISOString()
    }
  }
];