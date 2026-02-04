import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { tenantService } from '@/services/tenantService'
import api from '@/services/api'

export const useTenantStore = defineStore('tenant', () => {
  // Initialize currentTenantId from auth store user if available, otherwise localStorage
  const getInitialTenantId = () => {
    // Try to get from auth store first (only if it's been initialized)
    try {
      const { useAuthStore } = require('./auth')
      const authStore = useAuthStore()
      if (authStore.user?.tenantId) {
        // Sync to localStorage for persistence
        localStorage.setItem('currentTenantId', authStore.user.tenantId)
        return authStore.user.tenantId
      }
    } catch (error) {
      // Auth store not available yet or error, fall back to localStorage
    }
    
    // Fall back to localStorage
    return localStorage.getItem('currentTenantId') || null
  }
  
  const currentTenantId = ref(getInitialTenantId())
  const tenants = ref([])
  const loading = ref(false)
  const error = ref(null)

  const currentTenant = computed(() => {
    if (!currentTenantId.value) return null
    return tenants.value.find(t => t.id === currentTenantId.value) || null
  })

  async function fetchTenants() {
    try {
      loading.value = true
      error.value = null
      const response = await tenantService.getAll()
      tenants.value = response
    } catch (err) {
      console.error('Error fetching tenants:', err)
      if (err.code === 'ERR_CONNECTION_REFUSED') {
        error.value = 'Unable to connect to the server. Please check if the API server is running.'
      } else if (err.response?.status === 401) {
        error.value = 'Authentication required'
      } else {
        error.value = 'Failed to load tenants. Please try again later.'
      }
      tenants.value = []
      throw err
    } finally {
      loading.value = false
    }
  }

  async function createTenant(tenantData) {
    try {
      loading.value = true
      error.value = null
      const newTenant = await tenantService.create(tenantData)
      tenants.value.push(newTenant)
      return newTenant
    } catch (err) {
      console.error('Error creating tenant:', err)
      if (err.code === 'ERR_CONNECTION_REFUSED') {
        error.value = 'Unable to connect to the server. Please check if the API server is running.'
      } else {
        error.value = 'Failed to create tenant. Please try again later.'
      }
      throw err
    } finally {
      loading.value = false
    }
  }

  async function setCurrentTenant(tenantId) {
    try {
      loading.value = true
      error.value = null
      
      // Call backend to switch tenant and get new tokens
      const { useAuthStore } = await import('./auth')
      const authStore = useAuthStore()
      await authStore.switchTenant(tenantId)
      
      // Update local state - use tenantId from auth store's updated user for consistency
      const updatedTenantId = authStore.user?.tenantId || tenantId
      currentTenantId.value = updatedTenantId
      localStorage.setItem('currentTenantId', updatedTenantId || '')
      
      // Navigate to dashboard with smooth transition (no page reload!)
      const router = (await import('@/router')).default
      if (router.currentRoute.value.path !== '/dashboard') {
        router.push('/dashboard')
      }
      
      return true
    } catch (err) {
      console.error('Error switching tenant:', err)
      error.value = 'Failed to switch tenant. Please try again.'
      throw err
    } finally {
      loading.value = false
    }
  }

  return {
    currentTenantId,
    currentTenant,
    tenants,
    loading,
    error,
    fetchTenants,
    createTenant,
    setCurrentTenant
  }
})
