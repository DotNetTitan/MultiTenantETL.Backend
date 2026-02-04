<template>
  <div class="register-container">
    <v-row class="fill-height" align="center" justify="center">
      <v-col cols="12" sm="8" md="6" lg="5" xl="4">
        <v-card class="register-card" elevation="24">
          <!-- Header Section -->
          <div class="register-header">
            <v-avatar size="64" class="mb-4" color="primary">
              <v-icon size="40" color="white">mdi-account-plus</v-icon>
            </v-avatar>
            <h1 class="text-h4 font-weight-bold mb-2 text-primary">Create Account</h1>
            <p class="text-subtitle-1 text-medium-emphasis">Join our ETL platform</p>
          </div>

          <v-divider class="my-6" />

          <!-- Form Section -->
          <v-card-text class="px-8 pb-8">
            <v-form :disabled="authStore.loading" @submit.prevent="handleRegister">
              <v-row>
                <v-col cols="12" sm="6">
                  <FormInput
                    :model-value="form.firstName"
                    label="First Name"
                    prepend-inner-icon="mdi-account"
                    variant="outlined"
                    :error="errors.firstName"
                    @update:model-value="form.firstName = $event; validateField('firstName', $event, [required])"
                  />
                </v-col>
                <v-col cols="12" sm="6">
                  <FormInput
                    :model-value="form.lastName"
                    label="Last Name"
                    prepend-inner-icon="mdi-account"
                    variant="outlined"
                    :error="errors.lastName"
                    @update:model-value="form.lastName = $event; validateField('lastName', $event, [required])"
                  />
                </v-col>
              </v-row>

              <FormInput
                :model-value="form.email"
                label="Email"
                type="email"
                prepend-inner-icon="mdi-email"
                variant="outlined"
                :error="errors.email"
                class="mb-4"
                @update:model-value="form.email = $event; validateField('email', $event, [required])"
              />

              <FormInput
                :model-value="form.password"
                label="Password"
                type="password"
                prepend-inner-icon="mdi-lock"
                variant="outlined"
                :error="errors.password"
                class="mb-2"
                @update:model-value="form.password = $event; validateField('password', $event, [required, minLength(8)])"
              />
              
              <FormInput
                :model-value="form.confirmPassword"
                label="Confirm Password"
                type="password"
                prepend-inner-icon="mdi-lock-check"
                variant="outlined"
                :error="errors.confirmPassword"
                class="mb-4"
                @update:model-value="form.confirmPassword = $event; validateField('confirmPassword', $event, [required, matchesPassword])"
              />

              <p class="text-caption text-medium-emphasis mb-4">
                Must contain uppercase, lowercase, digit, and special character
              </p>

              <!-- Error alert -->
              <v-alert
                v-if="authStore.error || Object.keys(errors).length > 0"
                type="error"
                variant="tonal"
                class="mb-4"
                density="compact"
              >
                {{ authStore.error || 'Please fix the validation errors above.' }}
              </v-alert>

              <!-- Success alert -->
              <v-alert
                v-if="success"
                type="success"
                variant="tonal"
                class="mb-4"
                density="compact"
              >
                {{ success }}
              </v-alert>

              <v-btn
                color="primary"
                size="large"
                block
                :loading="authStore.loading"
                :disabled="authStore.loading || success || !form.firstName || !form.lastName || !form.email || !form.password || !form.confirmPassword || Object.keys(errors).length > 0"
                class="text-none font-weight-bold"
                elevation="2"
                @click="handleRegister"
              >
                {{ authStore.loading ? 'Creating account...' : 'Register' }}
              </v-btn>

              <div class="text-center mt-4">
                <span class="text-medium-emphasis">Already have an account?</span>
                <router-link to="/login" class="text-primary text-decoration-none ml-1">
                  Sign in
                </router-link>
              </div>
            </v-form>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>
  </div>
</template>

<script setup>
import { reactive, ref, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { useAuthStore } from '@/stores/auth';
import { useFormValidation, required, minLength } from '@/composables/useFormValidation';
import FormInput from '@/components/form/FormInput.vue';

const router = useRouter();
const authStore = useAuthStore();
const { errors, validateField, validateForm } = useFormValidation();
const success = ref('');

const form = reactive({
  email: '',
  password: '',
  confirmPassword: '',
  firstName: '',
  lastName: ''
});

// Custom validation rule for password match
const matchesPassword = (value) => {
  return value === form.password ? null : 'Passwords do not match';
};

async function handleRegister() {
  const isValid = validateForm({
    firstName: { value: form.firstName, rules: [required] },
    lastName: { value: form.lastName, rules: [required] },
    email: { value: form.email, rules: [required] },
    password: { value: form.password, rules: [required, minLength(8)] },
    confirmPassword: { value: form.confirmPassword, rules: [required, matchesPassword] }
  });

  if (!isValid) {
    return;
  }

  try {
    await authStore.register({
      email: form.email,
      password: form.password,
      firstName: form.firstName,
      lastName: form.lastName
    });
    success.value = 'Account created successfully! Please check your email to confirm your account.';
    
    // Redirect to login after 3 seconds
    setTimeout(() => {
      router.push('/login');
    }, 3000);
  } catch (error) {
    // Error is already handled in the store
    console.warn('Registration failed:', error.message);
  }
}
</script>

<style scoped>
.register-container {
  min-height: 100%;
  width: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 24px 16px;
  background: linear-gradient(135deg, 
    rgba(var(--v-theme-primary), 0.03) 0%, 
    rgba(var(--v-theme-primary), 0.08) 100%);
}

.register-card {
  border-radius: 16px !important;
  overflow: hidden;
  backdrop-filter: blur(10px);
  background: rgba(var(--v-theme-surface), 0.95) !important;
  border: 1px solid rgba(var(--v-theme-on-surface), 0.1);
}

.register-header {
  text-align: center;
  padding: 48px 32px 0;
}
</style>
