<template>
  <div class="field-mapping-editor">
    <!-- Loading State -->
    <div v-if="loading" class="text-center py-8">
      <v-progress-circular indeterminate color="primary" />
      <p class="mt-4">{{ $t('pipeline.loadingSchemas') }}</p>
    </div>

    <!-- Error State -->
    <v-alert v-else-if="error" type="error" class="mb-4">
      {{ error }}
      <v-btn variant="text" @click="fetchSchemas">{{ $t('common.retry') }}</v-btn>
    </v-alert>

    <!-- Main Content -->
    <div v-else class="content-wrapper">
      <!-- Schema Viewer (collapsible) -->
      <v-expansion-panels class="mb-4">
        <v-expansion-panel>
          <v-expansion-panel-title>
            <div class="d-flex align-center">
              <v-icon size="24" class="mr-2">mdi-table-eye</v-icon>
              <span class="text-h6">{{ $t('pipeline.viewSourceDestFields') }}</span>
            </div>
          </v-expansion-panel-title>
          <v-expansion-panel-text>
            <SchemaViewer
              :source-schema="sourceSchema"
              :destination-schema="destinationSchema"
              :mappings="localMappings"
            />
          </v-expansion-panel-text>
        </v-expansion-panel>
      </v-expansion-panels>

      <!-- Mappings List -->
      <div class="mb-4">
        <v-expansion-panels v-model="mappingsExpanded">
          <v-expansion-panel value="mappings">
            <v-expansion-panel-title>
              <div class="d-flex align-center">
                <v-icon size="24" class="mr-2">mdi-map-marker-path</v-icon>
                <div>
                  <span class="text-h6">{{ $t('pipeline.fieldMappings') }}</span>
                  <span class="text-caption text-grey ml-2">({{ localMappings.length }} {{ localMappings.length === 1 ? 'mapping' : 'mappings' }})</span>
                </div>
              </div>
            </v-expansion-panel-title>
            <v-expansion-panel-text>
              <div v-if="localMappings.length === 0" class="text-center py-8">
                <v-icon size="64" color="grey-lighten-2">mdi-map-marker-path</v-icon>
                <p class="mt-4 text-body-1 text-grey">{{ $t('pipeline.noFieldMappings') }}</p>
                <p class="text-caption text-grey">{{ $t('pipeline.clickAddMapping') }}</p>
                <v-btn
                  color="primary"
                  prepend-icon="mdi-plus"
                  class="mt-4"
                  @click="addMapping"
                >
                  {{ $t('pipeline.addMapping') }}
                </v-btn>
              </div>

              <div v-else class="mappings-list">
                <MappingCard
                  v-for="(mapping, index) in localMappings"
                  :key="mapping.id"
                  :mapping="mapping"
                  :source-fields="sourceSchema.fields || []"
                  :destination-fields="destinationSchema.fields || []"
                  :index="index"
                  :can-move-up="index > 0"
                  :can-move-down="index < localMappings.length - 1"
                  class="mb-3"
                  @update:mapping="updateMapping(index, $event)"
                  @remove="removeMapping(index)"
                  @move-up="moveUp(index)"
                  @move-down="moveDown(index)"
                />
                
                <!-- Add Mapping Button at Bottom -->
                <v-btn
                  color="primary"
                  prepend-icon="mdi-plus"
                  block
                  variant="outlined"
                  size="large"
                  class="mt-2"
                  @click="addMapping"
                >
                  {{ $t('pipeline.addMapping') }}
                </v-btn>
              </div>
            </v-expansion-panel-text>
          </v-expansion-panel>
        </v-expansion-panels>
      </div>

      <!-- Validation Summary (show below mappings, only if there are errors and at least one mapping) -->
      <ValidationSummary
        v-if="hasCompleteMappings && !validationResult.isValid"
        :validation-errors="validationResult.errors"
        :unmapped-required-fields="validationResult.unmappedRequiredFields"
        :is-valid="validationResult.isValid"
        class="mb-4"
      />
    </div>

    <!-- Schema Conversion Dialog -->
    <v-dialog v-model="showConversionDialog" max-width="600">
      <v-card>
        <v-card-title class="d-flex align-center">
          {{ $t('pipeline.convertToManualSchema') }}
          <v-spacer />
          <v-btn
            icon
            variant="text"
            :disabled="convertingSchema"
            @click="showConversionDialog = false"
          >
            <v-icon>mdi-close</v-icon>
          </v-btn>
        </v-card-title>
        <v-card-text>
          <p class="mb-4">
            {{ $t('pipeline.convertDescription') }}
          </p>

          <v-alert type="info" variant="tonal" class="mb-4">
            <div class="text-subtitle-2 mb-2">{{ $t('pipeline.whatWillHappen') }}</div>
            <ul class="pl-4">
              <li v-if="autoDetectedSourceId">{{ $t('pipeline.sourceSchemaWillBeSaved') }}</li>
              <li v-if="autoDetectedDestinationId">{{ $t('pipeline.destinationSchemaWillBeSaved') }}</li>
              <li>{{ $t('pipeline.canEditSchemaLater') }}</li>
              <li>{{ $t('pipeline.fieldMappingsRemainUnchanged') }}</li>
            </ul>
          </v-alert>

          <p class="text-caption text-grey">
            {{ $t('pipeline.afterConversionNote') }}
          </p>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn :disabled="convertingSchema" @click="showConversionDialog = false">
            {{ $t('common.close') }}
          </v-btn>
          <v-btn color="primary" :loading="convertingSchema" @click="convertToManualSchema">
            {{ $t('pipeline.convertSchema') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup>
import { ref, watch, computed, onMounted } from 'vue';
import { useI18n } from 'vue-i18n';
import { 
  fetchSchema, 
  validateFieldMappings 
} from '@/services/schemaService';
import { saveSchema } from '@/services/schemaService';
import { detectSchema } from '@/services/connectorService';
import MappingCard from './MappingCard.vue';
import SchemaViewer from './SchemaViewer.vue';
import ValidationSummary from './ValidationSummary.vue';

const { t } = useI18n();

const props = defineProps({
  sourceId: {
    type: String,
    required: true
  },
  destinationId: {
    type: String,
    required: true
  },
  modelValue: {
    type: Array,
    default: () => []
  }
});

const emit = defineEmits(['update:modelValue', 'validate']);

// State
const loading = ref(false);
const error = ref(null);
const sourceSchema = ref({ fields: [] });
const destinationSchema = ref({ fields: [] });
const localMappings = ref([...props.modelValue]);
const usingAutoDetection = ref(false);
const showConversionDialog = ref(false);
const convertingSchema = ref(false);
const autoDetectedSourceId = ref(null);
const autoDetectedDestinationId = ref(null);
const mappingsExpanded = ref(null); // null = collapsed by default
const validationResult = ref({
  isValid: true,
  errors: [],
  warnings: [],
  unmappedRequiredFields: []
});

// Check if there are any complete mappings (with at least source or destination filled)
const hasCompleteMappings = computed(() => {
  return localMappings.value.some(m => 
    (m.sourceFields && m.sourceFields.length > 0) || m.destinationField
  );
});

// Watch for source/destination changes
watch([() => props.sourceId, () => props.destinationId], () => {
  fetchSchemas();
});

// Watch mappings and validate (but don't watch for external changes to avoid loop)
watch(localMappings, () => {
  validateMappings();
  emit('update:modelValue', localMappings.value);
}, { deep: true });

// Fetch schemas on mount
onMounted(() => {
  if (props.sourceId && props.destinationId) {
    fetchSchemas();
  }
});

// Methods
async function fetchSchemas() {
  if (!props.sourceId || !props.destinationId) {
    console.warn('Source or destination ID is missing');
    return;
  }
  
  loading.value = true;
  error.value = null;
  
  try {
    const [source, destination] = await Promise.all([
      fetchSchema(props.sourceId),
      fetchSchema(props.destinationId)
    ]);
    
    sourceSchema.value = source;
    destinationSchema.value = destination;
    
    // Check if either schema is using auto-detection
    usingAutoDetection.value = !source.isManual || !destination.isManual;
    
    // Track which data sources are using auto-detection
    if (!source.isManual) {
      autoDetectedSourceId.value = props.sourceId;
    }
    if (!destination.isManual) {
      autoDetectedDestinationId.value = props.destinationId;
    }
    
    // Validate existing mappings
    validateMappings();
  } catch (err) {
    error.value = `Failed to load schemas: ${err.message}`;
    console.error('Error fetching schemas:', err);
  } finally {
    loading.value = false;
  }
}

function addMapping() {
  const newMapping = {
    id: `mapping-${Date.now()}-${Math.random()}`,
    sourceFields: [],
    destinationField: '',
    transformations: [],
    order: localMappings.value.length + 1
  };
  localMappings.value.push(newMapping);
}

function updateMapping(index, updatedMapping) {
  localMappings.value[index] = updatedMapping;
}

function removeMapping(index) {
  localMappings.value.splice(index, 1);
  // Update order for remaining mappings
  localMappings.value.forEach((m, i) => {
    m.order = i + 1;
  });
}

function moveUp(index) {
  if (index > 0) {
    const temp = localMappings.value[index];
    localMappings.value[index] = localMappings.value[index - 1];
    localMappings.value[index - 1] = temp;
    
    // Update order
    localMappings.value.forEach((m, i) => {
      m.order = i + 1;
    });
  }
}

function moveDown(index) {
  if (index < localMappings.value.length - 1) {
    const temp = localMappings.value[index];
    localMappings.value[index] = localMappings.value[index + 1];
    localMappings.value[index + 1] = temp;
    
    // Update order
    localMappings.value.forEach((m, i) => {
      m.order = i + 1;
    });
  }
}

function validateMappings() {
  if (!sourceSchema.value?.fields || !destinationSchema.value?.fields) {
    validationResult.value = {
      isValid: true, // Don't block if schemas aren't loaded yet
      errors: [],
      warnings: [],
      unmappedRequiredFields: []
    };
    emit('validate', validationResult.value);
    return;
  }
  
  // Only validate complete mappings (ignore empty/incomplete ones)
  const completeMappings = localMappings.value.filter(m => 
    m.sourceFields && m.sourceFields.length > 0 && m.destinationField
  );
  
  // If there are no complete mappings at all, don't show validation errors yet
  // This prevents showing "unmapped required fields" errors when user is just starting
  if (completeMappings.length === 0) {
    validationResult.value = {
      isValid: true,
      errors: [],
      warnings: [],
      unmappedRequiredFields: []
    };
    emit('validate', validationResult.value);
    return;
  }
  
  const result = validateFieldMappings(
    completeMappings,
    sourceSchema.value,
    destinationSchema.value
  );
  
  validationResult.value = result;
  emit('validate', result);
}

async function convertToManualSchema() {
  try {
    convertingSchema.value = true;
    
    // Convert source schema if it's auto-detected
    if (autoDetectedSourceId.value) {
      const autoSchema = await detectSchema(autoDetectedSourceId.value);
      const fields = transformAutoSchemaToManual(autoSchema);
      await saveSchema(autoDetectedSourceId.value, fields);
    }
    
    // Convert destination schema if it's auto-detected
    if (autoDetectedDestinationId.value) {
      const autoSchema = await detectSchema(autoDetectedDestinationId.value);
      const fields = transformAutoSchemaToManual(autoSchema);
      await saveSchema(autoDetectedDestinationId.value, fields);
    }
    
    // Refresh schemas
    await fetchSchemas();
    
    showConversionDialog.value = false;
  } catch (err) {
    error.value = `Failed to convert schema: ${err.message}`;
    console.error('Error converting schema:', err);
  } finally {
    convertingSchema.value = false;
  }
}

function transformAutoSchemaToManual(autoSchema) {
  let fields = [];
  
  if (autoSchema.tables && autoSchema.tables.length > 0) {
    // Database schema with tables
    const autoGeneratedFields = ['id', 'createdat', 'updatedat', 'created_at', 'updated_at'];
    
    fields = autoSchema.tables[0].columns.map((col, index) => {
      const isAutoGenerated = autoGeneratedFields.includes(col.name.toLowerCase());
      return {
        id: `field-${Date.now()}-${index}`,
        name: col.name,
        type: col.type,
        nullable: col.nullable !== false,
        required: !isAutoGenerated && col.nullable === false,
        description: col.description || '',
        order: index + 1
      };
    });
  } else if (autoSchema.columns) {
    // File schema with columns
    fields = autoSchema.columns.map((col, index) => ({
      id: `field-${Date.now()}-${index}`,
      name: col.name,
      type: col.type || 'varchar',
      nullable: true,
      required: false,
      description: '',
      order: index + 1
    }));
  } else if (autoSchema.endpoints) {
    // API schema with endpoints
    const firstEndpoint = autoSchema.endpoints[0];
    fields = (firstEndpoint.fields || []).map((fieldName, index) => ({
      id: `field-${Date.now()}-${index}`,
      name: fieldName,
      type: 'varchar',
      nullable: true,
      required: false,
      description: '',
      order: index + 1
    }));
  }
  
  return fields;
}
</script>

<style scoped>
.field-mapping-editor {
  width: 100%;
  display: flex;
  flex-direction: column;
}

.content-wrapper {
  flex: 1;
  padding-right: 8px;
}

.mappings-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}
</style>
