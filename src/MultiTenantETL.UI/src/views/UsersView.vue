<template>
  <div>
    <div class="d-flex align-center mb-4">
      <h1 class="text-h4 mr-4">{{ $t('users.title') }}</h1>
      <v-spacer />
      <!-- Note: Users are created via registration endpoint, not directly -->
    </div>

    <v-card>
      <v-card-text>
        <TableFilters
          v-model:search="search"
          :search-label="$t('users.searchUsers')"
          :filters="[{
            key: 'status',
            label: $t('common.status'),
            items: statusOptions,
            cols: 3
          }]"
          :sort-options="sortOptions"
          @filter="handleFilter"
          @sort="handleSort"
        />

        <v-data-table
          :headers="headers"
          :items="users"
          :loading="loading"
          :items-per-page="10"
          class="mt-2"
        >
          <template #item.status="{ item }">
            <v-chip
              :color="item.isActive ? 'success' : 'error'"
              text-color="white"
              size="small"
            >
              {{ item.isActive ? $t('common.active') : $t('common.inactive') }}
            </v-chip>
          </template>
          <template #item.name="{ item }">
            {{ item.firstName }} {{ item.lastName }}
          </template>
          <template #item.roles="{ item }">
            <v-chip
              v-for="role in item.roles"
              :key="role"
              :color="getRoleColor(role)"
              text-color="white"
              size="small"
              class="mr-1"
            >
              {{ role }}
            </v-chip>
          </template>
          <template #item.createdAt="{ item }">
            {{ formatDate(item.createdAt) }}
          </template>
          <template #item.actions="{ item }">
            <!-- TenantAdmin cannot edit SuperAdmin users -->
            <v-btn
              v-if="!(authStore.user?.role === 'TenantAdmin' && item.roles?.includes('SuperAdmin'))"
              icon
              variant="text"
              size="small"
              :title="$t('users.editUser')"
              @click="editUser(item)"
            >
              <v-icon>mdi-pencil</v-icon>
            </v-btn>
            <!-- Only SuperAdmin can activate/deactivate users -->
            <v-btn
              v-if="authStore.user?.role === 'SuperAdmin' && !item.isActive"
              icon
              variant="text"
              size="small"
              color="success"
              :title="$t('users.activateUser')"
              @click="toggleUserStatus(item)"
            >
              <v-icon>mdi-check</v-icon>
            </v-btn>
            <v-btn
              v-else-if="authStore.user?.role === 'SuperAdmin' && item.isActive"
              icon
              variant="text"
              size="small"
              color="warning"
              :title="$t('users.deactivateUser')"
              @click="toggleUserStatus(item)"
            >
              <v-icon>mdi-close</v-icon>
            </v-btn>
            <!-- Only SuperAdmin can delete users -->
            <v-btn
              v-if="authStore.user?.role === 'SuperAdmin'"
              icon
              variant="text"
              size="small"
              color="error"
              :title="$t('users.deleteUser')"
              :disabled="item.isActive"
              @click="confirmDelete(item)"
            >
              <v-icon>mdi-delete</v-icon>
            </v-btn>
          </template>
        </v-data-table>
      </v-card-text>
    </v-card>

    <!-- Create/Edit User Dialog -->
    <v-dialog
      v-model="showCreateDialog"
      max-width="600px"
      persistent
    >
      <v-card>
        <v-card-title class="text-h5 pa-4 d-flex align-center">
          {{ editedUser.id ? $t('users.editUser') : $t('users.createUser') }}
          <v-spacer />
          <v-btn
            icon
            variant="text"
            :disabled="savingUser"
            @click="closeCreateDialog"
          >
            <v-icon>mdi-close</v-icon>
          </v-btn>
        </v-card-title>
        <v-card-text class="pa-4">
          <UserForm
            v-model:user="editedUser"
            :roles="roles"
            @submit="saveUser"
            @remove-tenant="handleRemoveTenant"
          />
          
          <!-- Tenant Management Section -->
          <v-divider v-if="editedUser.id" class="my-4" />
          <div v-if="editedUser.id" class="mt-4">
            <div class="d-flex align-center mb-2">
              <h3 class="text-subtitle-1">{{ $t('users.manageTenants') }}</h3>
              <v-spacer />
              <v-btn
                size="small"
                color="primary"
                @click="showAddTenantDialog = true"
              >
                <v-icon class="mr-1">mdi-plus</v-icon>
                {{ $t('users.addToTenant') }}
              </v-btn>
            </div>
          </div>
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn
            :disabled="savingUser"
            @click="closeCreateDialog"
          >
            {{ $t('common.close') }}
          </v-btn>
          <v-btn
            color="primary"
            :loading="savingUser"
            @click="saveUser"
          >
            {{ $t('common.save') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Delete Confirmation Dialog -->
    <ConfirmationDialog
      v-model:show="showDeleteDialog"
      :title="$t('users.deleteUser')"
      :confirm-text="$t('common.delete')"
      confirm-color="error"
      :loading="deletingUser"
      @confirm="deleteUser"
    >
      {{ $t('users.deleteConfirm', { name: `${userToDelete?.firstName} ${userToDelete?.lastName}` }) }}
    </ConfirmationDialog>

    <!-- Add to Tenant Dialog -->
    <v-dialog
      v-model="showAddTenantDialog"
      max-width="500px"
    >
      <v-card>
        <v-card-title>{{ $t('users.addToTenant') }}</v-card-title>
        <v-card-text>
          <v-select
            v-model="selectedTenantId"
            :items="availableTenants"
            item-title="name"
            item-value="id"
            :label="$t('forms.selectTenant')"
            class="mb-4"
          />
          <v-select
            v-model="selectedTenantRole"
            :items="tenantRoles"
            :label="$t('forms.tenantRole')"
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn @click="showAddTenantDialog = false">
            {{ $t('common.cancel') }}
          </v-btn>
          <v-btn
            color="primary"
            :loading="addingToTenant"
            :disabled="!selectedTenantId"
            @click="addUserToTenant"
          >
            {{ $t('common.add') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup>
import { ref, onMounted, computed, watch } from 'vue';
import { useRouter } from 'vue-router';
import { useAuthStore } from '@/stores/auth';
import { userService } from '@/services/userService';

// Import required components
import TableFilters from '@/components/table/TableFilters.vue';
import UserForm from '@/components/users/UserForm.vue';
import ConfirmationDialog from '@/components/dialogs/ConfirmationDialog.vue';
import { useGlobalState } from '@/composables/useGlobalState';

// Destructure methods from service
const {
  getRoleColor,
  formatDate,
  getAvailableRoles,
  createEmpty
} = userService;

const router = useRouter();
const authStore = useAuthStore();

// Import i18n
import { useI18n } from 'vue-i18n';
const { t } = useI18n();

// Data table
const headers = computed(() => [
  { title: t('common.name'), key: 'name' },
  { title: t('users.email'), key: 'email' },
  { title: t('users.role'), key: 'roles', width: '120px' },
  { title: t('common.status'), key: 'status', width: '120px' },
  { title: t('common.created'), key: 'createdAt', width: '150px' },
  { title: t('common.actions'), key: 'actions', sortable: false, width: '150px', align: 'end' }
]);

// Get available roles from service
const roles = getAvailableRoles();

// Filters and sorting
const search = ref('');
const statusFilter = ref('All');
const sortBy = ref('name_asc');
const statusOptions = computed(() => [
  { title: t('filters.allStatuses'), value: 'All' },
  { title: t('common.active'), value: 'Active' },
  { title: t('common.inactive'), value: 'Inactive' }
]);
const sortOptions = computed(() => [
  { title: t('filters.nameAsc'), value: 'name_asc' },
  { title: t('filters.nameDesc'), value: 'name_desc' },
  { title: t('filters.createdDesc'), value: 'created_desc' },
  { title: t('filters.createdAsc'), value: 'created_asc' }
]);

// Data
const allUsers = ref([]);
const users = ref([]);
const loading = ref(false);
const savingUser = ref(false);
const deletingUser = ref(false);
const addingToTenant = ref(false);

// Dialog controls
const showCreateDialog = ref(false);
const showDeleteDialog = ref(false);
const showAddTenantDialog = ref(false);
const userToDelete = ref(null);
const editedUser = ref(createEmpty());

// Global notification
const { showSuccess, showError } = useGlobalState();

// Tenant management
const availableTenants = ref([]);
const selectedTenantId = ref(null);
const selectedTenantRole = ref('User');
const tenantRoles = ['TenantAdmin', 'User'];

async function fetchUsers() {
  try {
    loading.value = true;
    const result = await userService.getAll({
      status: statusFilter.value,
      sort: sortBy.value
    });
    allUsers.value = result;
    filterUsers();
  } catch (error) {
    console.error('Error fetching users:', error);
    showError(t('users.errors.loadFailed'), t('common.error'));
  } finally {
    loading.value = false;
  }
}

function filterUsers() {
  let filtered = [...allUsers.value];
  
  // Apply search filter
  if (search.value) {
    const searchLower = search.value.toLowerCase();
    filtered = filtered.filter(user => 
      user.email?.toLowerCase().includes(searchLower) ||
      user.firstName?.toLowerCase().includes(searchLower) ||
      user.lastName?.toLowerCase().includes(searchLower) ||
      `${user.firstName} ${user.lastName}`.toLowerCase().includes(searchLower)
    );
  }
  
  users.value = filtered;
}

async function editUser(user) {
  try {
    // Fetch full user details including tenants
    const fullUser = await userService.getById(user.id);
    
    // Convert roles array to single role for the form
    const role = Array.isArray(fullUser.roles) && fullUser.roles.length > 0 
      ? fullUser.roles[0] 
      : 'User';
    
    editedUser.value = { 
      ...fullUser,
      role: role,
      tenants: fullUser.tenants || []
    };
    showCreateDialog.value = true;
  } catch (error) {
    console.error('Error loading user details:', error);
    showError(t('users.errors.loadDetailsFailed'), t('common.error'));
  }
}

function closeCreateDialog() {
  showCreateDialog.value = false;
  editedUser.value = createEmpty();
}

function confirmDelete(user) {
  userToDelete.value = user;
  showDeleteDialog.value = true;
}

async function deleteUser() {
  try {
    deletingUser.value = true;
    await userService.delete(userToDelete.value.id);
    await fetchUsers();
    showDeleteDialog.value = false;
    userToDelete.value = null;
    showSuccess(t('users.deleteSuccess'), t('users.title'));
  } catch (error) {
    console.error('Error deleting user:', error);
    showError(t('users.errors.deleteFailed'), t('common.error'));
  } finally {
    deletingUser.value = false;
  }
}

async function saveUser() {
  try {
    savingUser.value = true;
    
    if (editedUser.value.id) {
      await userService.update(editedUser.value.id, editedUser.value);
    }
    
    await fetchUsers();
    showCreateDialog.value = false;
    editedUser.value = createEmpty();
    showSuccess(t('users.saveSuccess'), t('users.title'));
  } catch (error) {
    console.error('Error saving user:', error);
    showError(t('users.errors.saveFailed'), t('common.error'));
  } finally {
    savingUser.value = false;
  }
}

async function toggleUserStatus(user) {
  try {
    loading.value = true;
    await userService.toggleStatus(user.id);
    await fetchUsers();
    showSuccess(
      user.isActive ? t('users.deactivateSuccess') : t('users.activateSuccess'),
      t('users.title')
    );
  } catch (error) {
    console.error('Error toggling user status:', error);
    showError(t('users.errors.statusUpdateFailed'), t('common.error'));
  } finally {
    loading.value = false;
  }
}

function handleFilter({ key, value }) {
  if (key === 'status') {
    statusFilter.value = value;
    fetchUsers(); // Re-fetch for status changes (backend filter)
  }
}

function handleSort(value) {
  sortBy.value = value;
  fetchUsers(); // Re-fetch for sorting (backend sort)
}

async function handleRemoveTenant(tenantId) {
  try {
    loading.value = true;
    await userService.removeUserFromTenant(editedUser.value.id, tenantId);
    
    // Refresh user data
    const updatedUser = await userService.getById(editedUser.value.id);
    editedUser.value.tenants = updatedUser.tenants || [];
    
    showSuccess(t('users.removeFromTenantSuccess'), t('users.title'));
  } catch (error) {
    console.error('Error removing user from tenant:', error);
    showError(t('users.errors.removeFromTenantFailed'), t('common.error'));
  } finally {
    loading.value = false;
  }
}

async function addUserToTenant() {
  try {
    addingToTenant.value = true;
    await userService.addUserToTenant(
      editedUser.value.id,
      selectedTenantId.value,
      selectedTenantRole.value
    );
    
    // Refresh user data
    const updatedUser = await userService.getById(editedUser.value.id);
    editedUser.value.tenants = updatedUser.tenants || [];
    
    showAddTenantDialog.value = false;
    selectedTenantId.value = null;
    selectedTenantRole.value = 'User';
    
    showSuccess(t('users.addToTenantSuccess'), t('users.title'));
  } catch (error) {
    console.error('Error adding user to tenant:', error);
    showError(t('users.errors.addToTenantFailed'), t('common.error'));
  } finally {
    addingToTenant.value = false;
  }
}

async function fetchAvailableTenants() {
  try {
    const tenantService = (await import('@/services/tenantService')).tenantService;
    const allTenants = await tenantService.getAll();
    
    // Filter out tenants user is already in
    const userTenantIds = (editedUser.value.tenants || []).map(t => t.tenantId);
    availableTenants.value = allTenants.filter(t => !userTenantIds.includes(t.id));
  } catch (error) {
    console.error('Error fetching tenants:', error);
  }
}

// Watch search changes to filter locally
watch(search, () => {
  filterUsers();
});

onMounted(async () => {
  if (!authStore.isAdmin) {
    router.push('/');
    return;
  }
  
  await fetchUsers();
  await fetchAvailableTenants();
});
</script>