<template>
  <form class="space-y-4" @submit.prevent="handleSubmit">
    <FormInput
      v-model="form.name"
      label="Name"
      :error="errors.name"
      required
    />

    <div class="grid grid-cols-2 gap-4">
      <FormInput
        v-model="form.type"
        label="Type"
        type="select"
        :options="transformationTypes"
        :error="errors.type"
        required
        @change="handleTypeChange"
      />

      <FormInput
        v-model="form.errorHandling"
        label="Error Handling"
        type="select"
        :options="errorHandlingOptions"
        :error="errors.errorHandling"
        required
      />
    </div>

    <!-- Filter Transformation -->
    <div v-if="form.type === 'Filter'" class="space-y-4">
      <FormInput
        v-model="form.config.filterColumn"
        label="Filter Column"
        type="select"
        :options="availableColumns"
        :error="errors.filterColumn"
        required
      />

      <div class="grid grid-cols-2 gap-4">
        <FormInput
          v-model="form.config.operator"
          label="Operator"
          type="select"
          :options="filterOperators"
          :error="errors.operator"
          required
        />

        <FormInput
          v-if="!['isEmpty', 'isNotEmpty'].includes(form.config.operator)"
          v-model="form.config.value"
          label="Value"
          :error="errors.value"
          required
        />
      </div>
    </div>

    <!-- Map Transformation -->
    <div v-else-if="form.type === 'Map'" class="space-y-4">
      <FormInput
        v-model="form.config.sourceColumn"
        label="Source Column"
        type="select"
        :options="availableColumns"
        :error="errors.sourceColumn"
        required
      />

      <FormInput
        v-model="form.config.targetColumn"
        label="Target Column"
        :error="errors.targetColumn"
        required
      />

      <div class="border rounded p-4 space-y-4">
        <h4 class="font-medium">Value Mappings</h4>
        <div v-for="(mapping, index) in form.config.mappings" :key="index" class="grid grid-cols-3 gap-4">
          <FormInput
            v-model="mapping.from"
            label="From"
            :error="errors[`mapping${index}From`]"
          />
          <FormInput
            v-model="mapping.to"
            label="To"
            :error="errors[`mapping${index}To`]"
          />
          <button
            type="button"
            class="btn btn-danger mt-6"
            @click="removeMappingRow(index)"
          >
            Remove
          </button>
        </div>
        <button
          type="button"
          class="btn btn-secondary"
          @click="addMappingRow"
        >
          Add Mapping
        </button>
      </div>
    </div>

    <!-- Aggregation Transformation -->
    <div v-else-if="form.type === 'Aggregation'" class="space-y-4">
      <FormInput
        v-model="form.config.groupByColumns"
        label="Group By Columns"
        type="multiselect"
        :options="availableColumns"
        :error="errors.groupByColumns"
        required
      />

      <div class="grid grid-cols-2 gap-4">
        <FormInput
          v-model="form.config.aggregationType"
          label="Aggregation Type"
          type="select"
          :options="aggregationTypes"
          :error="errors.aggregationType"
          required
        />

        <FormInput
          v-if="form.config.aggregationType !== 'count'"
          v-model="form.config.aggregationColumn"
          label="Aggregation Column"
          type="select"
          :options="availableColumns"
          :error="errors.aggregationColumn"
          required
        />
      </div>

      <FormInput
        v-model="form.config.resultColumn"
        label="Result Column"
        :error="errors.resultColumn"
        required
      />
    </div>

    <!-- Script Transformation -->
    <div v-else-if="form.type === 'Script'" class="space-y-4">
      <FormInput
        v-model="form.config.script"
        label="JavaScript Transformation"
        type="textarea"
        :error="errors.script"
        required
        class="font-mono"
        placeholder="// Example:
row => ({
  ...row,
  fullName: `${row.firstName} ${row.lastName}`
})"
      />

      <div class="bg-gray-100 p-4 rounded">
        <h4 class="font-medium mb-2">Available Variables:</h4>
        <ul class="list-disc list-inside text-sm">
          <li>row: Current data row</li>
          <li>context: Transformation context</li>
          <li>utils: Helper functions</li>
        </ul>
      </div>
    </div>

    <!-- Trim Transformation -->
    <div v-else-if="form.type === 'Trim'" class="space-y-4">
      <FormInput
        v-model="form.config.column"
        label="Column"
        type="select"
        :options="availableColumns"
        :error="errors.column"
        required
      />

      <FormInput
        v-model="form.config.trimType"
        label="Trim Type"
        type="select"
        :options="['both', 'start', 'end']"
        :error="errors.trimType"
        required
      />

      <div class="bg-gray-100 p-4 rounded text-sm">
        <strong>both:</strong> Remove whitespace from both ends<br>
        <strong>start:</strong> Remove whitespace from the beginning<br>
        <strong>end:</strong> Remove whitespace from the end
      </div>
    </div>

    <!-- Case Convert Transformation -->
    <div v-else-if="form.type === 'Case Convert'" class="space-y-4">
      <FormInput
        v-model="form.config.column"
        label="Column"
        type="select"
        :options="availableColumns"
        :error="errors.column"
        required
      />

      <FormInput
        v-model="form.config.caseType"
        label="Case Type"
        type="select"
        :options="['uppercase', 'lowercase', 'titlecase', 'camelcase']"
        :error="errors.caseType"
        required
      />

      <div class="bg-gray-100 p-4 rounded text-sm">
        <strong>uppercase:</strong> HELLO WORLD<br>
        <strong>lowercase:</strong> hello world<br>
        <strong>titlecase:</strong> Hello World<br>
        <strong>camelcase:</strong> helloWorld
      </div>
    </div>

    <!-- Substring Transformation -->
    <div v-else-if="form.type === 'Substring'" class="space-y-4">
      <FormInput
        v-model="form.config.column"
        label="Column"
        type="select"
        :options="availableColumns"
        :error="errors.column"
        required
      />

      <div class="grid grid-cols-2 gap-4">
        <FormInput
          v-model.number="form.config.start"
          label="Start Position"
          type="number"
          :error="errors.start"
          required
          placeholder="0"
        />

        <FormInput
          v-model.number="form.config.length"
          label="Length (optional)"
          type="number"
          :error="errors.length"
          placeholder="Leave empty for rest of string"
        />
      </div>

      <div class="bg-gray-100 p-4 rounded text-sm">
        Extract a portion of the string starting at the specified position.<br>
        Position is 0-based (first character is at position 0).
      </div>
    </div>

    <!-- Replace Transformation -->
    <div v-else-if="form.type === 'Replace'" class="space-y-4">
      <FormInput
        v-model="form.config.column"
        label="Column"
        type="select"
        :options="availableColumns"
        :error="errors.column"
        required
      />

      <FormInput
        v-model="form.config.searchValue"
        label="Search For"
        :error="errors.searchValue"
        required
        placeholder="Text or pattern to find"
      />

      <FormInput
        v-model="form.config.replaceValue"
        label="Replace With"
        :error="errors.replaceValue"
        placeholder="Replacement text (empty to remove)"
      />

      <div class="flex items-center space-x-4">
        <label class="flex items-center">
          <input
            v-model="form.config.useRegex"
            type="checkbox"
            class="mr-2"
          />
          Use Regular Expression
        </label>

        <label v-if="form.config.useRegex" class="flex items-center">
          <input
            v-model="form.config.caseSensitive"
            type="checkbox"
            class="mr-2"
          />
          Case Sensitive
        </label>

        <label class="flex items-center">
          <input
            v-model="form.config.replaceAll"
            type="checkbox"
            class="mr-2"
          />
          Replace All Occurrences
        </label>
      </div>
    </div>

    <!-- Split Transformation -->
    <div v-else-if="form.type === 'Split'" class="space-y-4">
      <FormInput
        v-model="form.config.column"
        label="Column"
        type="select"
        :options="availableColumns"
        :error="errors.column"
        required
      />

      <FormInput
        v-model="form.config.delimiter"
        label="Delimiter"
        :error="errors.delimiter"
        required
        placeholder="e.g., comma (,), pipe (|), space"
      />

      <FormInput
        v-model.number="form.config.maxSplits"
        label="Maximum Splits (optional)"
        type="number"
        :error="errors.maxSplits"
        placeholder="Leave empty for unlimited"
      />

      <div class="bg-gray-100 p-4 rounded text-sm">
        <strong>Output:</strong> Creates an array from the split string.<br>
        You can access individual parts using array indexing in subsequent transformations.
      </div>
    </div>

    <div class="flex justify-between mt-6">
      <button
        type="button"
        class="btn btn-secondary"
        :disabled="!isFormValid || validating"
        @click="handleValidateTransformation"
      >
        {{ validating ? 'Validating...' : 'Validate' }}
      </button>

      <div class="space-x-2">
        <button
          type="button"
          class="btn btn-outline"
          @click="$emit('cancel')"
        >
          Cancel
        </button>
        <button
          type="submit"
          class="btn btn-primary"
          :disabled="!isFormValid || saving"
        >
          {{ saving ? 'Saving...' : 'Save' }}
        </button>
      </div>
    </div>
  </form>
</template>

<script setup>
import { ref, computed, watch } from 'vue';
import { useTransformation } from '@/composables/useTransformation';
import FormInput from '@/components/form/FormInput.vue';
import { useFormValidation, required, maxLength } from '@/composables/useFormValidation';

const props = defineProps({
  initialData: {
    type: Object,
    default: () => ({})
  },
  inputSchema: {
    type: Object,
    required: true
  }
});

const emit = defineEmits(['save', 'cancel', 'notify']);

const { validateTransformation } = useTransformation();
const { errors, validateField, validateForm, clearErrors } = useFormValidation();

const transformationTypes = ['Filter', 'Map', 'Aggregation', 'Script', 'Trim', 'Case Convert', 'Substring', 'Replace', 'Split'];
const errorHandlingOptions = ['Stop', 'Skip', 'Retry'];
const filterOperators = ['equals', 'notEquals', 'contains', 'startsWith', 'endsWith', 'isEmpty', 'isNotEmpty'];
const aggregationTypes = ['sum', 'avg', 'min', 'max', 'count'];

const availableColumns = computed(() => 
  props.inputSchema?.columns?.map(col => col.name) || []
);

const form = ref({
  name: '',
  type: 'Filter',
  errorHandling: 'Stop',
  config: {
    // Filter config
    filterColumn: '',
    operator: '',
    value: '',

    // Map config
    sourceColumn: '',
    targetColumn: '',
    mappings: [],

    // Aggregation config
    groupByColumns: [],
    aggregationType: '',
    aggregationColumn: '',
    resultColumn: '',

    // Script config
    script: ''
  },
  ...props.initialData
});

const transformationRules = {
  name: [required, maxLength(100)],
  type: [required],
  errorHandling: [required],
  config: {
    Filter: {
      filterColumn: [required],
      operator: [required],
      value: (value, { operator }) => operator && !['isEmpty', 'isNotEmpty'].includes(operator) && !value ? 'Value is required for this operator' : null
    },
    Map: {
      sourceColumn: [required],
      targetColumn: [required],
      mappings: (value) => (!Array.isArray(value) || value.length === 0) ? 'At least one mapping is required' : null
    },
    Aggregation: {
      groupByColumns: (value) => (!Array.isArray(value) || value.length === 0) ? 'At least one group by column is required' : null,
      aggregationType: [required],
      resultColumn: [required],
      aggregationColumn: (value, { aggregationType }) => 
        aggregationType && aggregationType !== 'count' && !value ? 'Aggregation column is required for this type' : null
    },
    Script: {
      script: [(value) => {
        if (!value?.trim()) return 'Script is required';
        try {
          new Function('row', value);
          return null;
        } catch (e) {
          return `Script syntax error: ${e.message}`;
        }
      }]
    }
  }
};

const validating = ref(false);
const saving = ref(false);

const isFormValid = computed(() => {
  const baseFields = ['name', 'type', 'errorHandling'];
  
  if (!baseFields.every(field => form.value[field])) return false;

  switch (form.value.type) {
    case 'Filter':
      return form.value.config.filterColumn && form.value.config.operator &&
        (!['isEmpty', 'isNotEmpty'].includes(form.value.config.operator) ? form.value.config.value : true);
    
    case 'Map':
      return form.value.config.sourceColumn && form.value.config.targetColumn;
    
    case 'Aggregation':
      return form.value.config.groupByColumns?.length > 0 &&
        form.value.config.aggregationType &&
        (form.value.config.aggregationType === 'count' || form.value.config.aggregationColumn) &&
        form.value.config.resultColumn;
    
    case 'Script':
      return !!form.value.config.script;
    
    case 'Trim':
      return form.value.config.column && form.value.config.trimType;
    
    case 'Case Convert':
      return form.value.config.column && form.value.config.caseType;
    
    case 'Substring':
      return form.value.config.column && form.value.config.start !== null && form.value.config.start !== '';
    
    case 'Replace':
      return form.value.config.column && form.value.config.searchValue;
    
    case 'Split':
      return form.value.config.column && form.value.config.delimiter;
    
    default:
      return false;
  }
});

watch(() => form.value.type, (newType) => {
  form.value.config = getDefaultConfig(newType);
  errors.value = {};
});

const getDefaultConfig = (type) => {
  switch (type) {
    case 'Filter':
      return {
        filterColumn: '',
        operator: '',
        value: ''
      };
    case 'Map':
      return {
        sourceColumn: '',
        targetColumn: '',
        mappings: []
      };
    case 'Aggregation':
      return {
        groupByColumns: [],
        aggregationType: '',
        aggregationColumn: '',
        resultColumn: ''
      };
    case 'Script':
      return {
        script: ''
      };
    case 'Trim':
      return {
        column: '',
        trimType: 'both'
      };
    case 'Case Convert':
      return {
        column: '',
        caseType: 'lowercase'
      };
    case 'Substring':
      return {
        column: '',
        start: 0,
        length: null
      };
    case 'Replace':
      return {
        column: '',
        searchValue: '',
        replaceValue: '',
        useRegex: false,
        caseSensitive: false,
        replaceAll: true
      };
    case 'Split':
      return {
        column: '',
        delimiter: ',',
        maxSplits: null
      };
    default:
      return {};
  }
};

const handleTypeChange = () => {
  form.value.config = getDefaultConfig(form.value.type);
  errors.value = {};
};

const addMappingRow = () => {
  form.value.config.mappings.push({ from: '', to: '' });
};

const removeMappingRow = (index) => {
  form.value.config.mappings.splice(index, 1);
};

const handleValidateTransformation = async () => {
  if (!isFormValid.value) return;
  
  validating.value = true;
  errors.value = {};

  try {
    const result = await validateTransformation(form.value, props.inputSchema);
    if (result.isValid) {
      emit('notify', {
        type: 'success',
        message: 'Transformation validation successful!'
      });
    } else {
      errors.value = result.errors.reduce((acc, err) => {
        acc[err.field] = err.message;
        return acc;
      }, {});
      emit('notify', {
        type: 'error',
        message: 'Validation failed. Please check the errors.'
      });
    }
  } catch (err) {
    errors.value.validation = err.message;
  } finally {
    validating.value = false;
  }
};

const validateTransformationField = async (field, value) => {
  let rules = [];
  
  if (field === 'name' || field === 'type' || field === 'errorHandling') {
    rules = transformationRules[field];
  } else if (field.startsWith('config.')) {
    const [_, configField] = field.split('.');
    rules = transformationRules.config[form.value.type]?.[configField] || [];
  }

  await validateField(field, value, Array.isArray(rules) ? rules : [rules], {
    operator: form.value.config.operator,
    aggregationType: form.value.config.aggregationType
  });
};

const handleFieldUpdate = async (field, value) => {
  if (field === 'type') {
    form.value.config = getDefaultConfig(value);
    clearErrors();
  } else {
    if (field.includes('.')) {
      const [parent, child] = field.split('.');
      form.value[parent][child] = value;
    } else {
      form.value[field] = value;
    }
    await validateTransformationField(field, value);
  }
};

const handleSubmit = async () => {
  const configRules = transformationRules.config[form.value.type];
  const validationFields = {
    name: { value: form.value.name, rules: transformationRules.name },
    type: { value: form.value.type, rules: transformationRules.type },
    errorHandling: { value: form.value.errorHandling, rules: transformationRules.errorHandling }
  };

  // Add config field validations
  Object.entries(configRules).forEach(([field, rules]) => {
    validationFields[`config.${field}`] = {
      value: form.value.config[field],
      rules,
      options: {
        operator: form.value.config.operator,
        aggregationType: form.value.config.aggregationType
      }
    };
  });

  const isValid = await validateForm(validationFields);

  if (isValid) {
    emit('save', { ...form.value });
  }
};
</script>