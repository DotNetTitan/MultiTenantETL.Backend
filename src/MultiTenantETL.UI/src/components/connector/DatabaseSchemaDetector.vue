<template>
  <v-card variant="outlined" class="mb-4">
    <v-card-text>
      <div class="text-center">
        <v-icon size="48" color="primary" class="mb-2">mdi-database-search</v-icon>
        <h4 class="text-h6 mb-2">{{ $t('schema.detectDatabaseSchema') }}</h4>
        <p class="text-caption text-grey mb-4">
          {{ $t('schema.detectDatabaseDescription') }}
        </p>

        <v-text-field
          v-model="tableName"
          :label="$t('schema.tableName')"
          :placeholder="$t('schema.tableNamePlaceholder')"
          variant="outlined"
          density="compact"
          prepend-inner-icon="mdi-table"
          class="mb-3"
          :disabled="detecting"
          @keyup.enter="detectSchema"
        />

        <v-btn
          color="primary"
          prepend-icon="mdi-magnify"
          :loading="detecting"
          :disabled="!tableName.trim()"
          @click="detectSchema"
        >
          {{ $t('schema.detectSchema') }}
        </v-btn>

        <div class="mt-2 text-caption text-grey">
          {{ $t('schema.databaseDetectionNote') }}
        </div>
      </div>

      <v-alert v-if="detecting" type="info" class="mt-4" density="compact">
        <v-progress-linear indeterminate class="mb-2" />
        {{ $t('schema.detectingSchema', { tableName }) }}
      </v-alert>

      <v-card v-if="detectionResult" variant="outlined" class="mt-4">
        <v-card-title class="d-flex align-center">
          <v-icon start color="success">mdi-check-circle</v-icon>
          <span>{{ $t('schema.foundFields', { count: detectionResult.fields.length, filename: tableName }) }}</span>
          <v-spacer />
          <v-btn 
            size="small" 
            variant="text"
            @click="showPreview = !showPreview"
          >
            {{ showPreview ? $t('schema.hidePreview') : $t('schema.showPreview') }}
            <v-icon end>{{ showPreview ? 'mdi-chevron-up' : 'mdi-chevron-down' }}</v-icon>
          </v-btn>
        </v-card-title>

        <!-- Schema Preview -->
        <v-expand-transition>
          <div v-if="showPreview">
            <v-divider />
            <v-card-text>
              <div class="text-subtitle-2 mb-3">{{ $t('schema.detectedFields') }}</div>
              <div class="schema-preview">
                <v-chip
                  v-for="field in detectionResult.fields"
                  :key="field.id"
                  size="small"
                  class="mr-2 mb-2"
                  :color="field.isPrimaryKey ? 'success' : 'primary'"
                  variant="outlined"
                >
                  <v-icon v-if="field.isPrimaryKey" start size="x-small">mdi-key</v-icon>
                  <strong>{{ field.name }}</strong>
                  <span class="text-grey ml-1">({{ getTypeLabel(field.type) }})</span>
                  <v-icon v-if="field.nullable" size="x-small" class="ml-1" color="grey" :title="$t('connectors.nullable')">mdi-help-circle-outline</v-icon>
                </v-chip>
              </div>
              <v-divider class="my-3" />
              <div class="text-caption text-grey">
                <v-icon size="small" class="mr-1">mdi-information</v-icon>
                {{ $t('schema.editAfterApply') }}
              </div>
            </v-card-text>
          </div>
        </v-expand-transition>

        <v-divider />
        <v-card-actions>
          <v-spacer />
          <v-btn
            color="success"
            variant="elevated"
            prepend-icon="mdi-check"
            @click="applySchema"
          >
            {{ $t('schema.applySchema') }}
          </v-btn>
        </v-card-actions>
      </v-card>

      <v-alert v-if="error" type="error" class="mt-4" density="compact" closable @click:close="error = null">
        {{ error }}
      </v-alert>
    </v-card-text>
  </v-card>
</template>

<script setup>
import { ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { useConnector } from '@/composables/useConnector';

const { t } = useI18n();

const props = defineProps({
  connectorId: {
    type: String,
    default: null
  },
  connectorType: {
    type: String,
    default: null
  },
  provider: {
    type: String,
    default: null
  },
  config: {
    type: Object,
    default: null
  }
});

const emit = defineEmits(['schema-generated']);

const { detectSchema: detectSchemaAPI, detectSchemaPreview, loading: detecting, error: apiError } = useConnector();

const tableName = ref('');
const detectionResult = ref(null);
const error = ref(null);
const showPreview = ref(false);

// Data types mapping for display
const DATA_TYPES = [
  { value: 'varchar', label: 'String' },
  { value: 'int', label: 'Integer' },
  { value: 'bigint', label: 'Big Integer' },
  { value: 'decimal', label: 'Decimal' },
  { value: 'boolean', label: 'Boolean' },
  { value: 'date', label: 'Date' },
  { value: 'datetime', label: 'Date Time' },
  { value: 'timestamp', label: 'Timestamp' },
  { value: 'json', label: 'JSON' },
  { value: 'text', label: 'Text (Long)' }
];

function getTypeLabel(type) {
  const dataType = DATA_TYPES.find(dt => dt.value === type);
  return dataType ? dataType.label : type;
}

async function detectSchema() {
  if (!tableName.value.trim()) {
    error.value = t('schema.tableNameRequired');
    return;
  }

  error.value = null;
  detectionResult.value = null;

  try {
    let schema;
    
    // Use preview endpoint for new connectors (no ID), or regular endpoint for existing connectors
    if (props.connectorId) {
      schema = await detectSchemaAPI(props.connectorId, tableName.value.trim());
    } else if (props.connectorType && props.provider && props.config) {
      schema = await detectSchemaPreview(
        props.connectorType,
        props.provider,
        props.config,
        tableName.value.trim()
      );
    } else {
      error.value = t('schema.detectionFailed');
      return;
    }
    
    // The backend returns schema in the Schema property (nested)
    const schemaData = schema.schema || schema.Schema || schema;
    const fieldsArray = schemaData.fields || schemaData.Fields || [];
    
    if (fieldsArray && fieldsArray.length > 0) {
      // Transform schema fields to match the expected format
      const fields = fieldsArray.map((field, index) => {
        const isNullable = field.isNullable ?? field.IsNullable ?? true;
        const isPrimaryKey = field.isPrimaryKey || field.IsPrimaryKey || false;
        
        return {
          id: `field-${Date.now()}-${index}`,
          name: field.name || field.Name,
          type: field.dataType || field.DataType || field.type || 'varchar',
          isPrimaryKey: isPrimaryKey,
          // Required: true if it's a primary key OR explicitly marked as required AND not nullable
          required: isPrimaryKey || (field.required && !isNullable) || false,
          // Nullable: opposite of required for primary keys, otherwise use database value
          nullable: isPrimaryKey ? false : isNullable,
          description: field.description || field.Description || '',
          order: index + 1
        };
      });

      detectionResult.value = { fields };
      showPreview.value = true;
    } else {
      error.value = t('schema.noFieldsDetected');
    }
  } catch (err) {
    error.value = apiError.value || err.message || t('schema.detectionFailed');
  }
}

function applySchema() {
  if (detectionResult.value) {
    emit('schema-generated', detectionResult.value.fields);
    detectionResult.value = null;
    tableName.value = '';
    showPreview.value = false;
  }
}
</script>
