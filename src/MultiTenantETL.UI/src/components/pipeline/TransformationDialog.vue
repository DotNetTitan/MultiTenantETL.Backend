<template>
  <div class="fixed inset-0 z-50 overflow-y-auto" aria-labelledby="modal-title" role="dialog" aria-modal="true">
    <div class="flex items-end justify-center min-h-screen pt-4 px-4 pb-20 text-center sm:block sm:p-0">
      <div class="fixed inset-0 bg-gray-500 bg-opacity-75 transition-opacity" aria-hidden="true" @click="$emit('close')"></div>

      <span class="hidden sm:inline-block sm:align-middle sm:h-screen" aria-hidden="true">&#8203;</span>

      <div class="relative inline-block align-bottom bg-white rounded-lg px-4 pt-5 pb-4 text-left overflow-hidden shadow-xl transform transition-all sm:my-8 sm:align-middle sm:max-w-2xl sm:w-full sm:p-6">
        <div class="absolute top-0 right-0 pt-4 pr-4">
          <button
            type="button"
            class="bg-white rounded-md text-gray-400 hover:text-gray-500"
            @click="$emit('close')"
          >
            <span class="sr-only">Close</span>
            <span class="h-6 w-6 text-2xl" aria-hidden="true">&times;</span>
          </button>
        </div>

        <div class="sm:flex sm:items-start">
          <div class="mt-3 text-center sm:mt-0 sm:text-left w-full">
            <h3 id="modal-title" class="text-lg leading-6 font-medium text-gray-900">
              {{ isEditing ? 'Edit' : 'Add' }} Transformation
            </h3>
            
            <div class="mt-4">
              <TransformationForm
                v-model="form"
                :input-schema="inputSchema"
                :errors="errors"
                @update:valid="isValid = $event"
              />
            </div>
          </div>
        </div>

        <div class="mt-5 sm:mt-4 sm:flex sm:flex-row-reverse">
          <button
            type="button"
            class="btn btn-primary ml-3"
            :disabled="!isValid || saving"
            @click="handleSave"
          >
            {{ saving ? 'Saving...' : 'Save' }}
          </button>
          <button
            type="button"
            class="btn btn-outline"
            @click="$emit('close')"
          >
            Cancel
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed } from 'vue';
import { useTransformation } from '@/composables/useTransformation';
import TransformationForm from '@/components/transformation/TransformationForm.vue';
import { useFormValidation } from '@/composables/useFormValidation';

const props = defineProps({
  transformation: {
    type: Object,
    default: null
  },
  inputSchema: {
    type: Object,
    required: true
  }
});

const emit = defineEmits(['save', 'close']);

const { validateTransformation: validateTransformationSchema } = useTransformation();

const isEditing = computed(() => !!props.transformation);

const form = ref(props.transformation ? { ...props.transformation } : {
  name: '',
  type: 'Filter',
  errorHandling: 'Stop',
  config: {}
});

const isValid = ref(false);
const saving = ref(false);

const { errors, validateField, validateForm, clearErrors } = useFormValidation();
const transformationErrors = ref([]);

const validateTransformation = async () => {
  const validationFields = {
    name: { value: form.value.name, rules: [v => !!v || 'Name is required'] },
    type: { value: form.value.type, rules: [v => !!v || 'Type is required'] }
  };

  // Add config validation based on type
  switch (form.value.type) {
    case 'Filter':
      validationFields['filterColumn'] = {
        value: form.value.config.filterColumn,
        rules: [
          v => !!v || 'Filter column is required',
          v => props.inputSchema.columns.includes(v) || 'Column does not exist in input schema'
        ]
      };
      validationFields['operator'] = {
        value: form.value.config.operator,
        rules: [v => !!v || 'Operator is required']
      };
      if (!['isEmpty', 'isNotEmpty'].includes(form.value.config.operator)) {
        validationFields['value'] = {
          value: form.value.config.value,
          rules: [v => !!v || 'Value is required for this operator']
        };
      }
      break;

    case 'Map':
      validationFields['sourceColumn'] = {
        value: form.value.config.sourceColumn,
        rules: [
          v => !!v || 'Source column is required',
          v => props.inputSchema.columns.includes(v) || 'Column does not exist in input schema'
        ]
      };
      validationFields['targetColumn'] = {
        value: form.value.config.targetColumn,
        rules: [v => !!v || 'Target column is required']
      };
      break;

    case 'Join':
      validationFields['joinTable'] = {
        value: form.value.config.joinTable,
        rules: [v => !!v || 'Join table is required']
      };
      validationFields['joinType'] = {
        value: form.value.config.joinType,
        rules: [v => !!v || 'Join type is required']
      };
      validationFields['joinConditions'] = {
        value: form.value.config.joinConditions,
        rules: [
          v => Array.isArray(v) && v.length > 0 || 'At least one join condition is required',
          v => v.every(c => c.leftColumn && c.rightColumn) || 'All join conditions must have left and right columns'
        ]
      };
      break;

    case 'Aggregate':
      validationFields['groupByColumns'] = {
        value: form.value.config.groupByColumns,
        rules: [
          v => Array.isArray(v) && v.length > 0 || 'At least one group by column is required',
          v => v.every(col => props.inputSchema.columns.includes(col)) || 'All columns must exist in input schema'
        ]
      };
      validationFields['aggregations'] = {
        value: form.value.config.aggregations,
        rules: [
          v => Array.isArray(v) && v.length > 0 || 'At least one aggregation is required',
          v => v.every(a => a.type && a.column && a.alias) || 'All aggregations must have type, column and alias'
        ]
      };
      break;

    case 'Script':
      validationFields['script'] = {
        value: form.value.config.script,
        rules: [
          v => !!v || 'Script is required',
          v => {
            try {
              // Validate script syntax
              new Function('row', v);
              return true;
            } catch (e) {
              return `Invalid script syntax: ${e.message}`;
            }
          }
        ]
      };
      break;
  }

  // Validate schema impact
  try {
    const outputSchema = await validateSchemaImpact(form.value, props.inputSchema);
    if (!outputSchema.isValid) {
      transformationErrors.value = outputSchema.errors;
      return false;
    }
    transformationErrors.value = [];
  } catch (err) {
    console.error('Schema validation error:', err);
    transformationErrors.value = ['Failed to validate schema impact'];
    return false;
  }

  return await validateForm(validationFields);
};

const validateSchemaImpact = async (transformation, schema) => {
  const errors = [];
  
  try {
    switch (transformation.type) {
      case 'Filter':
        // Filter doesn't change schema structure, just validate column exists
        if (!schema.columns.includes(transformation.config.filterColumn)) {
          errors.push(`Filter column '${transformation.config.filterColumn}' not found in schema`);
        }
        break;

      case 'Map':
        // Ensure source column exists and target column name is valid
        if (!schema.columns.includes(transformation.config.sourceColumn)) {
          errors.push(`Source column '${transformation.config.sourceColumn}' not found in schema`);
        }
        if (!/^[a-zA-Z_][a-zA-Z0-9_]*$/.test(transformation.config.targetColumn)) {
          errors.push('Target column name must be a valid identifier');
        }
        break;

      case 'Join':
        // Validate join columns exist in both schemas
        for (const condition of transformation.config.joinConditions) {
          if (!schema.columns.includes(condition.leftColumn)) {
            errors.push(`Left join column '${condition.leftColumn}' not found in schema`);
          }
          // Right table columns would be validated against the join table's schema in a real implementation
        }
        break;

      case 'Aggregate':
        // Validate all group by columns exist
        for (const column of transformation.config.groupByColumns) {
          if (!schema.columns.includes(column)) {
            errors.push(`Group by column '${column}' not found in schema`);
          }
        }
        // Validate aggregation columns exist
        for (const agg of transformation.config.aggregations) {
          if (agg.type !== 'count' && !schema.columns.includes(agg.column)) {
            errors.push(`Aggregation column '${agg.column}' not found in schema`);
          }
          if (!/^[a-zA-Z_][a-zA-Z0-9_]*$/.test(agg.alias)) {
            errors.push(`Invalid alias name '${agg.alias}'`);
          }
        }
        break;

      case 'Script':
        // Script validation would require analyzing the script's output schema
        // This would be implemented based on your specific script execution engine
        break;
    }

    return {
      isValid: errors.length === 0,
      errors
    };
  } catch (err) {
    console.error('Schema validation error:', err);
    return {
      isValid: false,
      errors: ['Failed to validate schema impact']
    };
  }
};

const handleSave = async () => {
  saving.value = true;
  errors.value = {};

  try {
    const validationResult = await validateTransformationSchema(form.value, props.inputSchema);
    
    if (validationResult.isValid) {
      emit('save', {
        ...form.value,
        id: props.transformation?.id || Date.now().toString()
      });
    } else {
      errors.value = validationResult.errors.reduce((acc, err) => {
        acc[err.field] = err.message;
        return acc;
      }, {});
    }
  } catch (err) {
    errors.value.general = err.message;
  } finally {
    saving.value = false;
  }
};
</script>