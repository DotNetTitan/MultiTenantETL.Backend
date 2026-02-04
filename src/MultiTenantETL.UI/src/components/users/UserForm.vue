<template>
  <v-form @submit.prevent="handleSubmit">
    <v-row>
      <v-col cols="12" md="6">
        <FormInput
          v-model="form.firstName"
          :label="$t('forms.firstName')"
          prepend-icon="mdi-account"
          :error-messages="errors.firstName"
          @update:model-value="updateField('firstName', $event)"
        />
      </v-col>
      <v-col cols="12" md="6">
        <FormInput
          v-model="form.lastName"
          :label="$t('forms.lastName')"
          prepend-icon="mdi-account"
          :error-messages="errors.lastName"
          @update:model-value="updateField('lastName', $event)"
        />
      </v-col>
      <v-col cols="12">
        <FormInput
          v-model="form.email"
          :label="$t('forms.email')"
          type="email"
          prepend-icon="mdi-email"
          :error-messages="errors.email"
          @update:model-value="updateField('email', $event)"
        />
      </v-col>
      <v-col cols="12" md="6">
        <v-select
          v-model="form.role"
          :items="roles"
          :label="$t('forms.globalRole')"
          prepend-icon="mdi-shield-account"
          variant="outlined"
          :error-messages="errors.role"
          hint="Global system role"
          persistent-hint
          @update:model-value="updateField('role', $event)"
        />
      </v-col>
      <v-col cols="12" md="6">
        <v-switch
          v-model="form.isActive"
          :label="$t('forms.active')"
          color="success"
          hide-details
          @update:model-value="updateField('isActive', $event)"
        />
      </v-col>
      <v-col v-if="form.tenants && form.tenants.length > 0" cols="12">
        <v-card variant="outlined">
          <v-card-title class="text-subtitle-2">
            {{ $t('forms.tenantMemberships') }}
          </v-card-title>
          <v-card-text>
            <v-chip
              v-for="tenant in form.tenants"
              :key="tenant.tenantId"
              class="ma-1"
              closable
              @click:close="removeTenant(tenant.tenantId)"
            >
              {{ tenant.tenantName }} ({{ tenant.roleCode }})
            </v-chip>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>
  </v-form>
</template>

<script setup>
import { ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import FormInput from '@/components/form/FormInput.vue';
import { useFormValidation } from '@/composables/useFormValidation';

const { t } = useI18n();

const props = defineProps({
  user: {
    type: Object,
    required: true
  },
  roles: {
    type: Array,
    required: true
  }
});

const emit = defineEmits(['update:user', 'submit', 'remove-tenant']);
const { errors, validateField, validateForm, clearErrors } = useFormValidation();

// Initialize form with proper role handling
const initializeForm = (user) => {
  const formData = { ...user };
  
  // Handle roles array - convert to single role for form
  if (Array.isArray(user.roles) && user.roles.length > 0) {
    formData.role = user.roles[0];
  } else if (!formData.role) {
    formData.role = 'User';
  }
  
  return formData;
};

const form = ref(initializeForm(props.user));

// Clear errors when form changes
watch(() => form.value, () => {
  clearErrors();
}, { deep: true });

// Watch for prop changes
watch(() => props.user, (newValue) => {
  form.value = initializeForm(newValue);
}, { deep: true });

const removeTenant = (tenantId) => {
  emit('remove-tenant', tenantId);
};

const required = v => !!v || 'This field is required';
const emailRule = v => {
  if (!v) return 'Email is required';
  const pattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  return pattern.test(v) || 'Please enter a valid email address';
};

const updateField = (field, value) => {
  form.value[field] = value;
  emit('update:user', { ...form.value });
  
  switch (field) {
    case 'firstName':
    case 'lastName':
      validateField(field, value, [required]);
      break;
    case 'email':
      validateField(field, value, [required, emailRule]);
      break;
    case 'role':
      validateField(field, value, [required]);
      break;
  }
};

const handleSubmit = async () => {
  const isValid = validateForm({
    firstName: { value: form.value.firstName, rules: [required] },
    lastName: { value: form.value.lastName, rules: [required] },
    email: { value: form.value.email, rules: [required, emailRule] },
    role: { value: form.value.role, rules: [required] }
  });

  if (isValid) {
    emit('submit', { ...form.value });
  }
};
</script>