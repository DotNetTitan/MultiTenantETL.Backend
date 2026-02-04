<template>
  <div>
    <div class="d-flex align-center mb-4">
      <h1 class="text-h4 mr-4">{{ $t('connectors.title') }}</h1>
      <v-spacer />
      <v-btn 
        color="primary" 
        @click="createNewConnector"
      >
        <v-icon v-if="$vuetify.display.smAndUp" class="mr-2">mdi-plus</v-icon>
        <span v-if="$vuetify.display.xs">{{ $t('common.create') }}</span>
        <span v-else>{{ $t('connectors.createConnector') }}</span>
      </v-btn>
    </div>

    <v-card>
      <v-card-text>
        <v-row>
          <v-col cols="12" md="4">
            <v-text-field
              v-model="search"
              :label="$t('connectors.searchConnectors')"
              prepend-inner-icon="mdi-magnify"
              density="compact"
              hide-details
              class="mb-4"
              @update:model-value="fetchConnectors"
            />
          </v-col>
          <v-col cols="12" md="3">
            <v-select
              v-model="typeFilter"
              :label="$t('common.type')"
              :items="typeOptions"
              density="compact"
              hide-details
              class="mb-4"
              @update:model-value="fetchConnectors"
            />
          </v-col>
          <v-col cols="12" md="3">
            <v-select
              v-model="sortBy"
              :label="$t('filters.sortBy')"
              :items="sortOptions"
              density="compact"
              hide-details
              class="mb-4"
              @update:model-value="fetchConnectors"
            />
          </v-col>
        </v-row>

        <v-data-table
          :headers="headers"
          :items="connectors"
          :loading="loading"
          :items-per-page="10"
          class="mt-2"
        >
          <template #item.type="{ item }">
            <v-chip
              :color="getTypeColor(item.type)"
              text-color="white"
              size="small"
            >
              {{ $t(`connectors.${item.type.toLowerCase()}`) }}
            </v-chip>
          </template>
          <template #item.provider="{ item }">
            <v-tooltip v-if="item.provider" location="top">
              <template #activator="{ props }">
                <v-icon
                  v-bind="props"
                  :color="getProviderColor(item.provider)"
                  :icon="getProviderIcon(item.provider)"
                  size="24"
                />
              </template>
              <span>{{ item.provider }}</span>
            </v-tooltip>
            <span v-else class="text-grey">-</span>
          </template>
          <template #item.description="{ item }">
            <div>
              <div>{{ item.description || '-' }}</div>
              <div v-if="item.schema && item.schema.fields && item.schema.fields.length > 0" class="text-caption text-grey">
                <v-icon size="x-small" class="mr-1">mdi-table</v-icon>
                {{ item.schema.fields.length }} {{ $t('connectors.fields', item.schema.fields.length) }}
              </div>
            </div>
          </template>
          <template #item.direction="{ item }">
            <div class="d-flex align-center">
              <v-icon v-if="item.isSource" size="small" color="success" class="mr-1" :title="$t('connectors.source')">
                mdi-export
              </v-icon>
              <v-icon v-if="item.isDestination" size="small" color="primary" :title="$t('connectors.destination')">
                mdi-import
              </v-icon>
              <span class="ml-2 text-caption">
                {{ getDirectionLabel(item) }}
              </span>
            </div>
          </template>
          <template #item.actions="{ item }">
            <v-btn
              icon
              variant="text"
              size="small"
              :title="$t('connectors.editConnector')"
              @click="editConnector(item)"
            >
              <v-icon>mdi-pencil</v-icon>
            </v-btn>
            <v-btn
              icon
              variant="text"
              size="small"
              color="info"
              :title="$t('connectors.viewSchema')"
              @click="viewSchema(item)"
            >
              <v-icon>mdi-table-eye</v-icon>
            </v-btn>
            <v-btn
              icon
              variant="text"
              size="small"
              color="success"
              :title="$t('connectors.testConnection')"
              @click="testConnection(item)"
            >
              <v-icon>mdi-connection</v-icon>
            </v-btn>
            <v-btn
              icon
              variant="text"
              size="small"
              color="error"
              :title="$t('common.delete')"
              @click="confirmDelete(item)"
            >
              <v-icon>mdi-delete</v-icon>
            </v-btn>
          </template>
        </v-data-table>
      </v-card-text>
    </v-card>

    <!-- Create/Edit Connector Dialog -->
    <v-dialog
      v-model="showCreateDialog"
      :fullscreen="isDialogFullscreen"
      :max-width="isDialogFullscreen ? undefined : '900px'"
      persistent
      @update:model-value="handleDialogClose"
    >
      <ConnectorWizard
        :connector="editedConnector"
        @save="handleWizardSave"
        @close="showCreateDialog = false"
        @toggle-fullscreen="isDialogFullscreen = $event"
      />
    </v-dialog>

    <!-- Old inline form (keeping as backup, can be removed later) -->
    <v-dialog
      v-if="false"
      v-model="showCreateDialog"
      max-width="700px"
      persistent
      @update:model-value="handleDialogClose"
    >
      <v-card>
        <v-card-title>
          {{ editedConnector.id ? $t('connectors.editConnector') : $t('connectors.createConnector') }}
        </v-card-title>
        <v-card-text>
          <v-form ref="form" @submit.prevent="saveConnector">
            <v-row>
              <v-col cols="12">
                <v-text-field
                  v-model="editedConnector.name"
                  label="Data Source Name"
                  required
                  :rules="[v => !!v || 'Name is required']"
                />
              </v-col>
              
              <v-col cols="12" md="6">
                <v-select
                  v-model="editedConnector.type"
                  label="Type"
                  :items="connectorTypes"
                  :rules="[v => !!v || 'Type is required']"
                  @update:model-value="updateConnectorForm"
                />
              </v-col>
              
              <v-col cols="12" md="6">
                <v-switch
                  v-model="editedConnector.isSource"
                  label="Can be used as source"
                  color="primary"
                  hide-details
                />
              </v-col>
              
              <v-col cols="12" md="6">
                <v-switch
                  v-model="editedConnector.isDestination"
                  label="Can be used as destination"
                  color="primary"
                  hide-details
                />
              </v-col>
              
              <v-col cols="12" md="6">
                <v-switch
                  v-model="editedConnector.requiresCredentials"
                  label="Requires credentials"
                  color="primary"
                  hide-details
                  @update:model-value="updateCredentialsFields"
                />
              </v-col>
            </v-row>
            
            <!-- Database specific fields -->
            <div v-if="editedConnector.type === 'Database'">
              <v-row>
                <v-col cols="12" md="6">
                  <v-select
                    v-model="editedConnector.database.provider"
                    label="Database Provider"
                    :items="['SQL Server', 'MySQL', 'PostgreSQL', 'Oracle', 'SQLite']"
                    :rules="[v => !!v || 'Provider is required']"
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="editedConnector.database.server"
                    label="Server/Host"
                    :rules="[v => !!v || 'Server is required']"
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="editedConnector.database.port"
                    label="Port"
                    type="number"
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="editedConnector.database.databaseName"
                    label="Database Name"
                    :rules="[v => !!v || 'Database name is required']"
                  />
                </v-col>
              </v-row>
            </div>
            
            <!-- File specific fields -->
            <div v-if="editedConnector.type === 'File'">
              <v-row>
                <v-col cols="12" md="6">
                  <v-select
                    v-model="editedConnector.file.storageType"
                    label="Storage Type"
                    :items="['Local', 'SFTP', 'S3', 'Azure Blob', 'Google Cloud Storage']"
                    :rules="[v => !!v || 'Storage type is required']"
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="editedConnector.file.path"
                    label="Path/Bucket"
                    :rules="[v => !!v || 'Path is required']"
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-select
                    v-model="editedConnector.file.fileType"
                    label="File Type"
                    :items="['CSV', 'JSON', 'XML', 'Excel', 'Parquet', 'Avro']"
                    :rules="[v => !!v || 'File type is required']"
                  />
                </v-col>
                <v-col v-if="editedConnector.file.fileType === 'CSV'" cols="12" md="6">
                  <v-text-field
                    v-model="editedConnector.file.delimiter"
                    label="Delimiter"
                    placeholder=","
                  />
                </v-col>
              </v-row>
            </div>
            
            <!-- API specific fields -->
            <div v-if="editedConnector.type === 'API'">
              <v-row>
                <v-col cols="12">
                  <v-text-field
                    v-model="editedConnector.api.baseUrl"
                    label="Base URL"
                    :rules="[v => !!v || 'Base URL is required']"
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-select
                    v-model="editedConnector.api.authType"
                    label="Authentication Type"
                    :items="['None', 'Basic', 'Bearer Token', 'API Key', 'OAuth2']"
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-select
                    v-model="editedConnector.api.dataFormat"
                    label="Data Format"
                    :items="['JSON', 'XML', 'CSV']"
                    :rules="[v => !!v || 'Data format is required']"
                  />
                </v-col>
              </v-row>
            </div>
            
            <!-- Credentials fields -->
            <div v-if="editedConnector.requiresCredentials">
              <v-divider class="my-4" />
              <div class="text-subtitle-1 mb-2">Credentials</div>
              
              <v-row>
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="editedConnector.credentials.username"
                    label="Username"
                    :rules="credentialsRules.username"
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="editedConnector.credentials.password"
                    label="Password"
                    type="password"
                    :rules="credentialsRules.password"
                  />
                </v-col>
                
                <v-col v-if="editedConnector.api?.authType === 'API Key'" cols="12">
                  <v-text-field
                    v-model="editedConnector.credentials.apiKey"
                    label="API Key"
                    :rules="[v => !!v || 'API Key is required']"
                  />
                </v-col>
                
                <v-col v-if="editedConnector.api?.authType === 'Bearer Token'" cols="12">
                  <v-text-field
                    v-model="editedConnector.credentials.token"
                    label="Bearer Token"
                    :rules="[v => !!v || 'Token is required']"
                  />
                </v-col>
              </v-row>
            </div>
            
            <!-- Schema Definition -->
            <v-divider class="my-4" />
            <div class="text-subtitle-1 mb-2">Schema Definition</div>
            
            <!-- Schema Preview (if fields exist) -->
            <SchemaPreview
              v-if="editedConnector.schema.fields && editedConnector.schema.fields.length > 0"
              :fields="editedConnector.schema.fields"
              class="mb-4"
            />
            
            <SchemaEditor
              v-model="editedConnector.schema.fields"
              @validate="handleSchemaValidation"
            />
            
            <v-row>
              <v-col cols="12">
                <v-textarea
                  v-model="editedConnector.description"
                  label="Description"
                  rows="2"
                />
              </v-col>
            </v-row>
          </v-form>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn
            variant="text"
            @click="showCreateDialog = false"
          >
            Cancel
          </v-btn>
          <v-btn
            color="primary"
            :loading="savingConnector"
            @click="saveConnector"
          >
            Save
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Delete Confirmation Dialog -->
    <v-dialog
      v-model="showDeleteDialog"
      max-width="400px"
    >
      <v-card>
        <v-card-title class="text-h5">
          {{ $t('connectors.deleteConnector') }}
        </v-card-title>
        <v-card-text>
          {{ $t('connectors.deleteConfirm', { name: connectorToDelete?.name }) }}
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn
            variant="text"
            @click="showDeleteDialog = false"
          >
            {{ $t('common.cancel') }}
          </v-btn>
          <v-btn
            color="error"
            :loading="deletingConnector"
            @click="deleteConnector"
          >
            {{ $t('common.delete') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
    
    <!-- Connection Test Dialog -->
    <v-dialog
      v-model="showConnectionDialog"
      max-width="400px"
    >
      <v-card>
        <v-card-title class="d-flex align-center">
          {{ $t('connectors.testingConnection') }}
          <v-spacer />
          <v-btn
            icon
            variant="text"
            :disabled="testingConnection"
            @click="showConnectionDialog = false"
          >
            <v-icon>mdi-close</v-icon>
          </v-btn>
        </v-card-title>
        <v-card-text>
          <div v-if="testingConnection" class="d-flex flex-column align-center py-4">
            <v-progress-circular indeterminate size="64" width="4" />
            <div class="mt-4">{{ $t('connectors.testingConnectionTo', { name: connectionTestConnector?.name }) }}</div>
          </div>
          <div v-else-if="connectionTestResult" class="text-center py-4">
            <v-icon :color="connectionTestSuccess ? 'success' : 'error'" size="64">
              {{ connectionTestSuccess ? 'mdi-check-circle' : 'mdi-alert-circle' }}
            </v-icon>
            <div class="mt-4 text-h6">
              {{ connectionTestSuccess ? $t('connectors.connectionSuccessful') : $t('connectors.connectionFailed') }}
            </div>
            <div class="mt-2">{{ connectionTestMessage }}</div>
          </div>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn
            color="primary"
            variant="text"
            :disabled="testingConnection"
            @click="showConnectionDialog = false"
          >
            {{ $t('common.close') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Schema Change Warning Dialog -->
    <SchemaChangeWarningDialog
      v-model="showSchemaWarningDialog"
      :affected-pipelines="affectedPipelines"
      @cancel="handleSchemaWarningCancel"
      @proceed="handleSchemaWarningProceed"
    />

    <!-- Schema Viewer Dialog -->
    <v-dialog
      v-model="showSchemaDialog"
      max-width="900px"
    >
      <v-card>
        <v-card-title class="d-flex align-center">
          <v-icon class="mr-2">mdi-table-eye</v-icon>
          {{ selectedConnector?.name }} - {{ $t('common.schema') }}
          <v-spacer />
          <v-btn
            icon
            variant="text"
            @click="showSchemaDialog = false"
          >
            <v-icon>mdi-close</v-icon>
          </v-btn>
        </v-card-title>
        
        <v-card-text>
          <div v-if="loadingSchema" class="text-center py-8">
            <v-progress-circular indeterminate color="primary" size="64" />
            <div class="mt-4">{{ $t('connectors.loadingSchema') }}</div>
          </div>
          
          <div v-else-if="schemaError" class="text-center py-8">
            <v-icon size="64" color="error">mdi-alert-circle</v-icon>
            <div class="mt-4 text-error">{{ schemaError }}</div>
          </div>
          
          <div v-else-if="connectorSchema && connectorSchema.fields">
            <!-- Schema Metadata -->
            <v-card variant="outlined" class="mb-4">
              <v-card-text>
                <v-row dense>
                  <v-col cols="6">
                    <div class="text-caption text-grey">{{ $t('connectors.totalFields') }}</div>
                    <div class="text-h6">{{ connectorSchema.fields.length }}</div>
                  </v-col>
                  <v-col cols="6">
                    <div class="text-caption text-grey">{{ $t('connectors.schemaVersion') }}</div>
                    <div class="text-h6">{{ connectorSchema.version || 1 }}</div>
                  </v-col>
                  <v-col v-if="connectorSchema.lastModified" cols="12">
                    <div class="text-caption text-grey">{{ $t('connectors.lastModified') }}</div>
                    <div class="text-body-2">{{ formatDate(connectorSchema.lastModified) }}</div>
                  </v-col>
                </v-row>
              </v-card-text>
            </v-card>

            <!-- Fields Table -->
            <v-table density="comfortable" hover>
              <thead>
                <tr>
                  <th class="text-left">{{ $t('connectors.fieldName') }}</th>
                  <th class="text-left">{{ $t('connectors.dataType') }}</th>
                  <th class="text-center">{{ $t('common.required') }}</th>
                  <th class="text-center">{{ $t('connectors.nullable') }}</th>
                  <th class="text-left">{{ $t('common.description') }}</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="field in connectorSchema.fields" :key="field.name">
                  <td>
                    <div class="d-flex align-center">
                      <v-icon size="small" class="mr-2" color="primary">mdi-table-column</v-icon>
                      <strong>{{ field.name }}</strong>
                    </div>
                  </td>
                  <td>
                    <v-chip size="small" color="primary" variant="tonal">
                      {{ field.type }}
                    </v-chip>
                    <span v-if="field.length" class="text-caption ml-1">({{ field.length }})</span>
                  </td>
                  <td class="text-center">
                    <v-icon 
                      :color="field.required ? 'error' : 'grey-lighten-1'" 
                      size="small"
                    >
                      {{ field.required ? 'mdi-check-circle' : 'mdi-minus-circle' }}
                    </v-icon>
                  </td>
                  <td class="text-center">
                    <v-icon 
                      :color="field.nullable ? 'success' : 'grey-lighten-1'" 
                      size="small"
                    >
                      {{ field.nullable ? 'mdi-check-circle' : 'mdi-close-circle' }}
                    </v-icon>
                  </td>
                  <td>
                    <span class="text-caption">{{ field.description || '-' }}</span>
                  </td>
                </tr>
              </tbody>
            </v-table>
          </div>
        </v-card-text>
        
        <v-card-actions>
          <v-spacer />
          <v-btn
            color="primary"
            @click="showSchemaDialog = false"
          >
            {{ $t('common.close') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup>
import { ref, onMounted, onBeforeUnmount, computed, inject } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import axios from 'axios';
import { useAuthStore } from '@/stores/auth';
import { useTenantStore } from '@/stores/tenant';
import { useConnector } from '@/composables/useConnector';
import { useTranslatedMetadata } from '@/composables/useTranslatedMetadata';
import { useProviderMetadata } from '@/composables/useProviderMetadata';
import { 
  fetchConnectors as getConnectors, 
  createConnector,
  updateConnector,
  deleteConnector as deleteConnectorAPI, 
  testConnection as testConnectorConnection,
  testExistingConnection,
  fetchConnectorById
} from '@/services/connectorService';
import ConnectorWizard from '@/components/connector/ConnectorWizard.vue';
import SchemaEditor from '@/components/connector/SchemaEditor.vue';
import SchemaPreview from '@/components/connector/SchemaPreview.vue';
import SchemaChangeWarningDialog from '@/components/dialogs/SchemaChangeWarningDialog.vue';
import { findPipelinesUsingConnector } from '@/services/pipelineService';

const route = useRoute();
const router = useRouter();
const tenantStore = useTenantStore();
const { t } = useI18n();

const showNotification = inject('showNotification');

const { validateConnection } = useConnector();
const { connectorTypes: metadataTypes } = useTranslatedMetadata();
const { loadProviderMetadata, getProviderIcon, getProviderColor } = useProviderMetadata();

// Initialize provider metadata and fetch connectors on mount
onMounted(async () => {
  const authStore = useAuthStore();
  
  // Only fetch if authenticated
  if (authStore.isAuthenticated) {
    await loadProviderMetadata();
    await fetchConnectors();
  }
});

// Data table
const headers = computed(() => [
  { title: t('common.name'), key: 'name' },
  { title: t('common.type'), key: 'type', width: '120px' },
  { title: t('connectors.provider'), key: 'provider', width: '140px' },
  { title: t('common.description'), key: 'description' },
  { title: t('connectors.direction'), key: 'direction', width: '150px' },
  { title: t('common.actions'), key: 'actions', sortable: false, width: '120px', align: 'end' }
]);

// Filters and sorting
const search = ref('');
const typeFilter = ref('All');
const sortBy = ref('name_asc');
const typeOptions = computed(() => [
  { title: t('filters.allTypes'), value: 'All' },
  ...metadataTypes.value.map(type => ({
    title: type.label,
    value: type.value
  }))
]);
const sortOptions = computed(() => [
  { title: t('filters.nameAsc'), value: 'name_asc' },
  { title: t('filters.nameDesc'), value: 'name_desc' },
  { title: t('filters.createdDesc'), value: 'created_desc' },
  { title: t('filters.createdAsc'), value: 'created_asc' },
  { title: t('filters.typeAsc'), value: 'type_asc' }
]);

// Connector data
const connectors = ref([]);
const loading = ref(false);
const savingConnector = ref(false);
const deletingConnector = ref(false);

// Dialog controls
const showCreateDialog = ref(false);
const showDeleteDialog = ref(false);
const showConnectionDialog = ref(false);
const showSchemaDialog = ref(false);
const isDialogFullscreen = ref(false);
const showSchemaWarningDialog = ref(false);
const connectorToDelete = ref(null);
const connectionTestConnector = ref(null);
const selectedConnector = ref(null);
const testingConnection = ref(false);
const connectionTestResult = ref(false);
const connectionTestSuccess = ref(false);
const connectionTestMessage = ref('');
const affectedPipelines = ref([]);
const originalSchema = ref(null);
const schemaHasChanged = ref(false);
const loadingSchema = ref(false);
const connectorSchema = ref(null);
const schemaError = ref(null);

// Form data
const form = ref(null);
const editedConnector = ref(createEmptyConnector());

// Validation rules for credentials
const credentialsRules = computed(() => {
  if (!editedConnector.value.requiresCredentials) {
    return {
      username: [],
      password: []
    };
  }
  
  return {
    username: [v => !!v || 'Username is required'],
    password: [v => !!v || 'Password is required']
  };
});

function createEmptyConnector() {
  return {
    id: null,
    name: '',
    description: '',
    type: '',
    provider: '',
    isSource: true,
    isDestination: true,
    requiresCredentials: true,
    config: {},
    database: {
      provider: 'SQL Server',
      server: '',
      port: '',
      databaseName: ''
    },
    file: {
      storageType: 'Local',
      path: '',
      fileType: 'CSV',
      delimiter: ','
    },
    api: {
      baseUrl: '',
      authType: 'None',
      dataFormat: 'JSON'
    },
    credentials: {
      username: '',
      password: '',
      apiKey: '',
      token: ''
    },
    schema: {
      fields: [],
      version: 1,
      isManual: true,
      lastModified: new Date().toISOString()
    }
  };
}

function handleSchemaValidation(validation) {
  // Store validation result if needed
  
  // Check if schema has changed
  if (originalSchema.value && editedConnector.value.schema) {
    const currentSchemaStr = JSON.stringify(editedConnector.value.schema.fields);
    const originalSchemaStr = JSON.stringify(originalSchema.value);
    schemaHasChanged.value = currentSchemaStr !== originalSchemaStr;
  }
}

function editConnector(connector) {
  router.push(`/connectors/${connector.id}/edit`);
}

function getTypeColor(type) {
  switch (type) {
    case 'Database':
      return 'primary';
    case 'File':
      return 'success';
    case 'API':
      return 'info';
    default:
      return 'grey';
  }
}

// Provider icons and colors now come from backend via useProviderMetadata composable
// These functions are imported from the composable above

function getDirectionLabel(connector) {
  if (connector.isSource && connector.isDestination) {
    return t('connectors.both');
  } else if (connector.isSource) {
    return t('connectors.source');
  } else if (connector.isDestination) {
    return t('connectors.destination');
  }
  return '-';
}

function formatDate(dateString) {
  if (!dateString) return '-';
  const date = new Date(dateString);
  return date.toLocaleString();
}

async function fetchConnectors() {
  const authStore = useAuthStore();
  
  // Don't fetch if not authenticated
  if (!authStore.isAuthenticated) {
    return;
  }
  
  try {
    loading.value = true;
    
    const filters = {
      search: search.value,
      type: typeFilter.value,
      sortBy: sortBy.value
    };
    
    const result = await getConnectors(filters);
    // Handle paginated response from API
    connectors.value = result.connectors || result;
  } catch (error) {
    // Don't log errors for silent failures (like during logout)
    if (!error.silent) {
      console.error('Error fetching connectors:', error);
    }
  } finally {
    loading.value = false;
  }
}

function updateConnectorForm() {
  // Reset specific fields when changing connector type
  if (editedConnector.value.type === 'Database') {
    editedConnector.value.database = {
      provider: 'SQL Server',
      server: '',
      port: '',
      databaseName: ''
    };
  } else if (editedConnector.value.type === 'File') {
    editedConnector.value.file = {
      storageType: 'Local',
      path: '',
      fileType: 'CSV',
      delimiter: ','
    };
  } else if (editedConnector.value.type === 'API') {
    editedConnector.value.api = {
      baseUrl: '',
      authType: 'None',
      dataFormat: 'JSON'
    };
  }
  
  updateCredentialsFields();
}

function updateCredentialsFields() {
  if (!editedConnector.value.requiresCredentials) {
    editedConnector.value.credentials = {
      username: '',
      password: '',
      apiKey: '',
      token: ''
    };
  }
}

function confirmDelete(connector) {
  connectorToDelete.value = connector;
  showDeleteDialog.value = true;
}

async function deleteConnector() {
  try {
    deletingConnector.value = true;
    
    await deleteConnectorAPI(connectorToDelete.value.id);
    
    // Remove from local array
    const index = connectors.value.findIndex(c => c.id === connectorToDelete.value.id);
    if (index !== -1) {
      connectors.value.splice(index, 1);
    }
    
    showNotification(t('connectors.deleteSuccess'), 'success');
    
    showDeleteDialog.value = false;
    connectorToDelete.value = null;
  } catch (error) {
    console.error('Error deleting connector:', error);
    
    // Extract error message from API response
    let errorMessage = t('connectors.deleteError');
    
    if (error.response?.data?.detail) {
      errorMessage = error.response.data.detail;
    } else if (error.response?.data?.title) {
      errorMessage = error.response.data.title;
    } else if (error.userMessage) {
      errorMessage = error.userMessage;
    } else if (error.message) {
      errorMessage = error.message;
    }
    
    showNotification(errorMessage, 'error', 6000); // Longer timeout for error messages
  } finally {
    deletingConnector.value = false;
  }
}

const validateSchema = async (connector) => {
  // Since we're using mock data, just return success
  return { isValid: true, errors: [] };
};

const validateDatabaseSchema = async (connector) => {
  return { isValid: true, errors: [] };
};

const validateFileSchema = async (connector) => {
  return { isValid: true, errors: [] };
};

const validateApiSchema = async (connector) => {
  return { isValid: true, errors: [] };
};

async function saveConnector() {
  try {
    // Check if this is an edit and schema has changed
    if (editedConnector.value.id && schemaHasChanged.value) {
      // Check if connector is used in any pipelines
      const pipelines = await findPipelinesUsingConnector(editedConnector.value.id);
      
      if (pipelines.length > 0) {
        // Show warning dialog
        affectedPipelines.value = pipelines;
        showSchemaWarningDialog.value = true;
        return; // Don't save yet, wait for user confirmation
      }
    }
    
    // Proceed with save
    await performSave();
  } catch (error) {
    console.error('Error saving data source:', error);
  }
}

async function handleWizardSave(connector) {
  try {
    // Check if this is an edit and schema has changed
    if (connector.id && schemaHasChanged.value) {
      // Check if connector is used in any pipelines
      const pipelines = await findPipelinesUsingConnector(connector.id);
      
      if (pipelines.length > 0) {
        // Show warning dialog
        affectedPipelines.value = pipelines;
        showSchemaWarningDialog.value = true;
        return; // Don't save yet, wait for user confirmation
      }
    }
    
    // Proceed with save
    await performSave(connector);
  } catch (error) {
    console.error('Error saving data source:', error);
  }
}

async function performSave(connector = null) {
  try {
    savingConnector.value = true;
    
    const dataToSave = connector || editedConnector.value;
    let savedConnector;
    
    if (dataToSave.id) {
      // Update existing connector
      savedConnector = await updateConnector(dataToSave.id, dataToSave);
      const index = connectors.value.findIndex(ds => ds.id === dataToSave.id);
      if (index !== -1) {
        connectors.value[index] = savedConnector;
      }
    } else {
      // Create new connector
      savedConnector = await createConnector(dataToSave);
      connectors.value.push(savedConnector);
    }
    
    showCreateDialog.value = false;
    showSchemaWarningDialog.value = false;
    
    // Reset form to empty state
    editedConnector.value = createEmptyConnector();
    originalSchema.value = null;
    schemaHasChanged.value = false;
  } catch (error) {
    console.error('Error saving connector:', error);
  } finally {
    savingConnector.value = false;
  }
}

function handleSchemaWarningCancel() {
  showSchemaWarningDialog.value = false;
}

function handleSchemaWarningProceed() {
  performSave();
}

function createNewConnector() {
  router.push('/connectors/new');
}

function handleDialogClose(isOpen) {
  if (!isOpen) {
    // Dialog is closing - reset form if not saving
    if (!savingConnector.value) {
      editedConnector.value = createEmptyConnector();
      originalSchema.value = null;
      schemaHasChanged.value = false;
    }
  }
}

async function testConnection(connector) {
  try {
    connectionTestConnector.value = connector;
    showConnectionDialog.value = true;
    testingConnection.value = true;
    connectionTestResult.value = false;
    
    // Use the endpoint that tests existing connectors by ID
    const result = await testExistingConnection(connector.id);
    
    connectionTestSuccess.value = result.success;
    connectionTestMessage.value = result.success
      ? `Successfully connected to ${connector.name}`
      : `Failed to connect to ${connector.name}: ${result.message}`;
    
    connectionTestResult.value = true;
  } catch (error) {
    console.error('Error testing connection:', error);
    connectionTestSuccess.value = false;
    // Show backend validation error if available
    const errorMessage = error.response?.data?.message || error.response?.data?.title || error.message;
    connectionTestMessage.value = `Error testing connection: ${errorMessage}`;
    connectionTestResult.value = true;
  } finally {
    testingConnection.value = false;
  }
}

async function viewSchema(connector) {
  try {
    selectedConnector.value = connector;
    showSchemaDialog.value = true;
    loadingSchema.value = true;
    connectorSchema.value = null;
    schemaError.value = null;
    
    // Fetch the full connector details to get the schema
    const fullConnector = await fetchConnectorById(connector.id);
    
    if (!fullConnector.schema || !fullConnector.schema.fields || fullConnector.schema.fields.length === 0) {
      schemaError.value = 'No schema defined for this connector';
    } else {
      connectorSchema.value = fullConnector.schema;
    }
  } catch (error) {
    console.error('Error loading schema:', error);
    schemaError.value = error.message || 'Failed to load schema';
  } finally {
    loadingSchema.value = false;
  }
}

function getMethodColor(method) {
  const colors = {
    'GET': 'success',
    'POST': 'primary',
    'PUT': 'warning',
    'DELETE': 'error',
    'PATCH': 'info'
  };
  return colors[method] || 'grey';
}

let tenantSubscription = null;

onMounted(async () => {
  await fetchConnectors();
  
  // Check if we need to open the create dialog from route
  if (route.query.action === 'create') {
    showCreateDialog.value = true;
  }
  
  // Refetch if tenant changes
  tenantSubscription = tenantStore.$subscribe(() => {
    if (tenantStore.currentTenantId) {
      fetchConnectors();
    }
  });
});

onBeforeUnmount(() => {
  // Clean up tenant subscription
  if (tenantSubscription) {
    tenantSubscription();
  }
  
  // Reset all state
  showCreateDialog.value = false;
  showDeleteDialog.value = false;
  showConnectionDialog.value = false;
  showSchemaDialog.value = false;
  connectorToDelete.value = null;
  connectionTestConnector.value = null;
  selectedConnector.value = null;
  editedConnector.value = createEmptyConnector();
});
</script>

<style scoped>
/* Theme-aware styling */
:deep(.v-card) {
  border-radius: var(--app-border-radius);
  transition: all var(--app-transition-speed) ease;
}

:deep(.v-dialog > .v-card) {
  border-radius: var(--app-border-radius);
  overflow: hidden;
}

:deep(.v-list-item:hover) {
  background-color: rgba(var(--v-theme-primary), 0.05);
}

.execution-row {
  transition: background-color 0.2s ease;
  cursor: pointer;
}

.execution-row:hover {
  background-color: rgba(var(--v-theme-primary), 0.05);
}


</style>
