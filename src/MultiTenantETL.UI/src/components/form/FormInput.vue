<template>
  <v-text-field
    :model-value="modelValue"
    :label="label"
    :type="showPassword ? 'text' : type"
    :error-messages="errorMessages"
    :prepend-icon="prependIcon"
    :append-inner-icon="passwordToggleIcon"
    variant="outlined"
    v-bind="$attrs"
    @update:model-value="handleInput"
    @blur="handleBlur"
    @click:append-inner="togglePasswordVisibility"
  />
</template>

<script setup>
import { computed, ref, onBeforeUnmount, watch } from 'vue';
import { useFormValidation } from '@/composables/useFormValidation';

const props = defineProps({
  modelValue: {
    type: [String, Number, Boolean, Array],
    default: null
  },
  type: {
    type: String,
    default: 'text'
  },
  label: {
    type: String,
    default: ''
  },
  prependIcon: {
    type: String,
    default: ''
  },
  rules: {
    type: Array,
    default: () => []
  },
  error: {
    type: [String, Array],
    default: null
  },
  hint: {
    type: String,
    default: ''
  },
  persistentHint: {
    type: Boolean,
    default: false
  },
  validateOnBlur: {
    type: Boolean,
    default: false
  },
  validateOnChange: {
    type: Boolean,
    default: true
  },
  hideDetails: {
    type: Boolean,
    default: false
  },
  dense: {
    type: Boolean,
    default: false
  },
  debounce: {
    type: Number,
    default: 300
  },
  required: {
    type: Boolean,
    default: false
  }
});

const emit = defineEmits(['update:model-value', 'validation']);
const localErrors = ref([]);
let validationTimeout = null;

const validateInput = async () => {
  localErrors.value = [];
  
  if (!props.rules?.length && !props.asyncValidation) return true;
  
  const rules = [...(props.rules || [])];

  for (const rule of rules) {
    try {
      const result = await rule(props.modelValue);
      if (result !== true && result !== null && result !== undefined) {
        localErrors.value.push(result);
      }
    } catch (err) {
      console.error('Validation error:', err);
      localErrors.value.push('Validation failed');
    }
  }

  const isValid = localErrors.value.length === 0;
  emit('validation', {
    valid: isValid,
    errors: localErrors.value
  });

  return isValid;
};

const handleInput = (event) => {
  const value = event?.target?.value ?? event;
  emit('update:model-value', value);

  if (props.validateOnChange) {
    if (validationTimeout) clearTimeout(validationTimeout);
    validationTimeout = setTimeout(() => validateInput(), props.debounce);
  }
};

const handleBlur = async () => {
  if (props.validateOnBlur) {
    await validateInput();
  }
};

// Handle v-model updates from parent
watch(() => props.modelValue, (newValue) => {
  if (props.validateOnChange && newValue !== undefined) {
    if (validationTimeout) clearTimeout(validationTimeout);
    validationTimeout = setTimeout(() => validateInput(), props.debounce);
  }
});

const errorMessages = computed(() => {
  if (props.error) {
    return Array.isArray(props.error) ? props.error : [props.error];
  }
  return localErrors.value;
});

const showPassword = ref(false);

const passwordToggleIcon = computed(() => {
  if (props.type !== 'password') return '';
  return showPassword.value ? 'mdi-eye-off' : 'mdi-eye';
});

const togglePasswordVisibility = () => {
  if (props.type === 'password') {
    showPassword.value = !showPassword.value;
  }
};

// Clear validation on unmount
onBeforeUnmount(() => {
  if (validationTimeout) {
    clearTimeout(validationTimeout);
  }
});
</script>