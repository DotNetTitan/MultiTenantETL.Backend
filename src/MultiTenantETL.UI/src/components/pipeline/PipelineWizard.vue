<template>
  <v-card class="wizard-card" elevation="0">
    <v-stepper v-model="currentStep" alt-labels flat class="wizard-stepper">
      <v-stepper-header>
        <v-stepper-item
          :complete="currentStep > 1"
          :value="1"
          :title="$t('common.basicInfo')"
          :subtitle="$t('pipelines.nameAndDescription')"
        />
        <v-icon class="step-arrow">mdi-chevron-right</v-icon>
        <v-stepper-item
          :complete="currentStep > 2"
          :value="2"
          :title="$t('pipelines.sourceAndDestination')"
          :subtitle="$t('pipelines.selectConnectors')"
        />
        <v-icon class="step-arrow">mdi-chevron-right</v-icon>
        <v-stepper-item
          :complete="currentStep > 3"
          :value="3"
          :title="$t('pipelines.fieldMappings')"
          :subtitle="$t('pipeline.mapSourceToDestination')"
        />
        <v-icon class="step-arrow">mdi-chevron-right</v-icon>
        <v-stepper-item
          :complete="currentStep > 4"
          :value="4"
          :title="$t('pipelines.schedule')"
          :subtitle="$t('pipelines.configureSchedule')"
        />
        <v-icon class="step-arrow">mdi-chevron-right</v-icon>
        <v-stepper-item
          :value="5"
          :title="$t('common.reviewAndSave')"
          :subtitle="$t('common.reviewConfiguration')"
        />
      </v-stepper-header>

      <v-stepper-window class="stepper-window">
        <!-- Step 1: Basic Info -->
        <v-stepper-window-item :value="1">
          <div class="pa-6">
            <v-row>
              <v-col cols="12">
                <v-text-field
                  v-model="pipeline.name"
                  :label="$t('pipelines.pipelineName')"
                  :placeholder="$t('pipelines.pipelineNamePlaceholder')"
                  variant="outlined"
                  :rules="[v => !!v || $t('validation.required', { field: $t('common.name') })]"
                  required
                />
              </v-col>
              <v-col cols="12">
                <v-textarea
                  v-model="pipeline.description"
                  :label="$t('common.description')"
                  :placeholder="$t('pipelines.descriptionPlaceholder')"
                  variant="outlined"
                  rows="3"
                />
              </v-col>
              <v-col cols="12">
                <v-switch
                  v-model="pipeline.isActive"
                  :label="$t('common.active')"
                  color="success"
                  hide-details
                />
              </v-col>
            </v-row>
          </div>
        </v-stepper-window-item>

        <!-- Step 2: Source & Destination -->
        <v-stepper-window-item :value="2">
          <div class="pa-6">
            <v-row>
              <v-col cols="12" md="6">
                <v-card variant="outlined" class="pa-4">
                  <div class="d-flex align-center mb-3">
                    <v-icon color="blue" class="mr-2">mdi-database-export</v-icon>
                    <span class="text-h6">{{ $t('pipelines.source') }}</span>
                  </div>
                  <v-select
                    v-model="pipeline.sourceId"
                    :items="sourceConnectors"
                    item-title="name"
                    item-value="id"
                    :label="$t('pipelines.selectSource')"
                    variant="outlined"
                    :rules="[v => !!v || $t('pipelines.sourceRequired')]"
                  />
                </v-card>
              </v-col>
              <v-col cols="12" md="6">
                <v-card variant="outlined" class="pa-4">
                  <div class="d-flex align-center mb-3">
                    <v-icon color="green" class="mr-2">mdi-database-import</v-icon>
                    <span class="text-h6">{{ $t('pipelines.destination') }}</span>
                  </div>
                  <v-select
                    v-model="pipeline.destinationId"
                    :items="destinationConnectors"
                    item-title="name"
                    item-value="id"
                    :label="$t('pipelines.selectDestination')"
                    variant="outlined"
                    :rules="[v => !!v || $t('pipelines.destinationRequired')]"
                  />
                </v-card>
              </v-col>
            </v-row>
          </div>
        </v-stepper-window-item>

        <!-- Step 3: Field Mappings -->
        <v-stepper-window-item :value="3">
          <div class="pa-6 step-3-content">
            <FieldMappingEditor
              v-if="pipeline.sourceId && pipeline.destinationId"
              v-model="pipeline.fieldMappings"
              :source-id="getConnectorId(pipeline.sourceId)"
              :destination-id="getConnectorId(pipeline.destinationId)"
              @validate="handleMappingValidation"
            />
            <v-alert v-else type="info" variant="tonal">
              {{ $t('pipelines.selectSourceDestinationFirst') }}
            </v-alert>
          </div>
        </v-stepper-window-item>

        <!-- Step 4: Schedule -->
        <v-stepper-window-item :value="4">
          <div class="pa-6">
            <div v-if="!pipeline.id" class="text-center py-8">
              <v-icon size="64" color="grey-lighten-1" class="mb-4">mdi-calendar-clock</v-icon>
              <div class="text-h6 mb-2">{{ $t('pipelines.scheduleAfterSave') }}</div>
              <div class="text-body-2 text-grey mb-4">
                {{ $t('pipelines.scheduleAfterSaveDescription') }}
              </div>
              <v-btn
                color="primary"
                variant="outlined"
                prepend-icon="mdi-arrow-right"
                @click="currentStep = 5"
              >
                {{ $t('pipelines.continueToReview') }}
              </v-btn>
            </div>
            
            <div v-else>
              <v-card variant="outlined" class="mb-4">
                <v-card-title class="d-flex align-center">
                  <v-icon class="mr-2">mdi-calendar-clock</v-icon>
                  {{ $t('pipelines.pipelineSchedule') }}
                  <v-spacer />
                  <v-btn
                    v-if="!pipelineSchedule"
                    color="primary"
                    variant="tonal"
                    size="small"
                    prepend-icon="mdi-plus"
                    @click="openScheduleDialog"
                  >
                    {{ $t('schedules.createSchedule') }}
                  </v-btn>
                </v-card-title>
                <v-card-text>
                  <div v-if="loadingSchedules" class="text-center py-4">
                    <v-progress-circular indeterminate color="primary" />
                  </div>
                  <div v-else-if="!pipelineSchedule" class="text-center py-4">
                    <v-icon size="48" color="grey-lighten-1" class="mb-2">mdi-calendar-blank</v-icon>
                    <div class="text-body-2 text-grey">{{ $t('pipelines.noScheduleConfigured') }}</div>
                  </div>
                  <div v-else>
                    <v-list density="compact">
                      <v-list-item>
                        <template #prepend>
                          <v-icon :color="pipelineSchedule.isActive ? 'success' : 'grey'">
                            {{ pipelineSchedule.isActive ? 'mdi-clock-check' : 'mdi-clock-outline' }}
                          </v-icon>
                        </template>
                        <v-list-item-title>
                          <code class="text-primary">{{ pipelineSchedule.cronExpression }}</code>
                          <v-chip size="x-small" :color="pipelineSchedule.isActive ? 'success' : 'grey'" class="ml-2">
                            {{ pipelineSchedule.isActive ? $t('schedules.active') : $t('schedules.inactive') }}
                          </v-chip>
                        </v-list-item-title>
                        <v-list-item-subtitle>
                          {{ pipelineSchedule.timezone }}
                          <span v-if="pipelineSchedule.description"> - {{ pipelineSchedule.description }}</span>
                        </v-list-item-subtitle>
                        <v-list-item-subtitle v-if="pipelineSchedule.nextRun">
                          {{ $t('schedules.nextRun') }}: {{ formatScheduleDate(pipelineSchedule.nextRun) }}
                        </v-list-item-subtitle>
                        <template #append>
                          <v-btn
                            icon
                            variant="text"
                            size="small"
                            :title="$t('common.edit')"
                            @click="openScheduleDialog"
                          >
                            <v-icon>mdi-pencil</v-icon>
                          </v-btn>
                          <v-btn
                            icon
                            variant="text"
                            size="small"
                            color="error"
                            :title="$t('common.delete')"
                            @click="confirmDeleteSchedule"
                          >
                            <v-icon>mdi-delete</v-icon>
                          </v-btn>
                        </template>
                      </v-list-item>
                    </v-list>
                  </div>
                </v-card-text>
              </v-card>
            </div>
          </div>
        </v-stepper-window-item>

        <!-- Step 5: Review & Save -->
        <v-stepper-window-item :value="5">
          <div class="pa-6">
            <div class="text-h5 mb-4">{{ $t('pipelines.reviewPipelineConfiguration') }}</div>
            
            <!-- Basic Information -->
            <v-card variant="outlined" class="mb-4">
              <v-card-title class="text-subtitle-1 bg-surface-variant">
                <v-icon class="mr-2">mdi-information</v-icon>
                {{ $t('executions.basicInformation') }}
              </v-card-title>
              <v-card-text>
                <v-list density="compact">
                  <v-list-item>
                    <template #prepend>
                      <v-icon>mdi-label</v-icon>
                    </template>
                    <v-list-item-title>{{ $t('common.name') }}</v-list-item-title>
                    <v-list-item-subtitle>{{ pipeline.name }}</v-list-item-subtitle>
                  </v-list-item>
                  <v-list-item v-if="pipeline.description">
                    <template #prepend>
                      <v-icon>mdi-text</v-icon>
                    </template>
                    <v-list-item-title>{{ $t('common.description') }}</v-list-item-title>
                    <v-list-item-subtitle>{{ pipeline.description }}</v-list-item-subtitle>
                  </v-list-item>
                </v-list>
              </v-card-text>
            </v-card>

            <!-- Connectors -->
            <v-card variant="outlined" class="mb-4">
              <v-card-title class="text-subtitle-1 bg-surface-variant">
                <v-icon class="mr-2">mdi-database-sync</v-icon>
                {{ $t('connectors.title') }}
              </v-card-title>
              <v-card-text>
                <v-list density="compact">
                  <v-list-item>
                    <template #prepend>
                      <v-icon color="blue">mdi-database-export</v-icon>
                    </template>
                    <v-list-item-title>{{ $t('pipelines.source') }}</v-list-item-title>
                    <v-list-item-subtitle>{{ getConnectorName(pipeline.sourceId) }}</v-list-item-subtitle>
                  </v-list-item>
                  <v-list-item>
                    <template #prepend>
                      <v-icon color="green">mdi-database-import</v-icon>
                    </template>
                    <v-list-item-title>{{ $t('pipelines.destination') }}</v-list-item-title>
                    <v-list-item-subtitle>{{ getConnectorName(pipeline.destinationId) }}</v-list-item-subtitle>
                  </v-list-item>
                </v-list>
              </v-card-text>
            </v-card>

            <!-- Field Mappings -->
            <v-card variant="outlined" class="mb-4">
              <v-card-title class="text-subtitle-1 bg-surface-variant">
                <v-icon class="mr-2">mdi-arrow-left-right</v-icon>
                {{ $t('pipelines.fieldMappings') }} ({{ pipeline.fieldMappings?.length || 0 }})
              </v-card-title>
              <v-card-text>
                <v-table v-if="pipeline.fieldMappings && pipeline.fieldMappings.length > 0" density="compact">
                  <thead>
                    <tr>
                      <th>{{ $t('pipelines.sourceFieldsPlural') }}</th>
                      <th>{{ $t('transformations.title') }}</th>
                      <th>{{ $t('pipelines.destinationField') }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr v-for="mapping in pipeline.fieldMappings" :key="mapping.id">
                      <td>
                        <v-chip
                          v-for="(field, idx) in mapping.sourceFields"
                          :key="idx"
                          size="x-small"
                          class="mr-1"
                          variant="tonal"
                        >
                          {{ field }}
                        </v-chip>
                        <span v-if="!mapping.sourceFields || mapping.sourceFields.length === 0" class="text-grey">-</span>
                      </td>
                      <td>
                        <div v-if="mapping.transformations && mapping.transformations.length > 0" class="d-flex align-center gap-1">
                          <v-chip
                            v-for="(trans, idx) in mapping.transformations"
                            :key="idx"
                            size="x-small"
                            variant="outlined"
                            :color="getTransformationColor(trans.type)"
                            class="mr-1"
                          >
                            <v-icon start size="x-small">{{ getTransformationIcon(trans.type) }}</v-icon>
                            {{ idx + 1 }}. {{ trans.type }}
                          </v-chip>
                        </div>
                        <span v-else class="text-grey">{{ $t('common.none') }}</span>
                      </td>
                      <td>{{ mapping.destinationField || '-' }}</td>
                    </tr>
                  </tbody>
                </v-table>
                <div v-else class="text-center py-4 text-grey">
                  {{ $t('pipelines.noFieldMappingsConfigured') }}
                </div>
              </v-card-text>
            </v-card>

            <!-- Schedule -->
            <v-card variant="outlined" class="mb-4">
              <v-card-title class="text-subtitle-1 bg-surface-variant">
                <v-icon class="mr-2">mdi-calendar-clock</v-icon>
                {{ $t('pipelines.schedule') }}
              </v-card-title>
              <v-card-text>
                <div v-if="!pipeline.id" class="text-center py-4">
                  <v-icon color="grey-lighten-1" class="mb-2">mdi-information-outline</v-icon>
                  <div class="text-body-2 text-grey">{{ $t('pipelines.scheduleAfterSaveShort') }}</div>
                </div>
                <div v-else-if="loadingSchedules" class="text-center py-4">
                  <v-progress-circular indeterminate size="24" color="primary" />
                </div>
                <div v-else-if="!pipelineSchedule" class="text-center py-4">
                  <v-icon color="grey-lighten-1" class="mb-2">mdi-calendar-blank</v-icon>
                  <div class="text-body-2 text-grey">{{ $t('pipelines.noScheduleConfigured') }}</div>
                </div>
                <v-list v-else density="compact">
                  <v-list-item>
                    <template #prepend>
                      <v-icon :color="pipelineSchedule.isActive ? 'success' : 'grey'">
                        {{ pipelineSchedule.isActive ? 'mdi-clock-check' : 'mdi-clock-outline' }}
                      </v-icon>
                    </template>
                    <v-list-item-title>
                      <code>{{ pipelineSchedule.cronExpression }}</code>
                      <v-chip size="x-small" :color="pipelineSchedule.isActive ? 'success' : 'grey'" class="ml-2">
                        {{ pipelineSchedule.isActive ? $t('schedules.active') : $t('schedules.inactive') }}
                      </v-chip>
                    </v-list-item-title>
                    <v-list-item-subtitle>
                      {{ pipelineSchedule.timezone }}
                      <span v-if="pipelineSchedule.description"> - {{ pipelineSchedule.description }}</span>
                    </v-list-item-subtitle>
                  </v-list-item>
                </v-list>
              </v-card-text>
            </v-card>
          </div>
        </v-stepper-window-item>
      </v-stepper-window>
    </v-stepper>

    <v-divider />

    <v-card-actions class="pa-4">
      <v-btn
        v-if="currentStep > 1"
        variant="outlined"
        prepend-icon="mdi-chevron-left"
        @click="currentStep--"
      >
        {{ $t('common.previous') }}
      </v-btn>
      <v-spacer />
      <v-btn
        v-if="currentStep < 5"
        color="primary"
        variant="elevated"
        append-icon="mdi-chevron-right"
        :disabled="!canProceed"
        @click="handleNext"
      >
        {{ $t('common.next') }}
      </v-btn>
      <v-btn
        v-else
        color="primary"
        variant="elevated"
        prepend-icon="mdi-content-save"
        :loading="saving"
        :disabled="!canSave"
        @click="handleSave"
      >
        {{ $t('common.save') }}
        <v-tooltip activator="parent" location="top">{{ $t('pipelines.savePipeline') }}</v-tooltip>
      </v-btn>
    </v-card-actions>

    <!-- Connector Change Warning Dialog -->
    <v-dialog v-model="showConnectorChangeWarning" max-width="500">
      <v-card>
        <v-card-title class="d-flex align-center">
          <v-icon color="warning" class="mr-2">mdi-alert</v-icon>
          {{ $t('pipelines.connectorChangeWarning') }}
        </v-card-title>
        <v-card-text>
          <p>{{ $t('pipelines.connectorChangeMessage') }}</p>
          <p class="mt-2 text-warning">
            <strong>{{ $t('pipelines.existingMappingsWillBeCleared') }}</strong>
          </p>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="cancelConnectorChange">
            {{ $t('common.cancel') }}
          </v-btn>
          <v-btn color="warning" @click="confirmConnectorChange">
            {{ $t('common.continue') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Schedule Create/Edit Dialog -->
    <ScheduleDialog
      v-model="showScheduleDialog"
      :schedule="editingSchedule"
      :pipeline-id="props.pipeline.id"
      :pipeline-name="props.pipeline.name"
      :show-pipeline-selector="false"
      @saved="onScheduleSaved"
    />

    <!-- Delete Schedule Confirmation Dialog -->
    <v-dialog v-model="showDeleteScheduleDialog" max-width="500">
      <v-card>
        <v-card-title>{{ $t('schedules.deleteSchedule') }}</v-card-title>
        <v-card-text>{{ $t('schedules.deleteConfirm') }}</v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="showDeleteScheduleDialog = false">
            {{ $t('common.cancel') }}
          </v-btn>
          <v-btn color="error" :loading="deletingSchedule" @click="deleteScheduleConfirmed">
            {{ $t('common.delete') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </v-card>
</template>

<script setup>
import { ref, computed, watch, onMounted } from 'vue';
import { useI18n } from 'vue-i18n';
import { useSchedule } from '@/composables/useSchedule';
import { useGlobalState } from '@/composables/useGlobalState';
import FieldMappingEditor from './FieldMappingEditor.vue';
import ScheduleDialog from '@/components/schedules/ScheduleDialog.vue';

const { t } = useI18n();
const { 
  loadScheduleForPipeline, 
  removeSchedule
} = useSchedule();
const { showSuccess, showError } = useGlobalState();

const props = defineProps({
  pipeline: {
    type: Object,
    required: true
  },
  connectors: {
    type: Array,
    default: () => []
  },
  timezones: {
    type: Array,
    default: () => []
  }
});

const emit = defineEmits(['save', 'close', 'create-connector', 'toggle-fullscreen']);

const currentStep = ref(1);
const saving = ref(false);
const mappingValidation = ref({ isValid: true, errors: [], unmappedRequiredFields: [] });

// Track original connector IDs to detect changes
const originalSourceId = ref(null);
const originalDestinationId = ref(null);
const showConnectorChangeWarning = ref(false);

// Schedule-related state (one schedule per pipeline)
const pipelineSchedule = ref(null);
const loadingSchedules = ref(false);
const showScheduleDialog = ref(false);
const showDeleteScheduleDialog = ref(false);
const editingSchedule = ref(null);
const deletingSchedule = ref(false);

// Store original IDs on mount
onMounted(async () => {
  originalSourceId.value = props.pipeline.sourceId;
  originalDestinationId.value = props.pipeline.destinationId;
  
  // Load schedule if editing an existing pipeline
  if (props.pipeline.id) {
    await fetchPipelineSchedule();
  }
});

// Fetch schedule for this pipeline (one per pipeline)
async function fetchPipelineSchedule() {
  if (!props.pipeline.id) return;
  
  loadingSchedules.value = true;
  try {
    pipelineSchedule.value = await loadScheduleForPipeline(props.pipeline.id);
  } catch (error) {
    console.error('Failed to load schedule:', error);
    pipelineSchedule.value = null;
  } finally {
    loadingSchedules.value = false;
  }
}

function openScheduleDialog() {
  // If schedule exists, edit it; otherwise create new
  editingSchedule.value = pipelineSchedule.value ? { ...pipelineSchedule.value } : null;
  showScheduleDialog.value = true;
}

async function onScheduleSaved() {
  showSuccess(
    editingSchedule.value?.id ? t('schedules.updateSuccess') : t('schedules.createSuccess'),
    t('schedules.title')
  );
  await fetchPipelineSchedule();
}

function confirmDeleteSchedule() {
  showDeleteScheduleDialog.value = true;
}

async function deleteScheduleConfirmed() {
  if (!pipelineSchedule.value) return;
  
  deletingSchedule.value = true;
  try {
    await removeSchedule(pipelineSchedule.value.id);
    showSuccess(t('schedules.deleteSuccess'), t('schedules.title'));
    showDeleteScheduleDialog.value = false;
    pipelineSchedule.value = null;
  } catch (error) {
    showError(error.message || t('schedules.deleteError'), t('common.error'));
  } finally {
    deletingSchedule.value = false;
  }
}

function formatScheduleDate(dateString) {
  if (!dateString) return '-';
  return new Date(dateString).toLocaleString();
}

// Watch for connector changes
watch([() => props.pipeline.sourceId, () => props.pipeline.destinationId], ([newSourceId, newDestId]) => {
  const sourceChanged = originalSourceId.value && newSourceId !== originalSourceId.value;
  const destChanged = originalDestinationId.value && newDestId !== originalDestinationId.value;
  
  // If connectors changed and there are existing mappings, show warning
  if ((sourceChanged || destChanged) && props.pipeline.fieldMappings && props.pipeline.fieldMappings.length > 0) {
    showConnectorChangeWarning.value = true;
  }
});

// Filter connectors by direction for source/destination dropdowns
const sourceConnectors = computed(() => 
  props.connectors.filter(c => c.direction === 'source' || c.direction === 'both' || c.isSource)
);

const destinationConnectors = computed(() => 
  props.connectors.filter(c => c.direction === 'destination' || c.direction === 'both' || c.isDestination)
);

const canProceed = computed(() => {
  switch (currentStep.value) {
    case 1:
      return !!props.pipeline.name;
    case 2:
      return !!props.pipeline.sourceId && !!props.pipeline.destinationId;
    case 3:
      // Allow proceeding even without mappings - they can be added later
      return true;
    default:
      return true;
  }
});

const canSave = computed(() => {
  return props.pipeline.name &&
         props.pipeline.sourceId &&
         props.pipeline.destinationId;
  // Removed mapping validation requirement - allow saving pipelines without complete mappings
});

function handleMappingValidation(result) {
  mappingValidation.value = result;
}

function getConnectorId(sourceIdOrObject) {
  return typeof sourceIdOrObject === 'string' ? sourceIdOrObject : sourceIdOrObject?.id;
}

function getConnectorName(sourceIdOrObject) {
  const id = typeof sourceIdOrObject === 'string' ? sourceIdOrObject : sourceIdOrObject?.id;
  const connector = props.connectors.find(c => c.id === id);
  return connector?.name || 'Unknown';
}

function getTransformationColor(type) {
  const colors = {
    'Filter': 'blue',
    'Map': 'green',
    'Aggregation': 'orange',
    'Script': 'purple',
    'Join': 'teal',
    'Trim': 'cyan',
    'Case Convert': 'indigo',
    'Substring': 'pink',
    'Replace': 'amber',
    'Split': 'lime'
  };
  return colors[type] || 'grey';
}

function getTransformationIcon(type) {
  const icons = {
    'Filter': 'mdi-filter',
    'Map': 'mdi-map',
    'Aggregation': 'mdi-chart-bar',
    'Script': 'mdi-code-braces',
    'Join': 'mdi-link-variant',
    'Trim': 'mdi-content-cut',
    'Case Convert': 'mdi-format-letter-case',
    'Substring': 'mdi-format-text',
    'Replace': 'mdi-find-replace',
    'Split': 'mdi-call-split'
  };
  return icons[type] || 'mdi-cog';
}

function handleNext() {
  // If moving from step 2 to step 3 and connectors changed, show warning
  if (currentStep.value === 2 && showConnectorChangeWarning.value) {
    // Warning dialog will handle the navigation
    return;
  }
  currentStep.value++;
}

function confirmConnectorChange() {
  // Clear existing field mappings
  props.pipeline.fieldMappings = [];
  
  // Update original IDs to new values
  originalSourceId.value = props.pipeline.sourceId;
  originalDestinationId.value = props.pipeline.destinationId;
  
  // Close dialog and stay on current step (Step 2)
  showConnectorChangeWarning.value = false;
}

function cancelConnectorChange() {
  // Revert to original connectors
  props.pipeline.sourceId = originalSourceId.value;
  props.pipeline.destinationId = originalDestinationId.value;
  
  // Close dialog
  showConnectorChangeWarning.value = false;
}

async function handleSave() {
  saving.value = true;
  try {
    await emit('save', props.pipeline);
  } catch (error) {
    console.error('Failed to save pipeline:', error);
    // Error handling is done by parent component
  } finally {
    saving.value = false;
  }
}
</script>

<style scoped>
.wizard-card {
  display: flex;
  flex-direction: column;
}

.wizard-stepper {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
}

.wizard-stepper :deep(.v-stepper-header) {
  flex-shrink: 0;
  padding: 24px 16px;
  background: rgba(var(--v-theme-surface-variant), 0.3);
}

/* Style stepper items */
.wizard-stepper :deep(.v-stepper-item) {
  padding: 12px 16px;
}

.wizard-stepper :deep(.v-stepper-item__avatar) {
  margin-bottom: 8px;
  width: 40px;
  height: 40px;
  font-size: 18px;
  font-weight: 600;
  border: 2px solid rgba(var(--v-theme-primary), 0.3);
  background: rgba(var(--v-theme-surface), 1);
}

.wizard-stepper :deep(.v-stepper-item--selected .v-stepper-item__avatar) {
  background: rgb(var(--v-theme-primary));
  border-color: rgb(var(--v-theme-primary));
  box-shadow: 0 4px 8px rgba(var(--v-theme-primary), 0.3);
  transform: scale(1.1);
  transition: all 0.3s ease;
}

.wizard-stepper :deep(.v-stepper-item--complete .v-stepper-item__avatar) {
  background: rgb(var(--v-theme-success));
  border-color: rgb(var(--v-theme-success));
}

.wizard-stepper :deep(.v-stepper-item__title) {
  font-size: 14px;
  font-weight: 600;
  margin-bottom: 4px;
}

.wizard-stepper :deep(.v-stepper-item--selected .v-stepper-item__title) {
  color: rgb(var(--v-theme-primary));
  font-weight: 700;
}

.wizard-stepper :deep(.v-stepper-item__subtitle) {
  font-size: 12px;
  opacity: 0.7;
}

/* Step arrow between items */
.step-arrow {
  color: rgba(var(--v-theme-primary), 0.5);
  font-size: 28px;
  margin: 0 16px;
  align-self: center;
  margin-top: 20px;
}

.stepper-window {
  flex: 1;
  overflow-y: auto;
  min-height: 0;
}

.stepper-window :deep(.v-stepper-window-item) {
  background: transparent !important;
}


</style>
