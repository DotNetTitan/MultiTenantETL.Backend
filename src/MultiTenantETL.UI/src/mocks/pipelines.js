// Mock data for pipelines and executions
export const mockPipelines = [
  {
    id: '1',
    name: 'Sales Data ETL',
    description: 'Extract sales data from SQL Server, transform, and load to data warehouse',
    sourceName: 'SQL Server - Sales',
    destinationName: 'Data Warehouse',
    status: 'Idle',
    sourceId: '1',
    destinationId: '5',
    transformationIds: ['1', '4'],
    transformations: [],
    fieldMappings: [
      {
        id: '1',
        sourceFields: ['OrderId'],
        destinationField: 'SaleId',
        transformations: []
      },
      {
        id: '2',
        sourceFields: ['CustomerId'],
        destinationField: 'ProductId',
        transformations: []
      },
      {
        id: '3',
        sourceFields: ['OrderDate'],
        destinationField: 'SaleDate',
        transformations: []
      },
      {
        id: '4',
        sourceFields: ['TotalAmount'],
        destinationField: 'Amount',
        transformations: [{ transformationId: '1' }]
      }
    ],
    isScheduled: true,
    schedule: {
      frequency: 'Daily',
      time: '02:00',
      cronExpression: '0 2 * * *',
      timezone: 'UTC'
    },
    createdAt: new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString(),
    lastRunAt: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString()
  },
  {
    id: '2',
    name: 'Customer Import',
    description: 'Import customer data from CSV files',
    sourceName: 'SFTP - Customer Files',
    destinationName: 'Customer Database',
    status: 'Idle',
    sourceId: '2',
    destinationId: '6',
    transformationIds: ['2', '3'],
    transformations: [],
    fieldMappings: [
      {
        id: '1',
        sourceFields: ['customer_id'],
        destinationField: 'customer_id',
        transformations: []
      },
      {
        id: '2',
        sourceFields: ['first_name'],
        destinationField: 'first_name',
        transformations: [{ transformationId: '4' }]
      },
      {
        id: '3',
        sourceFields: ['last_name'],
        destinationField: 'last_name',
        transformations: [{ transformationId: '4' }]
      },
      {
        id: '4',
        sourceFields: ['email'],
        destinationField: 'email',
        transformations: [{ transformationId: '5' }]
      },
      {
        id: '5',
        sourceFields: ['phone'],
        destinationField: 'phone',
        transformations: []
      }
    ],
    isScheduled: true,
    schedule: {
      frequency: 'Weekly',
      time: '04:30',
      cronExpression: '30 4 * * 1',
      timezone: 'America/New_York'
    },
    createdAt: new Date(Date.now() - 45 * 24 * 60 * 60 * 1000).toISOString(),
    lastRunAt: new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString()
  },
  {
    id: '3',
    name: 'Product Sync',
    description: 'Sync product data between systems',
    sourceName: 'ERP API',
    destinationName: 'E-commerce Platform',
    status: 'Running',
    sourceId: '3',
    destinationId: '7',
    transformationIds: ['5'],
    transformations: [],
    fieldMappings: [
      {
        id: '1',
        sourceFields: ['id'],
        destinationField: 'product_id',
        transformations: []
      },
      {
        id: '2',
        sourceFields: ['product_code'],
        destinationField: 'sku',
        transformations: [{ transformationId: '5' }]
      },
      {
        id: '3',
        sourceFields: ['quantity'],
        destinationField: 'stock_quantity',
        transformations: []
      },
      {
        id: '4',
        sourceFields: ['price'],
        destinationField: 'unit_price',
        transformations: []
      }
    ],
    isScheduled: false,
    schedule: null,
    createdAt: new Date(Date.now() - 60 * 24 * 60 * 60 * 1000).toISOString(),
    lastRunAt: new Date(Date.now() - 10 * 60 * 1000).toISOString()
  },
  {
    id: '4',
    name: 'Analytics Export',
    description: 'Export analytics data to reporting system',
    sourceName: 'Analytics DB',
    destinationName: 'Reporting System',
    status: 'Failed',
    sourceId: '4',
    destinationId: '8',
    transformationIds: [],
    transformations: [],
    fieldMappings: [
      {
        id: '1',
        sourceFields: ['metric_id'],
        destinationField: 'id',
        transformations: []
      },
      {
        id: '2',
        sourceFields: ['metric_name'],
        destinationField: 'name',
        transformations: []
      },
      {
        id: '3',
        sourceFields: ['metric_value'],
        destinationField: 'value',
        transformations: []
      },
      {
        id: '4',
        sourceFields: ['recorded_at'],
        destinationField: 'timestamp',
        transformations: []
      }
    ],
    isScheduled: true,
    schedule: {
      frequency: 'Daily',
      time: '01:00',
      cronExpression: '0 1 * * *',
      timezone: 'UTC'
    },
    createdAt: new Date(Date.now() - 15 * 24 * 60 * 60 * 1000).toISOString(),
    lastRunAt: new Date(Date.now() - 12 * 60 * 60 * 1000).toISOString()
  },
  {
    id: '5',
    name: 'Monthly Financial Report',
    description: 'Generate monthly financial reports from Oracle ERP',
    sourceName: 'Oracle ERP Database',
    destinationName: 'AWS S3 Bucket',
    status: 'Idle',
    sourceId: '9',
    destinationId: '11',
    transformationIds: ['11'],
    transformations: [],
    fieldMappings: [
      {
        id: '1',
        sourceFields: ['ItemId'],
        destinationField: 'item_id',
        transformations: []
      },
      {
        id: '2',
        sourceFields: ['ItemName'],
        destinationField: 'item_name',
        transformations: [{ transformationId: '11' }]
      },
      {
        id: '3',
        sourceFields: ['Quantity'],
        destinationField: 'quantity',
        transformations: []
      }
    ],
    isScheduled: true,
    schedule: {
      frequency: 'Monthly',
      dayOfMonth: 1,
      time: '00:00',
      cronExpression: '0 0 1 * *',
      timezone: 'America/Los_Angeles'
    },
    createdAt: new Date(Date.now() - 120 * 24 * 60 * 60 * 1000).toISOString(),
    lastRunAt: new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString()
  }
];

export const mockExecutions = [
  {
    id: '1',
    pipelineId: '1',
    pipelineName: 'Sales Data ETL',
    status: 'Completed',
    startTime: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000).toISOString(),
    endTime: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000 + 5 * 60 * 1000).toISOString(),
    recordsProcessed: 12345,
    errors: [],
    logs: [
      { timestamp: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000).toISOString(), level: 'INFO', message: 'Pipeline execution started' },
      { timestamp: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000 + 1 * 60 * 1000).toISOString(), level: 'INFO', message: 'Connected to SQL Server - Sales' },
      { timestamp: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000 + 2 * 60 * 1000).toISOString(), level: 'INFO', message: 'Extracted 12345 records' },
      { timestamp: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000 + 3 * 60 * 1000).toISOString(), level: 'INFO', message: 'Applied transformations' },
      { timestamp: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000 + 4 * 60 * 1000).toISOString(), level: 'INFO', message: 'Loaded 12345 records to Data Warehouse' },
      { timestamp: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000 + 5 * 60 * 1000).toISOString(), level: 'INFO', message: 'Pipeline execution completed successfully' }
    ]
  },
  {
    id: '2',
    pipelineId: '2',
    pipelineName: 'Customer Import',
    status: 'Running',
    startTime: new Date(Date.now() - 15 * 60 * 1000).toISOString(),
    endTime: null,
    recordsProcessed: 5000,
    errors: [],
    logs: [
      { timestamp: new Date(Date.now() - 15 * 60 * 1000).toISOString(), level: 'INFO', message: 'Pipeline execution started' },
      { timestamp: new Date(Date.now() - 13 * 60 * 1000).toISOString(), level: 'INFO', message: 'Connected to SFTP - Customer Files' },
      { timestamp: new Date(Date.now() - 10 * 60 * 1000).toISOString(), level: 'INFO', message: 'Extracting customer data from CSV' },
      { timestamp: new Date(Date.now() - 7 * 60 * 1000).toISOString(), level: 'INFO', message: 'Processing batch 1/3 - 5000 records processed' },
      { timestamp: new Date(Date.now() - 3 * 60 * 1000).toISOString(), level: 'INFO', message: 'Applying transformations to batch 2/3' }
    ]
  },
  {
    id: '3',
    pipelineId: '3',
    pipelineName: 'Product Sync',
    status: 'Failed',
    startTime: new Date(Date.now() - 3 * 60 * 60 * 1000).toISOString(),
    endTime: new Date(Date.now() - 3 * 60 * 60 * 1000 + 3 * 60 * 1000).toISOString(),
    recordsProcessed: 0,
    errors: ['Connection timeout: Unable to connect to E-commerce Platform API', 'Retry attempts exhausted'],
    logs: [
      { timestamp: new Date(Date.now() - 3 * 60 * 60 * 1000).toISOString(), level: 'INFO', message: 'Pipeline execution started' },
      { timestamp: new Date(Date.now() - 3 * 60 * 60 * 1000 + 1 * 60 * 1000).toISOString(), level: 'INFO', message: 'Connected to ERP API' },
      { timestamp: new Date(Date.now() - 3 * 60 * 60 * 1000 + 2 * 60 * 1000).toISOString(), level: 'ERROR', message: 'Connection timeout: Unable to connect to E-commerce Platform API' },
      { timestamp: new Date(Date.now() - 3 * 60 * 60 * 1000 + 2.5 * 60 * 1000).toISOString(), level: 'WARN', message: 'Retrying connection (attempt 1/3)...' },
      { timestamp: new Date(Date.now() - 3 * 60 * 60 * 1000 + 2.7 * 60 * 1000).toISOString(), level: 'WARN', message: 'Retrying connection (attempt 2/3)...' },
      { timestamp: new Date(Date.now() - 3 * 60 * 60 * 1000 + 2.9 * 60 * 1000).toISOString(), level: 'WARN', message: 'Retrying connection (attempt 3/3)...' },
      { timestamp: new Date(Date.now() - 3 * 60 * 60 * 1000 + 3 * 60 * 1000).toISOString(), level: 'ERROR', message: 'Retry attempts exhausted' },
      { timestamp: new Date(Date.now() - 3 * 60 * 60 * 1000 + 3 * 60 * 1000).toISOString(), level: 'ERROR', message: 'Pipeline execution failed' }
    ]
  }
];
