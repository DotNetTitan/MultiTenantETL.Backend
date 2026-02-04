<template>
  <div class="schema-editor">
    <div class="d-flex align-center mb-4">
      <div class="flex-grow-1">
        <p class="text-body-2 text-medium-emphasis">{{ $t('schema.defineStructure') }}</p>
      </div>
      <v-btn
        variant="text"
        prepend-icon="mdi-download"
        size="small"
        @click="exportSchema"
      >
        {{ $t('schema.export') }}
      </v-btn>
      <v-btn
        variant="text"
        prepend-icon="mdi-upload"
        size="small"
        @click="showImportDialog = true"
      >
        {{ $t('schema.import') }}
      </v-btn>
    </div>

    <!-- Database Schema Detector (for Database connectors) -->
    <DatabaseSchemaDetector
      v-if="localFields.length === 0 && connectorType === 'Database'"
      :connector-id="connectorId"
      :connector-type="connectorType"
      :provider="provider"
      :config="config"
      @schema-generated="handleSchemaGenerated"
    />

    <!-- API Schema Detector (for API connectors) -->
    <ApiSchemaDetector
      v-if="localFields.length === 0 && connectorType === 'API'"
      :connector-type="connectorType"
      :provider="provider"
      :config="config"
      @schema-generated="handleSchemaGenerated"
    />

    <!-- File Upload Schema Generator (for File connectors or when no connector type) -->
    <FileUploadSchemaGenerator
      v-if="localFields.length === 0 && (connectorType === 'File' || !connectorType)"
      @schema-generated="handleSchemaGenerated"
    />

    <v-divider v-if="localFields.length === 0" class="my-4">
      <span class="text-caption text-grey px-2">OR</span>
    </v-divider>

    <!-- Field List -->
    <v-card variant="outlined" class="mb-4">
      <v-table v-if="localFields.length > 0">
        <thead>
          <tr>
            <th>{{ $t('schema.fieldName') }}</th>
            <th>{{ $t('common.type') }}</th>
            <th>{{ $t('schema.uniqueId') }}</th>
            <th>{{ $t('common.required') }}</th>
            <th>{{ $t('connectors.nullable') }}</th>
            <th class="text-right">{{ $t('common.actions') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="(field, index) in localFields" :key="field.id">
            <td>
              <div class="d-flex align-center">
                <v-icon v-if="field.isPrimaryKey" color="primary" size="small" class="mr-2">mdi-key</v-icon>
                <div>
                  <strong>{{ field.name }}</strong>
                  <div v-if="field.description" class="text-caption text-grey">
                    {{ field.description }}
                  </div>
                </div>
              </div>
            </td>
            <td>
              <v-chip size="small" variant="tonal">
                {{ getTypeLabel(field.type) }}
              </v-chip>
            </td>
            <td>
              <v-icon v-if="field.isPrimaryKey" color="primary" size="small">
                mdi-check-circle
              </v-icon>
              <v-icon v-else color="grey" size="small">
                mdi-circle-outline
              </v-icon>
            </td>
            <td>
              <v-icon v-if="field.required" color="error" size="small">
                mdi-check-circle
              </v-icon>
              <v-icon v-else color="grey" size="small">
                mdi-circle-outline
              </v-icon>
            </td>
            <td>
              <v-icon v-if="field.nullable" color="success" size="small">
                mdi-check-circle
              </v-icon>
              <v-icon v-else color="grey" size="small">
                mdi-circle-outline
              </v-icon>
            </td>
            <td class="text-right">
              <v-btn
                icon
                variant="text"
                size="small"
                :disabled="index === 0"
                @click="moveFieldUp(index)"
              >
                <v-icon>mdi-arrow-up</v-icon>
              </v-btn>
              <v-btn
                icon
                variant="text"
                size="small"
                :disabled="index === localFields.length - 1"
                @click="moveFieldDown(index)"
              >
                <v-icon>mdi-arrow-down</v-icon>
              </v-btn>
              <v-btn
                icon
                variant="text"
                size="small"
                @click="editField(index)"
              >
                <v-icon>mdi-pencil</v-icon>
              </v-btn>
              <v-btn
                icon
                variant="text"
                size="small"
                color="error"
                @click="removeField(index)"
              >
                <v-icon>mdi-delete</v-icon>
              </v-btn>
            </td>
          </tr>
        </tbody>
      </v-table>

      <div v-else class="text-center py-8">
        <v-icon size="64" color="grey-lighten-2">mdi-table-off</v-icon>
        <p class="mt-2 text-grey">{{ $t('schema.noFieldsDefined') }}</p>
        <p class="text-caption text-grey">{{ $t('schema.addFieldsManually') }}</p>
      </div>
    </v-card>

    <!-- Add Field Button -->
    <v-btn
      block
      variant="tonal"
      prepend-icon="mdi-plus"
      @click="addField"
    >
      {{ $t('schema.addField') }}
    </v-btn>

    <!-- Summary -->
    <div v-if="localFields.length > 0" class="mt-4 text-caption text-grey">
      {{ $t('schema.summary', { count: localFields.length }) }}
      ({{ $t('schema.requiredCount', { count: requiredFieldsCount }) }})
    </div>

    <!-- Validation Errors (only show meaningful errors, not "no fields" on initial load) -->
    <div v-if="shouldShowValidationErrors" class="mt-4">
      <!-- Unique Identifier Validation -->
      <v-card v-if="uniqueIdentifierErrors.length > 0" variant="outlined" color="error" class="mb-3">
        <v-card-text class="py-3">
          <div class="d-flex align-start">
            <v-icon color="error" class="mr-3 mt-1">mdi-key-alert</v-icon>
            <div class="flex-grow-1">
              <div class="text-subtitle-2 mb-1">{{ $t('schema.uniqueIdentifierRequired') }}</div>
              <div v-for="(error, idx) in uniqueIdentifierErrors" :key="idx" class="text-caption mb-1">
                • {{ error }}
              </div>
            </div>
          </div>
        </v-card-text>
      </v-card>

      <!-- Other Validation Errors -->
      <v-card v-if="otherValidationErrors.length > 0" variant="outlined" color="error" class="mb-3">
        <v-card-text class="py-3">
          <div class="d-flex align-start">
            <v-icon color="error" class="mr-3 mt-1">mdi-alert</v-icon>
            <div class="flex-grow-1">
              <div class="text-subtitle-2 mb-2">{{ $t('schema.schemaIssues') }}</div>
              <div v-for="(error, idx) in otherValidationErrors" :key="idx" class="text-caption mb-1">
                • {{ error }}
              </div>
            </div>
          </div>
        </v-card-text>
      </v-card>
    </div>

    <!-- Field Editor Dialog -->
    <FieldEditorDialog
      v-model="showFieldDialog"
      :field="editingField"
      :existing-field-names="existingFieldNames"
      @save="saveField"
    />

    <!-- Import Dialog -->
    <v-dialog v-model="showImportDialog" max-width="500">
      <v-card>
        <v-card-title class="d-flex align-center">
          {{ $t('schema.importSchema') }}
          <v-spacer />
          <v-btn
            icon
            variant="text"
            @click="showImportDialog = false"
          >
            <v-icon>mdi-close</v-icon>
          </v-btn>
        </v-card-title>
        <v-card-text>
          <v-textarea
            v-model="importJson"
            :label="$t('schema.pasteJsonSchema')"
            rows="10"
            variant="outlined"
            placeholder="{&quot;fields&quot;: [{&quot;name&quot;: &quot;id&quot;, &quot;type&quot;: &quot;int&quot;, ...}]}"
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn @click="showImportDialog = false">{{ $t('common.close') }}</v-btn>
          <v-btn color="primary" @click="importSchema">{{ $t('schema.import') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup>
import { ref, computed, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { validateSchema } from '@/services/schemaService';
import DatabaseSchemaDetector from './DatabaseSchemaDetector.vue';
import ApiSchemaDetector from './ApiSchemaDetector.vue';
import FileUploadSchemaGenerator from './FileUploadSchemaGenerator.vue';
import FieldEditorDialog from './FieldEditorDialog.vue';

const { t } = useI18n();

const props = defineProps({
  modelValue: {
    type: Array,
    default: () => []
  },
  readonly: {
    type: Boolean,
    default: false
  },
  connectorType: {
    type: String,
    default: null
  },
  connectorId: {
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

const emit = defineEmits(['update:modelValue', 'validate']);

// State
const localFields = ref([...props.modelValue]);
const showFieldDialog = ref(false);
const showImportDialog = ref(false);
const editingField = ref(null);
const editingIndex = ref(-1);
const importJson = ref('');

// Data types mapping
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

// Computed
const requiredFieldsCount = computed(() => {
  return localFields.value.filter(f => f.required).length;
});

const existingFieldNames = computed(() => {
  return localFields.value
    .filter((_, idx) => idx !== editingIndex.value)
    .map(f => f.name.toLowerCase());
});

const validationErrors = computed(() => {
  const result = validateSchema(localFields.value);
  return result.errors;
});

// Separate unique identifier errors from other errors
const uniqueIdentifierErrors = computed(() => {
  return validationErrors.value.filter(error => 
    error.includes('unique identifier') || 
    error.includes('Unique identifier')
  );
});

const otherValidationErrors = computed(() => {
  return validationErrors.value.filter(error => 
    !error.includes('unique identifier') && 
    !error.includes('Unique identifier')
  );
});

// Only show validation errors if there are fields with actual errors
// Don't show "at least one field must be defined" on initial empty state
const shouldShowValidationErrors = computed(() => {
  if (validationErrors.value.length === 0) return false;
  
  // If there are no fields, don't show the "at least one field" error
  if (localFields.value.length === 0) return false;
  
  // If there are fields, show any validation errors
  return true;
});

// Watch for changes and emit
watch(localFields, () => {
  emit('update:modelValue', localFields.value);
  const validation = validateSchema(localFields.value);
  emit('validate', validation);
}, { deep: true });

// Methods
function getTypeLabel(type) {
  const dataType = DATA_TYPES.find(dt => dt.value === type);
  return dataType ? dataType.label : type;
}

function addField() {
  editingField.value = {
    id: `field-${Date.now()}-${Math.random()}`,
    name: '',
    type: 'varchar',
    isPrimaryKey: false,
    required: false,
    nullable: true,
    description: '',
    order: localFields.value.length + 1
  };
  editingIndex.value = -1;
  showFieldDialog.value = true;
}

function editField(index) {
  editingField.value = { ...localFields.value[index] };
  editingIndex.value = index;
  showFieldDialog.value = true;
}

function saveField(field) {
  // If this field is marked as unique identifier, unmark all other fields
  if (field.isPrimaryKey) {
    localFields.value.forEach((f, idx) => {
      if (idx !== editingIndex.value) {
        f.isPrimaryKey = false;
      }
    });
  }
  
  if (editingIndex.value >= 0) {
    // Update existing field
    localFields.value[editingIndex.value] = field;
  } else {
    // Add new field
    localFields.value.push(field);
  }
  showFieldDialog.value = false;
}

function removeField(index) {
  localFields.value.splice(index, 1);
  // Update order
  localFields.value.forEach((f, i) => {
    f.order = i + 1;
  });
}

function moveFieldUp(index) {
  if (index > 0) {
    const temp = localFields.value[index];
    localFields.value[index] = localFields.value[index - 1];
    localFields.value[index - 1] = temp;
    // Update order
    localFields.value.forEach((f, i) => {
      f.order = i + 1;
    });
  }
}

function moveFieldDown(index) {
  if (index < localFields.value.length - 1) {
    const temp = localFields.value[index];
    localFields.value[index] = localFields.value[index + 1];
    localFields.value[index + 1] = temp;
    // Update order
    localFields.value.forEach((f, i) => {
      f.order = i + 1;
    });
  }
}

function exportSchema() {
  const schema = {
    fields: localFields.value.map(f => ({
      name: f.name,
      type: f.type,
      required: f.required,
      nullable: f.nullable,
      description: f.description
    }))
  };
  
  const blob = new Blob([JSON.stringify(schema, null, 2)], { type: 'application/json' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = 'schema.json';
  a.click();
  URL.revokeObjectURL(url);
}

function importSchema() {
  try {
    const schema = JSON.parse(importJson.value);
    
    if (!schema.fields || !Array.isArray(schema.fields)) {
      throw new Error('Invalid schema format: missing fields array');
    }
    
    // Validate and import fields
    const importedFields = schema.fields.map((f, index) => {
      // Fix Required/Nullable conflict: if both are true, prioritize Required
      let required = f.required || false;
      let nullable = f.nullable !== false;
      
      if (required && nullable) {
        // If both are true, make it Required and not Nullable
        nullable = false;
      }
      
      return {
        id: `field-${Date.now()}-${index}`,
        name: f.name || '',
        type: f.type || 'varchar',
        required,
        nullable,
        description: f.description || '',
        order: index + 1
      };
    });
    
    localFields.value = importedFields;
    showImportDialog.value = false;
    importJson.value = '';
  } catch (error) {
    alert(`Import failed: ${error.message}`);
  }
}

function handleSchemaGenerated(schema) {
  // Handle both formats: array of fields or object with fields property
  let rawFields = [];
  
  if (Array.isArray(schema)) {
    rawFields = schema;
  } else if (schema && schema.fields && Array.isArray(schema.fields)) {
    rawFields = schema.fields;
  } else {
    console.error('❌ Invalid schema format received:', schema);
    localFields.value = [];
    return;
  }
  
  // Normalize fields to ensure they have required properties
  const normalizedFields = rawFields
    .filter(field => field && (
      field.name || field.Name || 
      field.fieldName || field.FieldName || 
      field.columnName || field.ColumnName
    )) // Filter out invalid fields
    .map((field, index) => {
      // Handle different field name properties (both camelCase and PascalCase)
      const name = field.name || field.Name || 
                   field.fieldName || field.FieldName || 
                   field.columnName || field.ColumnName || 
                   `field_${index + 1}`;
      
      // Handle different type properties
      const type = field.type || field.Type || 
                   field.dataType || field.DataType || 
                   'varchar';
      
      // Handle nullable properties
      const isNullable = field.nullable !== undefined ? field.nullable : 
                         field.Nullable !== undefined ? field.Nullable :
                         field.IsNullable !== undefined ? field.IsNullable :
                         true;
      
      // Handle primary key properties
      const isPrimaryKey = field.isPrimaryKey || field.IsPrimaryKey || 
                          field.isUnique || field.IsUnique || 
                          false;
      
      return {
        id: field.id || field.Id || `field-${Date.now()}-${index}`,
        name: name,
        type: type.toLowerCase(), // Normalize to lowercase
        isPrimaryKey: isPrimaryKey,
        required: field.required || field.Required || (!isNullable),
        nullable: isNullable,
        description: field.description || field.Description || '',
        order: field.order || field.Order || index + 1
      };
    });
  
  localFields.value = normalizedFields;
}
</script>

<style scoped>
.schema-editor {
  width: 100%;
}
</style>
