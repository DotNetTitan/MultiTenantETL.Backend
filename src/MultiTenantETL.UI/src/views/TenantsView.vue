<template>
  <div>
    <!-- Access denied message for non-admin users -->
    <v-alert
      v-if="!authStore.isAdmin"
      type="warning"
      variant="tonal"
      class="mb-4"
    >
      <v-icon class="mr-2">mdi-shield-alert</v-icon>
      {{ $t('common.accessDenied') }}
      <div class="text-body-2 mt-1">
        {{ $t('tenants.adminRequired') }}
      </div>
    </v-alert>

    <!-- Admin content -->
    <div v-else>
      <div class="d-flex align-center mb-4">
        <h1 class="text-h4">{{ $t('tenants.title') }}</h1>
        <v-spacer />
        <!-- Only SuperAdmin can add new tenants -->
        <v-btn
          v-if="authStore.user?.role === 'SuperAdmin'"
          color="primary"
          @click="openCreateDialog"
        >
          <v-icon v-if="$vuetify.display.smAndUp" class="mr-2">mdi-plus</v-icon>
          <span v-if="$vuetify.display.xs">{{ $t('common.add') }}</span>
          <span v-else>{{ $t('tenants.addTenant') }}</span>
        </v-btn>
      </div>

      <v-card>
        <v-card-text>
          <table-filters
            v-model:search="searchQuery"
            :search-label="$t('tenants.searchTenants')"
            :filters="availableFilters"
            :sort-options="sortOptions"
            @filter="handleFilter"
            @sort="handleSort"
          />

          <v-data-table
            :headers="headers"
            :items="tenants"
            :loading="loading"
            :items-per-page="10"
            class="mt-2"
          >
            <template #item.isActive="{ item }">
              <v-chip
                :color="item.isActive ? 'success' : 'error'"
                size="small"
              >
                {{ item.isActive ? $t('common.active') : $t('common.inactive') }}
              </v-chip>
            </template>
            <template #item.createdAt="{ item }">
              {{ tenantService.formatDate(item.createdAt) }}
            </template>
            <template #item.actions="{ item }">
              <!-- Manage users button (available to all admins) -->
              <v-btn
                icon="mdi-account-multiple"
                size="small"
                variant="text"
                color="primary"
                @click="openUsersDialog(item)"
              />
              <!-- Edit button (SuperAdmin only) -->
              <v-btn
                v-if="authStore.user?.role === 'SuperAdmin'"
                icon="mdi-pencil"
                size="small"
                variant="text"
                @click="openEditDialog(item)"
              />
              <!-- Delete button (SuperAdmin only) -->
              <v-btn
                v-if="authStore.user?.role === 'SuperAdmin'"
                icon="mdi-delete"
                size="small"
                variant="text"
                color="error"
                @click="confirmDelete(item)"
              />
            </template>
          </v-data-table>
        </v-card-text>
      </v-card>

      <!-- Tenant Form Dialog -->
      <v-dialog v-model="showCreateDialog" max-width="600">
        <v-card>
          <v-card-title class="d-flex align-center">
            {{ isEditing ? $t('tenants.editTenant') : $t('tenants.createTenant') }}
            <v-spacer />
            <v-btn
              icon
              variant="text"
              :disabled="loading"
              @click="closeDialog"
            >
              <v-icon>mdi-close</v-icon>
            </v-btn>
          </v-card-title>
          <v-card-text>
            <tenant-form
              :tenant="editedTenant"
              :loading="loading"
              :error="error"
              @update:tenant="editedTenant = $event"
            />
          </v-card-text>
          <v-card-actions>
            <v-spacer />
            <v-btn
              :disabled="loading"
              @click="closeDialog"
            >
              {{ $t('common.close') }}
            </v-btn>
            <v-btn
              color="primary"
              :loading="loading"
              @click="saveTenant"
            >
              {{ $t('common.save') }}
            </v-btn>
          </v-card-actions>
        </v-card>
      </v-dialog>

      <!-- Confirmation Dialog -->
      <confirmation-dialog
        v-model:show="showDeleteDialog"
        :title="$t('tenants.deleteTenant')"
        :message="$t('tenants.deleteConfirm')"
        @confirm="deleteTenant"
      />

      <!-- Tenant Users Dialog -->
      <v-dialog v-model="showUsersDialog" max-width="800">
        <v-card>
          <v-card-title class="d-flex align-center">
            {{ $t('tenants.manageUsers') }} - {{ selectedTenant?.name }}
            <v-spacer />
            <v-btn
              icon
              variant="text"
              @click="showUsersDialog = false"
            >
              <v-icon>mdi-close</v-icon>
            </v-btn>
          </v-card-title>
          <v-card-text>
            <tenant-users
              v-if="selectedTenant"
              :tenant-id="selectedTenant.id"
            />
          </v-card-text>
        </v-card>
      </v-dialog>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, watch } from 'vue';
import { useI18n } from 'vue-i18n';
const { t } = useI18n();
import { useAuthStore } from '@/stores/auth';
import { tenantService } from '@/services/tenantService';
import TableFilters from '@/components/table/TableFilters.vue';
import TenantForm from '@/components/tenants/TenantForm.vue';
import TenantUsers from '@/components/tenants/TenantUsers.vue';
import ConfirmationDialog from '@/components/dialogs/ConfirmationDialog.vue';

const authStore = useAuthStore();

const loading = ref(false);
const error = ref(null);
const allTenants = ref([]);
const tenants = ref([]);
const showCreateDialog = ref(false);
const showDeleteDialog = ref(false);
const showUsersDialog = ref(false);
const selectedTenant = ref(null);
const statusFilter = ref('all');
const sortBy = ref('name');
const searchQuery = ref('');

const editedTenant = ref(createEmptyTenant());
const isEditing = computed(() => !!editedTenant.value.id);

const availableFilters = computed(() => [{
  key: 'status',
  label: t('common.status'),
  cols: 3,
  items: [
    { title: t('common.all'), value: 'all' },
    { title: t('common.active'), value: 'active' },
    { title: t('common.inactive'), value: 'inactive' }
  ],
  default: 'all'
}]);

const sortOptions = computed(() => [
  { title: t('common.name'), value: 'name_asc' },
  { title: t('common.created'), value: 'createdAt_asc' }
]);

// Data table headers
const headers = computed(() => [
  { title: t('common.name'), key: 'name' },
  { title: t('tenants.slug'), key: 'slug' },
  { title: t('common.status'), key: 'isActive', width: '120px' },
  { title: t('common.created'), key: 'createdAt', width: '180px' },
  { title: t('common.actions'), key: 'actions', sortable: false, width: '200px', align: 'end' }
]);

function createEmptyTenant() {
  return {
    name: '',
    slug: '',
    description: ''
  };
}

// UI-specific methods
function openCreateDialog() {
  if (authStore.user?.role !== 'SuperAdmin') return;
  editedTenant.value = createEmptyTenant();
  showCreateDialog.value = true;
}

function openEditDialog(tenant) {
  if (authStore.user?.role !== 'SuperAdmin') return;
  editedTenant.value = { ...tenant };
  showCreateDialog.value = true;
}

function closeDialog() {
  showCreateDialog.value = false;
  editedTenant.value = createEmptyTenant();
}

function confirmDelete(tenant) {
  selectedTenant.value = tenant;
  showDeleteDialog.value = true;
}

function openUsersDialog(tenant) {
  selectedTenant.value = tenant;
  showUsersDialog.value = true;
}

// Data operations with error handling
async function fetchTenants() {
  try {
    loading.value = true;
    error.value = null;

    if (authStore.user?.role === 'SuperAdmin') {
      // SuperAdmin can see all tenants
      allTenants.value = await tenantService.getAll({
        status: statusFilter.value,
        sort: sortBy.value
      });
    } else {
      // TenantAdmin can see their own tenant(s)
      const userTenants = await tenantService.getMyTenants();
      // Convert user tenant format to tenant format for display
      allTenants.value = userTenants.map(ut => ({
        id: ut.tenantId,
        name: ut.tenantName,
        slug: ut.tenantSlug,
        isActive: ut.isActive,
        createdAt: new Date() // We don't have this info, but it's needed for display
      }));
    }

    filterTenants();
  } catch (err) {
    console.error('Error fetching tenants:', err);
    error.value = 'Failed to load tenants';
  } finally {
    loading.value = false;
  }
}

function filterTenants() {
  let filtered = [...allTenants.value];
  
  // Apply search filter
  if (searchQuery.value) {
    const searchLower = searchQuery.value.toLowerCase();
    filtered = filtered.filter(tenant => 
      tenant.name?.toLowerCase().includes(searchLower) ||
      tenant.slug?.toLowerCase().includes(searchLower) ||
      tenant.description?.toLowerCase().includes(searchLower)
    );
  }
  
  tenants.value = filtered;
}

// Watch search changes to filter locally
watch(searchQuery, () => {
  filterTenants();
});

async function saveTenant() {
  if (authStore.user?.role !== 'SuperAdmin') return;

  try {
    loading.value = true;
    error.value = null;

    if (isEditing.value) {
      await tenantService.update(editedTenant.value.id, editedTenant.value);
    } else {
      await tenantService.create(editedTenant.value);
    }

    if (authStore.isAdmin) {
      await fetchTenants();
    }
    closeDialog();
  } catch (err) {
    console.error('Error saving tenant:', err);
    error.value = 'Failed to save tenant';
  } finally {
    loading.value = false;
  }
}

async function deleteTenant() {
  if (authStore.user?.role !== 'SuperAdmin') return;

  if (!selectedTenant.value) return;

  try {
    loading.value = true;
    error.value = null;
    await tenantService.delete(selectedTenant.value.id);
    if (authStore.isAdmin) {
      await fetchTenants();
    }
    showDeleteDialog.value = false;
  } catch (err) {
    console.error('Error deleting tenant:', err);
    error.value = 'Failed to delete tenant';
  } finally {
    loading.value = false;
  }
}



// Event handlers
function handleFilter({ key, value }) {
  if (key === 'status') {
    statusFilter.value = value;
    if (authStore.isAdmin) {
      fetchTenants();
    }
  }
}

function handleSort(value) {
  sortBy.value = value;
  if (authStore.isAdmin) {
    fetchTenants();
  }
}

onMounted(() => {
  // Only fetch tenants if user is admin
  if (authStore.isAdmin) {
    fetchTenants();
  }
});
</script>
