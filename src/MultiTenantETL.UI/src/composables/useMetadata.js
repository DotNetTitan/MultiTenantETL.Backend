/**
 * Composable for accessing application metadata
 * Provides centralized access to configuration options
 */

import { ref, computed, onMounted } from 'vue';
import { 
  getAllMetadata, 
  getConnectorConfig,
  getTransformationTypes,
  cacheMetadata,
  getCachedMetadata
} from '@/services/metadataService';

// Global state - shared across all component instances
const metadata = ref(null);
const loading = ref(false);
const error = ref(null);
const isInitialized = ref(false);

/**
 * Main composable for metadata access
 */
export function useMetadata() {
  /**
   * Load all metadata from API or cache
   */
  async function loadMetadata(forceRefresh = false) {
    // Don't reload if already initialized and not forcing refresh
    if (isInitialized.value && !forceRefresh) {
      return;
    }

    loading.value = true;
    error.value = null;

    try {
      // Try to get from cache first
      if (!forceRefresh) {
        const cached = getCachedMetadata();
        if (cached) {
          metadata.value = cached;
          isInitialized.value = true;
          loading.value = false;
          return;
        }
      }

      // Fetch from API
      const data = await getAllMetadata();
      metadata.value = data;
      isInitialized.value = true;

      // Cache for future use
      cacheMetadata(data);
    } catch (err) {
      error.value = err.message || 'Failed to load metadata';
      console.error('Error loading metadata:', err);

      // Try to use cached data as fallback
      const cached = getCachedMetadata(Infinity); // Accept any age
      if (cached) {
        metadata.value = cached;
        isInitialized.value = true;
        console.warn('Using cached metadata due to API error');
      }
    } finally {
      loading.value = false;
    }
  }

  // Computed properties for easy access
  const connectorTypes = computed(() => 
    metadata.value?.connectorConfig?.types || []
  );

  const connectorProviders = computed(() => 
    metadata.value?.connectorConfig?.providers || {}
  );

  const directions = computed(() => 
    metadata.value?.connectorConfig?.directions || []
  );

  const authTypes = computed(() => 
    metadata.value?.connectorConfig?.authTypes || []
  );

  const fileFormats = computed(() => 
    metadata.value?.connectorConfig?.fileFormats || []
  );

  const writeOperations = computed(() => 
    metadata.value?.connectorConfig?.writeOperations || []
  );

  const httpMethods = computed(() => 
    metadata.value?.connectorConfig?.httpMethods || []
  );

  const transformationTypes = computed(() => 
    metadata.value?.transformationTypes || []
  );

  const dataTypes = computed(() => 
    metadata.value?.dataTypes || []
  );

  const scheduleFrequencies = computed(() => 
    metadata.value?.scheduleFrequencies || []
  );

  const daysOfWeek = computed(() => 
    metadata.value?.daysOfWeek || []
  );

  /**
   * Get providers for a specific connector type
   * @param {string} type - Connector type (Database, API, File)
   * @returns {Array} List of providers
   */
  function getProvidersForType(type) {
    return connectorProviders.value[type] || [];
  }

  /**
   * Get write operations that require primary key
   * @returns {Array} Write operations requiring primary key
   */
  function getWriteOperationsRequiringKey() {
    return writeOperations.value.filter(op => op.requiresPrimaryKey);
  }

  /**
   * Get transformation types by category
   * @param {string} category - Category name
   * @returns {Array} Transformation types in category
   */
  function getTransformationsByCategory(category) {
    return transformationTypes.value.filter(t => t.category === category);
  }

  /**
   * Get unique transformation categories
   * @returns {Array} List of categories
   */
  function getTransformationCategories() {
    const categories = new Set(
      transformationTypes.value.map(t => t.category)
    );
    return Array.from(categories);
  }

  return {
    // State
    metadata,
    loading,
    error,
    isInitialized,

    // Methods
    loadMetadata,
    getProvidersForType,
    getWriteOperationsRequiringKey,
    getTransformationsByCategory,
    getTransformationCategories,

    // Computed data
    connectorTypes,
    connectorProviders,
    directions,
    authTypes,
    fileFormats,
    writeOperations,
    httpMethods,
    transformationTypes,
    dataTypes,
    scheduleFrequencies,
    daysOfWeek
  };
}

/**
 * Initialize metadata on app startup
 * Call this in App.vue or main.js
 */
export async function initializeMetadata() {
  const { loadMetadata } = useMetadata();
  await loadMetadata();
}
