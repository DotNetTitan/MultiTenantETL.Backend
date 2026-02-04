/**
 * Composable for provider metadata (icons and colors)
 * Fetches from backend instead of hardcoding
 */

import { ref, computed } from 'vue'
import { getConnectorConfig } from '@/services/metadataService'

const providerMetadata = ref(null)
const loading = ref(false)
const error = ref(null)

export function useProviderMetadata() {
  /**
   * Load provider metadata from backend
   */
  async function loadProviderMetadata() {
    if (providerMetadata.value) {
      return providerMetadata.value
    }

    loading.value = true
    error.value = null
    
    try {
      const config = await getConnectorConfig()
      
      if (!config.providerMetadata) {
        throw new Error('Provider metadata not found in backend response')
      }
      
      providerMetadata.value = config.providerMetadata
      return providerMetadata.value
    } catch (err) {
      error.value = err
      throw err
    } finally {
      loading.value = false
    }
  }

  /**
   * Get icon for a provider
   * @param {string} provider - Provider name (e.g., 'PostgreSQL', 'S3')
   * @returns {string} Material Design Icon name
   */
  function getProviderIcon(provider) {
    if (!provider) return 'mdi-connection'
    
    // Return default if metadata not loaded yet (will update on next render after load)
    if (!providerMetadata.value) {
      return 'mdi-connection'
    }
    
    const metadata = providerMetadata.value[provider]
    if (metadata?.icon) {
      return metadata.icon
    }

    // Try to match by lowercase
    const providerLower = provider.toLowerCase()
    for (const [key, value] of Object.entries(providerMetadata.value)) {
      if (key.toLowerCase() === providerLower) {
        return value.icon || 'mdi-connection'
      }
    }

    // If not found, return default
    console.warn(`No icon found for provider: ${provider}`)
    return 'mdi-connection'
  }

  /**
   * Get color for a provider
   * @param {string} provider - Provider name
   * @returns {string} Vuetify color name
   */
  function getProviderColor(provider) {
    if (!provider) return 'grey'
    
    // Return default if metadata not loaded yet (will update on next render after load)
    if (!providerMetadata.value) {
      return 'grey'
    }
    
    const metadata = providerMetadata.value[provider]
    if (metadata?.color) {
      return metadata.color
    }

    // Try to match by lowercase
    const providerLower = provider.toLowerCase()
    for (const [key, value] of Object.entries(providerMetadata.value)) {
      if (key.toLowerCase() === providerLower) {
        return value.color || 'grey'
      }
    }

    // If not found, return default
    console.warn(`No color found for provider: ${provider}`)
    return 'grey'
  }

  /**
   * Check if metadata is loaded
   */
  const isLoaded = computed(() => providerMetadata.value !== null)

  return {
    providerMetadata: computed(() => providerMetadata.value),
    loading: computed(() => loading.value),
    error: computed(() => error.value),
    isLoaded,
    loadProviderMetadata,
    getProviderIcon,
    getProviderColor,
    // For testing only
    _resetState: () => {
      providerMetadata.value = null
      loading.value = false
      error.value = null
    }
  }
}
