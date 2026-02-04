<template>
  <div class="forgot-password-container">
    <v-row class="fill-height" align="center" justify="center">
      <v-col cols="12" sm="8" md="5" lg="4" xl="3">
        <v-card class="forgot-password-card" elevation="24">
          <!-- Header Section -->
          <div class="forgot-password-header">
            <v-avatar size="64" class="mb-4" color="primary">
              <v-icon size="40" color="white">mdi-lock-reset</v-icon>
            </v-avatar>
            <h1 class="text-h4 font-weight-bold mb-2 text-primary">Forgot Password?</h1>
            <p class="text-subtitle-1 text-medium-emphasis">
              Enter your email to receive reset instructions
            </p>
          </div>

          <v-divider class="my-6" />

          <!-- Form Section -->
          <v-card-text class="px-8 pb-8">
            <v-form @submit.prevent="handleForgotPassword">
              <FormInput
                :model-value="email"
                label="Email"
                type="email"
                prepend-inner-icon="mdi-email"
                variant="outlined"
                :error="errors.email"
                class="mb-4"
                @update:model-value="email = $event; validateField('email', $event, [required])"
              />

              <!-- Error alert -->
              <v-alert
                v-if="authStore.error"
                type="error"
                variant="tonal"
                class="mb-4"
                density="compact"
              >
                {{ authStore.error }}
              </v-alert>

              <!-- Success alert -->
              <v-alert
                v-if="success"
                type="success"
                variant="tonal"
                class="mb-4"
                icon="mdi-check-circle"
              >
                {{ success }}
              </v-alert>

              <v-btn
                color="primary"
                size="large"
                block
                :loading="authStore.loading"
                :disabled="authStore.loading || success || !email || !!errors.email"
                class="text-none font-weight-bold"
                elevation="2"
                @click="handleForgotPassword"
              >
                {{ authStore.loading ? 'Sending...' : 'Send Reset Link' }}
              </v-btn>

              <div class="text-center mt-4">
                <router-link to="/login" class="text-primary text-decoration-none">
                  <v-icon size="small" class="mr-1">mdi-arrow-left</v-icon>
                  Back to login
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
import { ref, onMounted } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { useFormValidation, required } from '@/composables/useFormValidation';
import FormInput from '@/components/form/FormInput.vue';

const authStore = useAuthStore();
const { errors, validateField, validateForm } = useFormValidation();
const email = ref('');
const success = ref('');

onMounted(() => {
  authStore.resetState();
});

async function handleForgotPassword() {
  const isValid = validateForm({
    email: { value: email.value, rules: [required] }
  });

  if (!isValid) return;

  try {
    await authStore.forgotPassword(email.value);
    success.value = 'If an account exists with this email, you will receive password reset instructions shortly.';
  } catch (error) {
    // Error is already handled in the store
    console.warn('Forgot password failed:', error.message);
  }
}
</script>

<style scoped>
.forgot-password-container {
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

.forgot-password-card {
  border-radius: 16px !important;
  overflow: hidden;
  backdrop-filter: blur(10px);
  background: rgba(var(--v-theme-surface), 0.95) !important;
  border: 1px solid rgba(var(--v-theme-on-surface), 0.1);
}

.forgot-password-header {
  text-align: center;
  padding: 48px 32px 0;
}
</style>
