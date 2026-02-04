/**
 * Metadata Service
 * Provides configuration metadata for the application
 * 
 * API Endpoint: GET /api/metadata/*
 */

import axios from './api';

// Mock flag - set to false when backend is ready
const USE_MOCK_DATA = false;

// Mock metadata - simulates backend response
const MOCK_METADATA = {
  connectorConfig: {
    types: [
      { value: 'Database', labelKey: 'connectors.database', icon: 'mdi-database' },
      { value: 'API', labelKey: 'connectors.api', icon: 'mdi-api' },
      { value: 'File', labelKey: 'connectors.file', icon: 'mdi-file-document' }
    ],

    providers: {
      Database: ['SqlServer', 'PostgreSQL', 'MySQL', 'Snowflake', 'BigQuery', 'Redshift', 'MongoDb', 'CosmosDb'],
      API: ['REST'],
      File: ['Local', 'FTP', 'SFTP', 'S3', 'AzureBlob', 'GCS']
    },

    providerMetadata: {
      SqlServer: { icon: 'mdi-microsoft-sql-server', color: '#CC2927' },
      PostgreSQL: { icon: 'mdi-database', color: '#336791' },
      MySQL: { icon: 'mdi-database', color: '#4479A1' },
      Snowflake: { icon: 'mdi-snowflake', color: '#00B0FF' },
      Snoflake: { icon: 'mdi-snowflake', color: '#00B0FF' }, // Alias for typo
      BigQuery: { icon: 'mdi-google-cloud', color: '#4285F4' },
      Redshift: { icon: 'mdi-database', color: '#FF9900' },
      MongoDb: { icon: 'mdi-database', color: '#4DB33D' },
      CosmosDb: { icon: 'mdi-infinity', color: '#44ACE1' },
      REST: { icon: 'mdi-api', color: '#FFB300' },
      Local: { icon: 'mdi-folder', color: '#757575' },
      FTP: { icon: 'mdi-server-network', color: '#5C6BC0' },
      SFTP: { icon: 'mdi-server-network', color: '#3F51B5' },
      S3: { icon: 'mdi-aws', color: '#FF9900' },
      AzureBlob: { icon: 'mdi-microsoft-azure', color: '#0089D6' },
      GCS: { icon: 'mdi-google-cloud', color: '#FBBC04' }
    },

    directions: [
      { value: 'source', labelKey: 'connectors.sourceOnly', icon: 'mdi-export' },
      { value: 'destination', labelKey: 'connectors.destinationOnly', icon: 'mdi-import' },
      { value: 'both', labelKey: 'connectors.both', icon: 'mdi-swap-horizontal' }
    ],

    authTypes: [
      { value: 'None', labelKey: 'connectors.authNone' },
      { value: 'Basic', labelKey: 'connectors.authBasic' },
      { value: 'Bearer', labelKey: 'connectors.authBearer' },
      { value: 'ApiKey', labelKey: 'connectors.authApiKey' }
    ],

    fileFormats: [
      { value: 'CSV', labelKey: 'common.csv', extension: '.csv' },
      { value: 'JSON', labelKey: 'common.json', extension: '.json' },
      { value: 'Excel', labelKey: 'common.excel', extension: '.xlsx' },
      { value: 'XML', labelKey: 'common.xml', extension: '.xml' },
      { value: 'Parquet', labelKey: 'common.parquet', extension: '.parquet' }
    ],

    writeOperations: [
      {
        value: 'INSERT',
        labelKey: 'connectors.writeOperationInsert',
        descriptionKey: 'connectors.insertDescription',
        requiresPrimaryKey: false
      },
      {
        value: 'UPDATE',
        labelKey: 'connectors.writeOperationUpdate',
        descriptionKey: 'connectors.updateDescription',
        requiresPrimaryKey: true
      },
      {
        value: 'UPSERT',
        labelKey: 'connectors.writeOperationUpsert',
        descriptionKey: 'connectors.upsertDescription',
        requiresPrimaryKey: true
      },
      {
        value: 'BULK_INSERT',
        labelKey: 'connectors.writeOperationBulkInsert',
        descriptionKey: 'connectors.bulkInsertDescription',
        requiresPrimaryKey: false
      }
    ],

    httpMethods: [
      { value: 'GET', labelKey: 'common.httpGet', color: 'success' },
      { value: 'POST', labelKey: 'common.httpPost', color: 'primary' },
      { value: 'PUT', labelKey: 'common.httpPut', color: 'warning' },
      { value: 'PATCH', labelKey: 'common.httpPatch', color: 'info' },
      { value: 'DELETE', labelKey: 'common.httpDelete', color: 'error' }
    ]
  },

  transformationTypes: [
    {
      value: 'Filter',
      labelKey: 'transformations.filter',
      icon: 'mdi-filter',
      categoryKey: 'transformations.categoryDataQuality',
      descriptionKey: 'transformations.filterDescription'
    },
    {
      value: 'Map',
      labelKey: 'transformations.map',
      icon: 'mdi-map',
      categoryKey: 'transformations.categoryTransformation',
      descriptionKey: 'transformations.mapDescription'
    },
    {
      value: 'Script',
      labelKey: 'transformations.script',
      icon: 'mdi-code-braces',
      categoryKey: 'transformations.categoryCustom',
      descriptionKey: 'transformations.scriptDescription'
    },
    {
      value: 'Trim',
      labelKey: 'transformations.trim',
      icon: 'mdi-content-cut',
      categoryKey: 'transformations.categoryText',
      descriptionKey: 'transformations.trimDescription'
    },
    {
      value: 'CaseConvert',
      labelKey: 'transformations.case',
      icon: 'mdi-format-letter-case',
      categoryKey: 'transformations.categoryText',
      descriptionKey: 'transformations.caseDescription'
    },
    {
      value: 'Substring',
      labelKey: 'transformations.substring',
      icon: 'mdi-text-box-outline',
      categoryKey: 'transformations.categoryText',
      descriptionKey: 'transformations.substringDescription'
    },
    {
      value: 'Replace',
      labelKey: 'transformations.replace',
      icon: 'mdi-find-replace',
      categoryKey: 'transformations.categoryText',
      descriptionKey: 'transformations.replaceDescription'
    }
  ],

  dataTypes: [
    // Fallback mock data - will be replaced by backend API
    { value: 'varchar', label: 'String (Varchar)', icon: 'mdi-text', category: 'string' },
    { value: 'int', label: 'Integer', icon: 'mdi-numeric', category: 'numeric' },
    { value: 'bigint', label: 'Big Integer', icon: 'mdi-numeric', category: 'numeric' },
    { value: 'decimal', label: 'Decimal', icon: 'mdi-decimal', category: 'numeric' },
    { value: 'boolean', label: 'Boolean', icon: 'mdi-checkbox-marked', category: 'boolean' },
    { value: 'date', label: 'Date', icon: 'mdi-calendar', category: 'datetime' },
    { value: 'datetime', label: 'Date Time', icon: 'mdi-calendar-clock', category: 'datetime' },
    { value: 'timestamp', label: 'Timestamp', icon: 'mdi-clock', category: 'datetime' },
    { value: 'uuid', label: 'UUID/GUID', icon: 'mdi-identifier', category: 'identifier' },
    { value: 'json', label: 'JSON', icon: 'mdi-code-json', category: 'structured' },
    { value: 'text', label: 'Text (Long)', icon: 'mdi-text-long', category: 'string' }
  ],

  scheduleFrequencies: [
    { value: 'daily', labelKey: 'pipelines.daily', icon: 'mdi-calendar-today' },
    { value: 'weekly', labelKey: 'pipelines.weekly', icon: 'mdi-calendar-week' },
    { value: 'monthly', labelKey: 'pipelines.monthly', icon: 'mdi-calendar-month' },
    { value: 'custom', labelKey: 'pipelines.custom', icon: 'mdi-cog' }
  ],

  daysOfWeek: [
    { value: 'monday', labelKey: 'pipelines.monday', shortKey: 'common.mon' },
    { value: 'tuesday', labelKey: 'pipelines.tuesday', shortKey: 'common.tue' },
    { value: 'wednesday', labelKey: 'pipelines.wednesday', shortKey: 'common.wed' },
    { value: 'thursday', labelKey: 'pipelines.thursday', shortKey: 'common.thu' },
    { value: 'friday', labelKey: 'pipelines.friday', shortKey: 'common.fri' },
    { value: 'saturday', labelKey: 'pipelines.saturday', shortKey: 'common.sat' },
    { value: 'sunday', labelKey: 'pipelines.sunday', shortKey: 'common.sun' }
  ]
};

/**
 * Simulates API delay for realistic testing
 */
const delay = (ms = 100) => new Promise(resolve => setTimeout(resolve, ms));

/**
 * Get connector configuration metadata
 * @returns {Promise<Object>} Connector configuration
 */
export async function getConnectorConfig() {
  if (USE_MOCK_DATA) {
    await delay(100);
    return MOCK_METADATA.connectorConfig;
  }

  const { data } = await axios.get('/api/metadata/connector-config');
  return data;
}

/**
 * Get transformation types metadata
 * @returns {Promise<Array>} Transformation types
 */
export async function getTransformationTypes() {
  if (USE_MOCK_DATA) {
    await delay(100);
    return MOCK_METADATA.transformationTypes;
  }

  const { data } = await axios.get('/api/metadata/transformation-types');
  return data;
}

/**
 * Get data types metadata
 * @returns {Promise<Array>} Data types
 */
export async function getDataTypes() {
  if (USE_MOCK_DATA) {
    await delay(50);
    return MOCK_METADATA.dataTypes;
  }

  const { data } = await axios.get('/api/metadata/data-types');
  return data;
}

/**
 * Get schedule frequencies metadata
 * @returns {Promise<Array>} Schedule frequencies
 */
export async function getScheduleFrequencies() {
  if (USE_MOCK_DATA) {
    await delay(50);
    return MOCK_METADATA.scheduleFrequencies;
  }

  const { data } = await axios.get('/api/metadata/schedule-frequencies');
  return data;
}

/**
 * Get days of week metadata
 * @returns {Promise<Array>} Days of week
 */
export async function getDaysOfWeek() {
  if (USE_MOCK_DATA) {
    await delay(50);
    return MOCK_METADATA.daysOfWeek;
  }

  const { data } = await axios.get('/api/metadata/days-of-week');
  return data;
}

/**
 * Get all metadata at once (for initial app load)
 * @returns {Promise<Object>} All metadata
 */
export async function getAllMetadata() {
  if (USE_MOCK_DATA) {
    await delay(200);
    return MOCK_METADATA;
  }

  const { data } = await axios.get('/api/metadata/all');
  return data;
}

/**
 * Cache metadata in localStorage
 * @param {Object} metadata - Metadata to cache
 */
export function cacheMetadata(metadata) {
  try {
    localStorage.setItem('app-metadata', JSON.stringify(metadata));
    localStorage.setItem('app-metadata-timestamp', Date.now().toString());
  } catch (error) {
    // Failed to cache metadata
  }
}

/**
 * Get cached metadata from localStorage
 * @param {number} maxAge - Maximum age in milliseconds (default: 1 hour)
 * @returns {Object|null} Cached metadata or null if expired/not found
 */
export function getCachedMetadata(maxAge = 3600000) {
  try {
    const cached = localStorage.getItem('app-metadata');
    const timestamp = localStorage.getItem('app-metadata-timestamp');

    if (!cached || !timestamp) return null;

    const age = Date.now() - parseInt(timestamp);
    if (age > maxAge) {
      // Cache expired
      localStorage.removeItem('app-metadata');
      localStorage.removeItem('app-metadata-timestamp');
      return null;
    }

    return JSON.parse(cached);
  } catch (error) {
    return null;
  }
}
