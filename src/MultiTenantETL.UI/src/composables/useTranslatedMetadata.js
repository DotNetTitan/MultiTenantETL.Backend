/**
 * Composable for accessing translated metadata
 * Automatically translates labelKey to current language
 */

import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import { useMetadata } from './useMetadata';

export function useTranslatedMetadata() {
  const { t } = useI18n();
  const metadata = useMetadata();

  /**
   * Translate an item with labelKey
   * @param {Object} item - Item with labelKey property
   * @returns {Object} Item with translated label
   */
  function translateItem(item) {
    if (!item) return item;
    
    const translated = { ...item };
    
    // Translate labelKey to label
    if (item.labelKey) {
      translated.label = t(item.labelKey);
    }
    
    // Translate descriptionKey to description
    if (item.descriptionKey) {
      translated.description = t(item.descriptionKey);
    }
    
    // Translate categoryKey to category
    if (item.categoryKey) {
      translated.category = t(item.categoryKey);
    }
    
    // Translate shortKey to short
    if (item.shortKey) {
      translated.short = t(item.shortKey);
    }
    
    return translated;
  }

  /**
   * Translate an array of items
   * @param {Array} items - Array of items with labelKey
   * @returns {Array} Array with translated labels
   */
  function translateItems(items) {
    if (!items || !Array.isArray(items)) return [];
    return items.map(translateItem);
  }

  // Computed properties with translations
  const connectorTypes = computed(() => 
    translateItems(metadata.connectorTypes.value)
  );

  const directions = computed(() => 
    translateItems(metadata.directions.value)
  );

  const authTypes = computed(() => 
    translateItems(metadata.authTypes.value)
  );

  const fileFormats = computed(() => 
    translateItems(metadata.fileFormats.value)
  );

  const writeOperations = computed(() => 
    translateItems(metadata.writeOperations.value)
  );

  const httpMethods = computed(() => 
    translateItems(metadata.httpMethods.value)
  );

  const transformationTypes = computed(() => 
    translateItems(metadata.transformationTypes.value)
  );

  const dataTypes = computed(() => 
    translateItems(metadata.dataTypes.value)
  );

  const scheduleFrequencies = computed(() => 
    translateItems(metadata.scheduleFrequencies.value)
  );

  const daysOfWeek = computed(() => 
    translateItems(metadata.daysOfWeek.value)
  );

  /**
   * Get translated providers for a specific type
   * @param {string} type - Connector type
   * @returns {Array} Array of provider strings (no translation needed)
   */
  function getProvidersForType(type) {
    return metadata.getProvidersForType(type);
  }

  /**
   * Get translated write operations that require primary key
   * @returns {Array} Translated write operations
   */
  function getWriteOperationsRequiringKey() {
    const ops = metadata.getWriteOperationsRequiringKey();
    return translateItems(ops);
  }

  /**
   * Get translated transformations by category
   * @param {string} categoryKey - Category translation key
   * @returns {Array} Translated transformations
   */
  function getTransformationsByCategory(categoryKey) {
    const items = metadata.transformationTypes.value.filter(
      t => t.categoryKey === categoryKey
    );
    return translateItems(items);
  }

  /**
   * Get unique translated transformation categories
   * @returns {Array} Array of translated category names
   */
  function getTransformationCategories() {
    const categoryKeys = new Set(
      metadata.transformationTypes.value.map(t => t.categoryKey)
    );
    return Array.from(categoryKeys).map(key => t(key));
  }

  /**
   * Get translated options for a specific metadata type
   * @param {string} type - Metadata type name (e.g., 'transformationTypes', 'connectorTypes')
   * @returns {Array} Translated options
   */
  function getTranslatedOptions(type) {
    const typeMap = {
      connectorTypes,
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

    const options = typeMap[type];
    return options ? options.value : [];
  }

  return {
    // State from original metadata
    loading: metadata.loading,
    error: metadata.error,
    isInitialized: metadata.isInitialized,

    // Methods
    loadMetadata: metadata.loadMetadata,
    getProvidersForType,
    getWriteOperationsRequiringKey,
    getTransformationsByCategory,
    getTransformationCategories,
    getTranslatedOptions,
    translateItem,
    translateItems,

    // Translated computed data
    connectorTypes,
    connectorProviders: metadata.connectorProviders, // No translation needed
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
