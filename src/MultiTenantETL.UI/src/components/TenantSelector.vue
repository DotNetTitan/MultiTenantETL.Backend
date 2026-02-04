<template>
  <div v-if="showTenantSelector" class="tenant-selector">
    <v-select
      v-model="selectedTenantId"
      :items="tenantItems"
      label="Select Tenant"
      density="compact"
      hide-details
      bg-color="transparent"
      class="tenant-select"
      :loading="tenantStore.loading"
      :error="!!tenantStore.error"
      :error-messages="tenantStore.error"
      @update:model-value="changeTenant"
    >
      <template #prepend>
        <v-icon>mdi-office-building</v-icon>
      </template>
    </v-select>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, watch } from 'vue';
import { useTenantStore } from '@/stores/tenant';
import { useGlobalState } from '@/composables/useGlobalState';
import { useI18n } from 'vue-i18n';

const tenantStore = useTenantStore();
const selectedTenantId = ref(tenantStore.currentTenantId);
const { showSuccess, showError } = useGlobalState();
const { t } = useI18n();

// Convert tenants array to format needed for v-select
const tenantItems = computed(() => {
  if (tenantStore.error) return [];
  // Filter out inactive tenants - users shouldn't see or switch to them
  return tenantStore.tenants
    .filter(tenant => tenant.isActive)
    .map(tenant => ({
      title: tenant.name,
      value: tenant.id
    }));
});

// Only show tenant selector if user has access to multiple active tenants
const showTenantSelector = computed(() => {
  return tenantItems.value.length > 1;
});

async function changeTenant(tenantId) {
  try {
    await tenantStore.setCurrentTenant(tenantId);
    
    // Find tenant name for notification
    const tenant = tenantStore.tenants.find(t => t.id === tenantId);
    const tenantName = tenant?.name || 'tenant';
    
    // Show success notification
    showSuccess(t('tenants.switchSuccess', { name: tenantName }), t('tenants.title'));
  } catch (error) {
    // Error is already handled in store, just show notification
    showError(t('tenants.errors.switchFailed'), t('common.error'));
  }
}

// Watch for current tenant changes in store
watch(() => tenantStore.currentTenantId, (newTenantId) => {
  selectedTenantId.value = newTenantId;
});

onMounted(async () => {
  try {
    const { tenantService } = await import('@/services/tenantService');
    const { useAuthStore } = await import('@/stores/auth');
    const authStore = useAuthStore();
    
    // Try to get all tenants if SuperAdmin, fallback to user's tenants if 403
    if (authStore.user?.role === 'SuperAdmin') {
      try {
        const allTenants = await tenantService.getAll();
        tenantStore.tenants = allTenants;
      } catch (error) {
        // If 403, user might not actually be SuperAdmin, fallback to their tenants
        if (error.response?.status === 403) {
          console.warn('SuperAdmin check failed, falling back to user tenants');
          const userTenants = await tenantService.getMyTenants();
          tenantStore.tenants = userTenants.map(ut => {
            const { tenantId, tenantName, tenantSlug, isActive } = ut;
            return {
              id: tenantId,
              name: tenantName,
              slug: tenantSlug,
              isActive: isActive
            };
          });
        } else {
          throw error;
        }
      }
    } else {
      const userTenants = await tenantService.getMyTenants();
      tenantStore.tenants = userTenants.map(ut => {
        const { tenantId, tenantName, tenantSlug, isActive } = ut;
        return {
          id: tenantId,
          name: tenantName,
          slug: tenantSlug,
          isActive: isActive
        };
      });
    }
    
    // Validate that current tenant ID exists in user's tenant list
    if (tenantStore.currentTenantId) {
      const tenantExists = tenantStore.tenants.some(t => t.id === tenantStore.currentTenantId && t.isActive);
      if (!tenantExists) {
        localStorage.removeItem('currentTenantId');
        tenantStore.currentTenantId = null;
        selectedTenantId.value = null;
      }
    }
    
    // Auto-select the first active tenant if none is currently selected
    // Only update local state - don't call API to avoid unnecessary tenant switch logs
    if (!tenantStore.currentTenantId && tenantStore.tenants.length > 0) {
      const firstActiveTenant = tenantStore.tenants.find(t => t.isActive);
      if (firstActiveTenant) {
        // Set locally without calling backend API
        tenantStore.currentTenantId = firstActiveTenant.id;
        localStorage.setItem('currentTenantId', firstActiveTenant.id);
        selectedTenantId.value = firstActiveTenant.id;
      }
    }
  } catch (error) {
    console.error('Failed to fetch tenants in TenantSelector:', error);
    tenantStore.error = 'Failed to load tenants';
  }
});
</script>

<style scoped>
.tenant-selector {
  min-width: 200px;
  margin: 0 16px;
}

.tenant-select :deep(.v-field__input) {
  color: white;
}

.tenant-select :deep(.v-field__append-inner) {
  color: white;
}

.tenant-select :deep(.v-field--error) {
  color: rgb(var(--v-theme-error));
}
</style>
