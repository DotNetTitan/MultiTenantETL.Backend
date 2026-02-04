<template>
  <div class="confirm-email-container">
    <v-row class="fill-height" align="center" justify="center">
      <v-col cols="12" sm="8" md="6" lg="4">
        <v-card class="confirm-card" elevation="24">
          <v-card-text class="text-center pa-8">
            <!-- Loading State -->
            <div v-if="loading">
              <v-progress-circular
                indeterminate
                color="primary"
                size="64"
                class="mb-4"
              />
              <h2 class="text-h5 font-weight-bold mb-2">Verifying Email</h2>
              <p class="text-medium-emphasis">Please wait while we verify your email address...</p>
            </div>

            <!-- Success State -->
            <div v-else-if="success">
              <v-avatar color="success" size="80" class="mb-4">
                <v-icon size="48" color="white">mdi-check-circle</v-icon>
              </v-avatar>
              <h2 class="text-h5 font-weight-bold mb-2 text-success">Email Verified!</h2>
              <p class="text-medium-emphasis mb-6">
                Your email has been successfully verified. You will be automatically redirected in {{ countdown }} second{{ countdown !== 1 ? 's' : '' }}, or click the button below to continue now.
              </p>
              <v-btn
                color="primary"
                size="large"
                block
                class="text-none font-weight-bold"
                @click="handleRedirect"
              >
                Continue Now
              </v-btn>
            </div>

            <!-- Error State -->
            <div v-else>
              <v-avatar color="error" size="80" class="mb-4">
                <v-icon size="48" color="white">mdi-alert-circle</v-icon>
              </v-avatar>
              <h2 class="text-h5 font-weight-bold mb-2 text-error">Verification Failed</h2>
              <p class="text-medium-emphasis mb-6">
                {{ error || 'We could not verify your email address. The link may be invalid or expired.' }}
              </p>
              <v-btn
                color="primary"
                variant="outlined"
                size="large"
                block
                to="/login"
                class="text-none font-weight-bold"
              >
                Back to Login
              </v-btn>
            </div>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useAuthStore } from '@/stores/auth';
import { authService } from '@/services/authService';

const route = useRoute();
const router = useRouter();
const authStore = useAuthStore();
const loading = ref(true);
const success = ref(false);
const error = ref('');
const countdown = ref(10);
let countdownInterval = null;

const handleRedirect = () => {
  if (countdownInterval) {
    clearInterval(countdownInterval);
  }
  if (authStore.isAuthenticated) {
    router.push('/dashboard');
  } else {
    router.push('/login');
  }
};

onMounted(async () => {
  const { userId, token } = route.query;

  if (!userId || !token) {
    error.value = 'Invalid verification link.';
    loading.value = false;
    return;
  }

  try {
    await authService.confirmEmail(userId, token);
    success.value = true;
    
    // Start countdown
    countdownInterval = setInterval(() => {
      countdown.value--;
      if (countdown.value <= 0) {
        handleRedirect();
      }
    }, 1000);
  } catch (err) {
    console.error('Email confirmation failed:', err);
    error.value = err.response?.data?.message || 'Failed to verify email. Please try again.';
  } finally {
    loading.value = false;
  }
});
</script>

<style scoped>
.confirm-email-container {
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

.confirm-card {
  border-radius: 16px !important;
  overflow: hidden;
  backdrop-filter: blur(10px);
  background: rgba(var(--v-theme-surface), 0.95) !important;
  border: 1px solid rgba(var(--v-theme-on-surface), 0.1);
}
</style>
