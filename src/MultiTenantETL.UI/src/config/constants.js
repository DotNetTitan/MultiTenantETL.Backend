/**
 * Application Constants
 * Fetched from backend on app initialization to avoid hardcoding
 */

import axios from '@/services/api'

export const AppConstants = {
  roles: null,
  oauthConfig: null,
  supportedLanguages: null,
  _initialized: false
}

export const ConnectorProviders = {
  SqlServer: 'SqlServer',
  PostgreSQL: 'PostgreSQL',
  MySQL: 'MySQL',
  Snowflake: 'Snowflake',
  BigQuery: 'BigQuery',
  Redshift: 'Redshift',
  MongoDb: 'MongoDb',
  CosmosDb: 'CosmosDb',
  REST: 'REST',
  Local: 'Local',
  FTP: 'FTP',
  SFTP: 'SFTP',
  S3: 'S3',
  AzureBlob: 'AzureBlob',
  GCS: 'GCS'
}

/**
 * Initialize constants from backend
 * Should be called once during app startup
 * @returns {Promise<void>}
 */
export async function initializeConstants() {
  if (AppConstants._initialized) {
    return
  }

  const { data } = await axios.get('/api/metadata/app-constants')

  // Handle both oAuthConfig (from backend) and oauthConfig (expected)
  const oauthConfig = data.oAuthConfig || data.oauthConfig

  // Validate response structure
  if (!data || !oauthConfig || !data.roles) {
    throw new Error('Invalid response structure from backend')
  }

  AppConstants.roles = data.roles
  AppConstants.oauthConfig = oauthConfig
  AppConstants.supportedLanguages = data.supportedLanguages
  AppConstants._initialized = true
}

/**
 * Get role constants
 * @returns {Object} Role names
 */
export function getRoles() {
  if (!AppConstants._initialized) {
    throw new Error('Constants not initialized. Call initializeConstants() first.')
  }
  return AppConstants.roles
}

/**
 * Get OAuth configuration
 * @returns {Object} OAuth config
 */
export function getOAuthConfig() {
  if (!AppConstants._initialized) {
    throw new Error('Constants not initialized. Call initializeConstants() first.')
  }

  if (!AppConstants.oauthConfig) {
    throw new Error('OAuth config is null/undefined. Backend returned invalid data.')
  }

  return AppConstants.oauthConfig
}

/**
 * Get supported languages
 * @returns {Array} Supported languages
 */
export function getSupportedLanguages() {
  if (!AppConstants._initialized) {
    throw new Error('Constants not initialized. Call initializeConstants() first.')
  }
  return AppConstants.supportedLanguages
}
