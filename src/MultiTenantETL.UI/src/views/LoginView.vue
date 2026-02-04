<template>
  <div class="login-container">
    <v-row class="fill-height" align="center" justify="center">
      <v-col cols="12" sm="8" md="5" lg="4" xl="3">
        <v-card class="login-card" elevation="24">
          <!-- Header Section with Icon -->
          <div class="login-header">
            <v-avatar size="64" class="mb-4" color="primary">
              <v-icon size="40" color="white">mdi-database-sync</v-icon>
            </v-avatar>
            <h1 class="text-h4 font-weight-bold mb-2 text-primary">ETL Portal</h1>
            <p class="text-subtitle-1 text-medium-emphasis">Multi-Tenant ETL Platform</p>
          </div>

          <v-divider class="my-6" />

          <!-- Login Section -->
          <v-card-text class="px-8 pb-8">
            <!-- Show redirecting message when login is initiated -->
            <template v-if="isRedirecting">
              <div class="text-center py-4">
                <v-progress-circular
                  indeterminate
                  color="primary"
                  size="48"
                  class="mb-4"
                />
                <p class="text-body-1 text-medium-emphasis">
                  Redirecting to login...
                </p>
              </div>
            </template>

            <!-- Normal login view -->
            <template v-else>
              <p class="text-body-1 text-center text-medium-emphasis mb-6">
                {{ $t('auth.signInPrompt') || 'Sign in to access your ETL pipelines and data integrations.' }}
              </p>
              
              <!-- Error alert for API server offline -->
              <v-alert
                v-if="authStore.apiOffline"
                type="warning"
                variant="tonal"
                icon="mdi-connection"
                :title="$t('auth.connectionError')"
                class="mb-4"
              >
                <p>{{ $t('auth.apiOfflineMessage') }}</p>
                <ul class="ml-4 mt-2">
                  <li>{{ $t('auth.apiOfflineReason1') }}</li>
                  <li>{{ $t('auth.apiOfflineReason2') }}</li>
                  <li>{{ $t('auth.apiOfflineReason3') }}</li>
                </ul>
                <p class="mt-2 text-caption">
                  {{ $t('auth.currentApiUrl') }}: {{ apiUrl }}
                </p>
              </v-alert>

              <!-- General error alert -->
              <v-alert
                v-if="authStore.error && !authStore.apiOffline"
                type="error"
                variant="tonal"
                class="mb-4"
                density="compact"
              >
                {{ authStore.error }}
              </v-alert>

              <v-btn
                color="primary"
                size="large"
                block
                :loading="authStore.loading"
                :disabled="authStore.loading"
                class="text-none font-weight-bold"
                elevation="2"
                @click="handleLogin"
              >
                <v-icon start>mdi-login</v-icon>
                {{ $t('auth.signIn') || 'Sign In' }}
              </v-btn>

              <div class="text-center mt-6">
                <span class="text-medium-emphasis">Don't have an account?</span>
                <router-link to="/register" class="text-primary text-decoration-none ml-1">
                  Sign up
                </router-link>
              </div>
            </template>
          </v-card-text>
        </v-card>

        <!-- Footer Info -->
        <div class="text-center mt-6">
          <p class="text-caption text-medium-emphasis">
            {{ $t('auth.secureMessage') }}
          </p>
        </div>
      </v-col>
    </v-row>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { API_CONFIG } from '@/config/api';

const authStore = useAuthStore();
const isRedirecting = ref(false);

// Reset state on mount
onMounted(() => {
  authStore.resetState();
});

// Get the API URL for display in error message
const apiUrl = computed(() => {
  return API_CONFIG.baseURL;
});

/**
 * Initiate OAuth 2.0 Authorization Code Flow with PKCE
 * This redirects the browser to the authorization server's login page
 */
async function handleLogin() {
  try {
    isRedirecting.value = true;
    await authStore.login();
    // Browser redirects, so this code won't execute
  } catch (error) {
    isRedirecting.value = false;
    // Error is already handled in the store
    console.warn('Login initiation failed:', error.message);
  }
}
</script>

<style scoped>
.login-container {
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

.login-card {
  border-radius: 16px !important;
  overflow: hidden;
  backdrop-filter: blur(10px);
  background: rgba(var(--v-theme-surface), 0.95) !important;
  border: 1px solid rgba(var(--v-theme-on-surface), 0.1);
}

.login-header {
  text-align: center;
  padding: 48px 32px 0;
}
</style>
