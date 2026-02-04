import { ref, computed } from 'vue';

export function useFormValidation() {
  const errors = ref({});
  const isSubmitting = ref(false);

  const hasErrors = computed(() => Object.keys(errors.value).length > 0);

  const validateField = (fieldName, value, rules = []) => {
    const fieldErrors = [];

    for (const rule of rules) {
      const error = rule(value);
      if (error) {
        fieldErrors.push(error);
      }
    }

    if (fieldErrors.length) {
      errors.value[fieldName] = fieldErrors;
      return false;
    } else {
      delete errors.value[fieldName];
      return true;
    }
  };

  const validateForm = (fields) => {
    errors.value = {};
    let isValid = true;

    Object.entries(fields).forEach(([fieldName, config]) => {
      const isFieldValid = validateField(fieldName, config.value, config.rules);
      if (!isFieldValid) {
        isValid = false;
      }
    });

    return isValid;
  };

  const clearErrors = (fieldName) => {
    if (fieldName) {
      delete errors.value[fieldName];
    } else {
      errors.value = {};
    }
  };

  const handleSubmit = async (submitFn) => {
    try {
      isSubmitting.value = true;
      clearErrors();
      await submitFn();
      return true;
    } catch (error) {
      // Handle different types of errors
      if (error.response?.data?.errors) {
        // API validation errors
        errors.value = error.response.data.errors;
      } else if (error.response?.data?.message) {
        // Single API error message
        errors.value._form = [error.response.data.message];
      } else {
        // Generic error
        errors.value._form = [error.message || 'An unexpected error occurred'];
      }
      return false;
    } finally {
      isSubmitting.value = false;
    }
  };

  return {
    errors,
    hasErrors,
    isSubmitting,
    validateField,
    validateForm,
    clearErrors,
    handleSubmit
  };
}

// Common validation rules
export const required = (value) => {
  if (value === null || value === undefined || value === '') {
    return 'This field is required';
  }
  if (Array.isArray(value) && value.length === 0) {
    return 'This field is required';
  }
  return null;
};

export const email = (value) => {
  if (!value) return null;
  const regex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  return regex.test(value) ? null : 'Please enter a valid email address';
};

export const minLength = (min) => (value) => {
  if (!value) return null;
  return String(value).length >= min ? null : `Must be at least ${min} characters`;
};

export const maxLength = (max) => (value) => {
  if (!value) return null;
  return String(value).length <= max ? null : `Must not exceed ${max} characters`;
};

export const numeric = (value) => {
  if (!value) return null;
  return !isNaN(Number(value)) ? null : 'Must be a number';
};

export const pattern = (regex, message) => (value) => {
  if (!value) return null;
  return regex.test(value) ? null : (message || 'Invalid format');
};