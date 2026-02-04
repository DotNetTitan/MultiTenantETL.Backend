<template>
  <div>
    <div class="d-flex align-center mb-4">
      <h1 class="text-h4 mr-4">{{ $t('pipelines.title') }}</h1>
      <v-spacer />
      <v-btn 
        color="primary" 
        @click="openCreatePipelineDialog"
      >
        <v-icon v-if="$vuetify.display.smAndUp" class="mr-2">mdi-plus</v-icon>
        <span v-if="$vuetify.display.xs">{{ $t('common.create') }}</span>
        <span v-else>{{ $t('pipelines.createPipeline') }}</span>
      </v-btn>
    </div>

    <v-card>
      <v-card-text>
        <v-row>
          <v-col cols="12" md="4">
            <v-text-field
              v-model="search"
              :label="$t('pipelines.searchPipelines')"
              prepend-inner-icon="mdi-magnify"
              density="compact"
              hide-details
              class="mb-4"
              @update:model-value="loadPipelines"
            />
          </v-col>
          <v-col cols="12" md="3">
            <v-select
              v-model="statusFilter"
              :label="$t('common.status')"
              :items="statusOptions"
              density="compact"
              hide-details
              class="mb-4"
              @update:model-value="loadPipelines"
            />
          </v-col>
          <v-col cols="12" md="2">
            <v-select
              v-model="isActiveFilter"
              :label="$t('common.active')"
              :items="activeOptions"
              density="compact"
              hide-details
              class="mb-4"
              @update:model-value="loadPipelines"
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
              @update:model-value="loadPipelines"
            />
          </v-col>
        </v-row>

        <v-data-table
          :headers="headers"
          :items="pipelines"
          :loading="loading"
          :items-per-page="10"
          class="mt-2"
        >
          <template #item.status="{ item }">
            <v-chip
              :color="getStatusColor(item.status)"
              text-color="white"
              size="small"
            >
              {{ getStatusLabel(item.status) }}
            </v-chip>
          </template>
          <template #item.lastRunAt="{ item }">
            {{ item.lastRunAt ? formatDate(item.lastRunAt) : $t('pipelines.never') }}
          </template>
          <template #item.isScheduled="{ item }">
            <v-icon
              v-if="item.isScheduled"
              icon="mdi-check"
              color="success"
              size="small"
            />
            <span v-else class="text-grey">—</span>
          </template>
          <template #item.isActive="{ item }">
            <v-switch
              :model-value="item.isActive"
              color="success"
              hide-details
              density="compact"
              :disabled="item.status === 'Running'"
              @update:model-value="handleToggleActive(item)"
            />
          </template>
          <template #item.actions="{ item }">
            <v-btn
              icon
              variant="text"
              size="small"
              :to="`/pipelines/${item.id}`"
              :title="$t('pipelines.viewDetails')"
            >
              <v-icon>mdi-eye</v-icon>
            </v-btn>
            <v-btn
              icon
              variant="text"
              size="small"
              :title="$t('common.edit')"
              @click="openEditDialog(item)"
            >
              <v-icon>mdi-pencil</v-icon>
            </v-btn>
            <v-btn
              icon
              variant="text"
              size="small"
              color="info"
              :title="$t('pipelines.viewMappings')"
              @click="viewMappings(item)"
            >
              <v-icon>mdi-map-marker-path</v-icon>
            </v-btn>
            <v-btn
              icon
              variant="text"
              size="small"
              color="success"
              :disabled="item.status === 'Running' || !item.isActive"
              :title="$t('pipelines.executePipeline')"
              @click="handleExecutePipeline(item)"
            >
              <v-icon>mdi-play</v-icon>
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

    <!-- Create/Edit Pipeline Dialog -->
    <v-dialog
      v-model="showCreateDialog"
      :fullscreen="isDialogFullscreen || $vuetify.display.mobile"
      :max-width="isDialogFullscreen ? undefined : '1400px'"
      persistent
      scrollable
    >
      <PipelineWizard
        :pipeline="editedPipeline"
        :connectors="connectors"
        :transformations="availableTransformations"
        :timezones="timezones"
        @save="handleSavePipeline"
        @close="showCreateDialog = false"
        @create-connector="goToCreateConnector"
        @add-transformation="addTransformation"
        @toggle-fullscreen="isDialogFullscreen = $event"
      />
    </v-dialog>

    <!-- Delete Confirmation Dialog -->
    <v-dialog
      v-model="showDeleteDialog"
      max-width="400px"
    >
      <v-card>
        <v-card-title class="text-h5">
          {{ $t('pipelines.deletePipeline') }}
        </v-card-title>
        <v-card-text>
          {{ $t('pipelines.deleteConfirm', { name: pipelineToDelete?.name }) }}
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
            :loading="deletingPipeline"
            @click="handleDeletePipeline"
          >
            {{ $t('common.delete') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Field Mappings Viewer Dialog -->
    <v-dialog
      v-model="showMappingsDialog"
      max-width="1200px"
    >
      <v-card>
        <v-card-title class="d-flex align-center pa-4">
          <v-icon class="mr-2" color="primary">mdi-map-marker-path</v-icon>
          <span class="text-h5 font-weight-medium">{{ selectedPipeline?.name }} - {{ $t('pipelines.fieldMappings') }}</span>
          <v-spacer />
          <v-btn
            icon
            variant="text"
            density="comfortable"
            @click="showMappingsDialog = false"
          >
            <v-icon>mdi-close</v-icon>
          </v-btn>
        </v-card-title>
        
        <v-card-text>
          <div v-if="loadingMappings" class="text-center py-8">
            <v-progress-circular indeterminate color="primary" size="64" />
            <div class="mt-4">{{ $t('pipelines.loadingMappings') }}</div>
          </div>
          
          <div v-else-if="mappingsError" class="text-center py-8">
            <v-icon size="64" color="error">mdi-alert-circle</v-icon>
            <div class="mt-4 text-error">{{ mappingsError }}</div>
          </div>
          
          <div v-else-if="pipelineMappings">
            <!-- Mappings List -->
            <MappingViewer 
              :mappings="pipelineMappings" 
              :source-name="selectedPipeline?.sourceName"
              :destination-name="selectedPipeline?.destinationName"
            />
          </div>
        </v-card-text>
        
        <v-card-actions>
          <v-spacer />
          <v-btn
            color="primary"
            @click="showMappingsDialog = false"
          >
            {{ $t('common.close') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup>
import { ref, onMounted, watch, computed } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import { useI18n } from 'vue-i18n';
import PipelineWizard from '@/components/pipeline/PipelineWizard.vue';
import FieldMappingEditor from '@/components/pipeline/FieldMappingEditor.vue';
import MappingViewer from '@/components/pipeline/MappingViewer.vue';
import { fetchPipelineById } from '@/services/pipelineService';
import { usePipeline } from '@/composables/usePipeline';
import { usePipelineForm } from '@/composables/usePipelineForm';

const router = useRouter();
const route = useRoute();
const { t } = useI18n();

// Get functionality from main pipeline composable
const {
  pipelines,
  loading,
  savingPipeline,
  deletingPipeline,
  search,
  statusFilter,
  isActiveFilter,
  sortBy,
  statusOptions,
  activeOptions,
  sortOptions,
  loadPipelines,
  savePipeline,
  deletePipeline,
  executePipeline,
  togglePipelineStatus,
  getStatusColor,
  formatDate,
  setupTenantSubscription
} = usePipeline();

// Get functionality from the form composable
const {
  form,
  editedPipeline,
  connectors,
  fetchConnectors,
  prepareEditPipeline,
  resetForm,
  timezones
} = usePipelineForm();

// Field mapping state
const mappingValidation = ref({ isValid: true, errors: [], unmappedRequiredFields: [] });

// Field mapping methods
function handleMappingValidation(result) {
  mappingValidation.value = result;
}

function getConnectorId(sourceIdOrObject) {
  return typeof sourceIdOrObject === 'string' ? sourceIdOrObject : sourceIdOrObject?.id;
}

// Data table headers
const headers = computed(() => [
  { title: t('common.name'), key: 'name' },
  { title: t('pipelines.source'), key: 'sourceName' },
  { title: t('pipelines.destination'), key: 'destinationName' },
  { title: t('common.status'), key: 'status', width: '120px' },
  { title: t('pipelines.lastRun'), key: 'lastRunAt', width: '150px' },
  { title: t('pipelines.scheduled'), key: 'isScheduled', width: '100px' },
  { title: t('common.active'), key: 'isActive', sortable: false, width: '100px' },
  { title: t('common.actions'), key: 'actions', sortable: false, width: '120px', align: 'end' }
]);

// Dialog controls
const showCreateDialog = ref(false);
const showDeleteDialog = ref(false);
const showMappingsDialog = ref(false);
const pipelineToDelete = ref(null);
const selectedPipeline = ref(null);
const isDialogFullscreen = ref(false);
const loadingMappings = ref(false);
const pipelineMappings = ref(null);
const mappingsError = ref(null);

function openEditDialog(pipeline) {
  router.push(`/pipelines/${pipeline.id}/edit`);
}

function openCreatePipelineDialog() {
  router.push('/pipelines/new');
}

function confirmDelete(pipeline) {
  pipelineToDelete.value = pipeline;
  showDeleteDialog.value = true;
}

async function handleDeletePipeline() {
  try {
    await deletePipeline(pipelineToDelete.value.id);
    showDeleteDialog.value = false;
    pipelineToDelete.value = null;
  } catch (error) {
    console.error('Error deleting pipeline:', error);
  }
}

async function handleSavePipeline() {
  if (!mappingValidation.value.isValid) {
    alert('Please fix field mapping errors before saving');
    return;
  }
  
  try {
    const pipelineToSave = { 
      ...editedPipeline.value, 
      sourceId: editedPipeline.value.sourceId?.id || editedPipeline.value.sourceId,
      destinationId: editedPipeline.value.destinationId?.id || editedPipeline.value.destinationId
    };
    await savePipeline(pipelineToSave);
    showCreateDialog.value = false;
    await loadPipelines();
  } catch (error) {
    console.error('Error saving pipeline:', error);
  }
}

async function handleExecutePipeline(pipeline) {
  try {
    await executePipeline(pipeline.id);
  } catch (error) {
    console.error('Error executing pipeline:', error);
  }
}

async function handleToggleActive(pipeline) {
  try {
    await togglePipelineStatus(pipeline.id);
  } catch (error) {
    console.error('Error toggling pipeline status:', error);
  }
}

function goToCreateConnector() {
  showCreateDialog.value = false;
  router.push('/connectors?action=create');
}

function getStatusLabel(status) {
  const statusMap = {
    'Running': t('executions.running'),
    'Completed': t('executions.completed'),
    'Failed': t('executions.failed'),
    'Cancelled': t('executions.cancelled'),
    'Idle': t('executions.idle')
  };
  return statusMap[status] || status;
}



async function viewMappings(pipeline) {
  try {
    selectedPipeline.value = pipeline;
    showMappingsDialog.value = true;
    loadingMappings.value = true;
    pipelineMappings.value = null;
    mappingsError.value = null;
    
    // Fetch the full pipeline details including mappings
    const fullPipeline = await fetchPipelineById(pipeline.id);
    
    // Fetch source and destination connectors to get schemas
    const { fetchConnectorById } = await import('@/services/connectorService');
    const [sourceConnector, destinationConnector] = await Promise.all([
      fetchConnectorById(fullPipeline.sourceConnectorId),
      fetchConnectorById(fullPipeline.destinationConnectorId)
    ]);
    
    // Helper to get field type from schema (case-insensitive)
    const getFieldType = (fieldName, schema) => {
      if (!schema || !fieldName) {
        return null;
      }
      
      // Handle both schema.fields and schema being the fields array directly
      const fields = schema.fields || (Array.isArray(schema) ? schema : null);
      
      if (!fields || !Array.isArray(fields)) {
        return null;
      }
      
      // Case-insensitive field name matching
      const fieldNameLower = fieldName.toLowerCase();
      const field = fields.find(f => f.name && f.name.toLowerCase() === fieldNameLower);
      
      if (!field) {
        return null;
      }
      
      // Backend uses 'dataType', frontend might use 'type'
      return field.dataType || field.type || null;
    };
    
    // Transform field mappings to match dialog format
    pipelineMappings.value = (fullPipeline.fieldMappings || []).map(mapping => {
      const sourceField = mapping.sourceFields && mapping.sourceFields.length > 0 
        ? mapping.sourceFields.join(', ') 
        : '';
      
      // Get types for all source fields
      let sourceFieldType = null;
      if (mapping.sourceFields && mapping.sourceFields.length > 0) {
        if (mapping.sourceFields.length === 1) {
          sourceFieldType = getFieldType(mapping.sourceFields[0], sourceConnector.schema);
        } else {
          // For multiple fields, show all types
          const types = mapping.sourceFields
            .map(field => getFieldType(field, sourceConnector.schema))
            .filter(type => type !== null);
          sourceFieldType = types.length > 0 ? types.join(', ') : null;
        }
      }
      
      const destinationFieldType = getFieldType(mapping.destinationField, destinationConnector.schema);
      
      // Get transformation details from inline transformations
      let transformation = null;
      if (mapping.transformations && mapping.transformations.length > 0) {
        // Get all unique transformation types
        const uniqueTypes = [...new Set(mapping.transformations.map(t => t.type).filter(Boolean))];
        const typeDisplay = uniqueTypes.length > 0 ? uniqueTypes.join(', ') : 'Transformation';
        
        transformation = {
          type: typeDisplay,
          name: uniqueTypes.length === 1 
            ? (mapping.transformations[0].name || typeDisplay)
            : `${mapping.transformations.length} transformations`,
          description: mapping.transformations.length > 1 
            ? `${mapping.transformations.length} transformation(s) applied: ${typeDisplay}` 
            : (mapping.transformations[0].description || `${typeDisplay} transformation`)
        };
      }
      
      return {
        sourceField,
        destinationField: mapping.destinationField || '',
        sourceFieldType,
        destinationFieldType,
        transformation
      };
    });
  } catch (error) {
    console.error('Error loading mappings:', error);
    mappingsError.value = error.message || 'Failed to load mappings';
  } finally {
    loadingMappings.value = false;
  }
}

onMounted(async () => {
  // Check if there's a status filter in the URL query parameter
  if (route.query.status) {
    const statusFromUrl = route.query.status.toLowerCase();
    
    if (statusFromUrl === 'active') {
      isActiveFilter.value = 'Active';
    } else {
      // Map URL status to internal status values for execution status
      const statusMap = {
        'running': 'Running',
        'idle': 'Idle',
        'failed': 'Failed',
        'completed': 'Completed'
      };
      
      if (statusMap[statusFromUrl]) {
        statusFilter.value = statusMap[statusFromUrl];
      }
    }
  }
  
  loadPipelines();
  setupTenantSubscription();
});

watch(showCreateDialog, (newValue) => {
  if (!newValue) {
    // Optionally reset form state when dialog is closed
  }
});

</script>

<style scoped>
/* Add any custom styles here */
</style>
