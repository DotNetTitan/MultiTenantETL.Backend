<template>
  <div class="reset-password-container">
    <v-row class="fill-height" align="center" justify="center">
      <v-col cols="12" sm="8" md="5" lg="4" xl="3">
        <v-card class="reset-password-card" elevation="24">
          <!-- Header Section -->
          <div class="reset-password-header">
            <v-avatar size="64" class="mb-4" color="primary">
              <v-icon size="40" color="white">mdi-lock-check</v-icon>
            </v-avatar>
            <h1 class="text-h4 font-weight-bold mb-2 text-primary">Reset Password</h1>
            <p class="text-subtitle-1 text-medium-emphasis">
              Enter your new password
            </p>
          </div>

          <v-divider class="my-6" />

          <!-- Form Section -->
          <v-card-text class="px-8 pb-8">
            <v-form @submit.prevent="handleResetPassword">
              <FormInput
                v-model="newPassword"
                label="New Password"
                type="password"
                prepend-inner-icon="mdi-lock"
                variant="outlined"
                :error="errors.newPassword"
                :disabled="loading || !!success"
                class="mb-2"
              />
              <p class="text-caption text-medium-emphasis mb-4">
                Must contain uppercase, lowercase, digit, and special character
              </p>

              <!-- Error alert -->
              <v-alert
                v-if="error"
                type="error"
                variant="tonal"
                class="mb-4"
                density="compact"
              >
                {{ error }}
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
                :loading="loading"
                :disabled="loading || success || !newPassword || !!errors.newPassword"
                class="text-none font-weight-bold"
                elevation="2"
                @click="handleResetPassword"
              >
                {{ loading ? 'Resetting...' : 'Reset Password' }}
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
import { ref, onMounted, watch } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import { useAuthStore } from '@/stores/auth';
import { useFormValidation, required, minLength } from '@/composables/useFormValidation';
import FormInput from '@/components/form/FormInput.vue';

const router = useRouter();
const route = useRoute();
const authStore = useAuthStore();
const { errors, validateField, validateForm } = useFormValidation();

const newPassword = ref('');
const userId = ref('');
const token = ref('');
const loading = ref(false);
const error = ref('');
const success = ref('');

// Watch for changes to trigger validation
watch(newPassword, (newValue) => {
  if (newValue) {
    validateField('newPassword', newValue, [required, minLength(8)]);
  } else {
    validateField('newPassword', newValue, [required]);
  }
});

onMounted(() => {
  authStore.resetState();
  
  // Get userId and token from query parameters
  userId.value = route.query.userId || '';
  token.value = route.query.token || '';

  // Only set error if both are missing, otherwise allow user to try
  // (backend will validate the token properly)
});

async function handleResetPassword() {
  if (!userId.value || !token.value) {
    error.value = 'Invalid password reset link.';
    return;
  }

  const isValid = validateForm({
    newPassword: { value: newPassword.value, rules: [required, minLength(8)] }
  });

  if (!isValid) return;

  try {
    loading.value = true;
    error.value = '';
    
    await authStore.resetPassword(userId.value, token.value, newPassword.value);
    success.value = 'Password reset successfully! Redirecting to login...';
    
    // Redirect to login after 2 seconds
    setTimeout(() => {
      router.push('/login');
    }, 2000);
  } catch (err) {
    console.error('Reset password failed:', err);
    error.value = err.response?.data?.message || 'Failed to reset password. Please try again.';
  } finally {
    loading.value = false;
  }
}
</script>

<style scoped>
.reset-password-container {
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

.reset-password-card {
  border-radius: 16px !important;
  overflow: hidden;
  backdrop-filter: blur(10px);
  background: rgba(var(--v-theme-surface), 0.95) !important;
  border: 1px solid rgba(var(--v-theme-on-surface), 0.1);
}

.reset-password-header {
  text-align: center;
  padding: 48px 32px 0;
}
</style>
