<template>
  <v-container class="fill-height" fluid>
    <v-row align="center" justify="center">
      <v-col cols="12" sm="8" md="6" lg="4">
        <v-card class="pa-8 text-center">
          <v-progress-circular
            v-if="!error"
            indeterminate
            color="primary"
            size="64"
            class="mb-4"
          />
          <v-icon v-else color="error" size="64" class="mb-4">
            mdi-alert-circle
          </v-icon>
          
          <h2 class="text-h5 mb-2">
            {{ error ? 'Authentication Failed' : $t('auth.processingLogin') }}
          </h2>
          <p class="text-body-2 text-medium-emphasis">
            {{ error || $t('auth.pleaseWait') }}
          </p>

          <v-btn
            v-if="error"
            color="primary"
            class="mt-4"
            @click="router.push('/login')"
          >
            Return to Login
          </v-btn>
        </v-card>
      </v-col>
    </v-row>
  </v-container>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { authService } from '@/services/authService'

const router = useRouter()
const route = useRoute()
const authStore = useAuthStore()
const error = ref(null)

onMounted(async () => {
  // Official Vite/Vue pattern: Check if side effect already occurred
  // If user is already authenticated, the callback was already processed
  if (authStore.isAuthenticated) {
    // Already processed, redirect to dashboard
    await router.replace('/dashboard')
    return
  }

  try {
    // Extract code and state from URL query parameters
    const code = route.query.code
    const state = route.query.state
    const errorParam = route.query.error
    const errorDescription = route.query.error_description

    // Check for OAuth errors from authorization server
    if (errorParam) {
      error.value = errorDescription || errorParam
      console.error('OAuth error:', errorParam, errorDescription)
      return
    }

    // Validate required parameters
    if (!code || !state) {
      error.value = 'Missing authorization code or state parameter'
      console.error('Invalid callback - missing parameters')
      return
    }

    // Exchange authorization code for tokens
    // Note: Authorization codes are single-use, so if this is called twice,
    // the backend will reject the second attempt (proper OAuth behavior)
    const result = await authService.handleCallback(code, state)

    // Update auth store
    await authStore.setUser(result.user)

    // Redirect to dashboard (use replace to avoid back button issues)
    await router.replace('/dashboard')
  } catch (err) {
    console.error('Auth callback error:', err)
    error.value = err.message || 'Authentication failed'
    
    // Redirect to login after showing error
    setTimeout(() => {
      router.replace('/login')
    }, 3000)
  }
})
</script>
