<template>
  <v-app>
    <!-- Global Loading Overlay -->
    <v-overlay
      v-model="isGlobalLoading"
      class="align-center justify-center"
      persistent
      :z-index="10000"
      :scrim="true"
      opacity="0.7"
    >
      <v-fade-transition mode="out-in">
        <v-card v-if="isGlobalLoading" class="pa-8 text-center loading-card" elevation="12">
          <v-progress-circular
            indeterminate
            color="primary"
            size="64"
            width="6"
          />
          <div class="mt-4 text-h6">{{ loadingMessage }}</div>
          <div class="mt-2 text-caption text-grey">Please wait...</div>
        </v-card>
      </v-fade-transition>
    </v-overlay>

    <!-- Error Boundary -->
    <div v-if="hasGlobalError" class="error-boundary">
      <v-container class="fill-height">
        <v-row align="center" justify="center">
          <v-col cols="12" md="6">
            <v-card class="pa-6 text-center">
              <v-icon size="64" color="error" class="mb-4">
                mdi-alert-circle-outline
              </v-icon>
              <h2 class="text-h5 mb-4">Something went wrong</h2>
              <p class="text-body-1 mb-4">
                {{ globalErrorMessage }}
              </p>
              <v-btn
                color="primary"
                size="large"
                @click="reloadApp"
              >
                Reload Application
              </v-btn>
            </v-card>
          </v-col>
        </v-row>
      </v-container>
    </div>

    <!-- Main Application -->
    <template v-else>
      <!-- Route Loading Bar -->
      <v-progress-linear
        v-if="isRouteLoading"
        indeterminate
        color="primary"
        height="3"
        style="position: fixed; top: 0; left: 0; right: 0; z-index: 9999;"
      />
      
      <!-- Use guest layout for public routes, otherwise use auth-based layout -->
      <guest-layout v-if="isPublicRoute || !isAuthenticated" />
      <authenticated-layout v-else />
      
      <!-- AI Chatbot (only for authenticated users on non-public routes) -->
      <ai-chatbot v-if="isAuthenticated && !isPublicRoute" />
    </template>

    <!-- Global Notifications - Elegant Toast Style -->
    <div class="notifications-container">
      <TransitionGroup name="notification">
        <div
          v-for="notification in notifications"
          :key="notification.id"
          class="toast-notification"
          :class="`toast-${notification.type}`"
        >
          <div class="toast-icon">
            <v-icon :color="getNotificationColor(notification.type)">
              {{ getNotificationIcon(notification.type) }}
            </v-icon>
          </div>
          <div class="toast-content">
            <div class="toast-title">{{ notification.title || getNotificationTitle(notification.type) }}</div>
            <div class="toast-message">{{ notification.message }}</div>
          </div>
          <button class="toast-close" @click="removeNotification(notification.id)">
            <v-icon size="18">mdi-close</v-icon>
          </button>
        </div>
      </TransitionGroup>
    </div>
  </v-app>
</template>

<script setup>
import { computed, ref, onBeforeUnmount, onMounted, provide, onErrorCaptured } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { useRouter } from 'vue-router';
import { initializeMetadata } from '@/composables/useMetadata';
import AuthenticatedLayout from '@/components/layouts/AuthenticatedLayout.vue';
import GuestLayout from '@/components/layouts/GuestLayout.vue';
import AiChatbot from '@/components/AiChatbot.vue';

const authStore = useAuthStore();
const router = useRouter();
const isAuthenticated = computed(() => authStore.isAuthenticated);
const isPublicRoute = computed(() => router.currentRoute.value.meta.public === true);

// Notification system
const notifications = ref([]);
const notificationTimeout = ref(null);

// Global loading state
const isGlobalLoading = ref(false);
const loadingMessage = ref('Loading...');
const isRouteLoading = ref(false);

// Error boundary state
const hasGlobalError = ref(false);
const globalErrorMessage = ref('An unexpected error occurred. Please reload the application.');
const errorCount = ref(0);
const maxErrorsBeforeBoundary = 3;

const showNotification = (message, type = 'info', timeout = 5000, title = null) => {
  const id = Date.now();
  notifications.value.push({
    id,
    message,
    type,
    title
  });

  if (timeout > 0) {
    setTimeout(() => {
      removeNotification(id);
    }, timeout);
  }
};

const removeNotification = (id) => {
  notifications.value = notifications.value.filter(n => n.id !== id);
};

// Helper functions for notification styling
const getNotificationIcon = (type) => {
  switch (type) {
    case 'success': return 'mdi-check-circle';
    case 'error': return 'mdi-alert-circle';
    case 'warning': return 'mdi-alert';
    case 'info': return 'mdi-information';
    default: return 'mdi-information';
  }
};

const getNotificationColor = (type) => {
  switch (type) {
    case 'success': return '#4CAF50';
    case 'error': return '#F44336';
    case 'warning': return '#FF9800';
    case 'info': return '#2196F3';
    default: return '#2196F3';
  }
};

const getNotificationTitle = (type) => {
  switch (type) {
    case 'success': return 'Success';
    case 'error': return 'Error';
    case 'warning': return 'Warning';
    case 'info': return 'Info';
    default: return 'Notification';
  }
};

const showGlobalLoading = (message = 'Loading...') => {
  loadingMessage.value = message;
  isGlobalLoading.value = true;
};

const hideGlobalLoading = () => {
  isGlobalLoading.value = false;
};

const handleError = (error, context = '') => {
  console.error(`Error ${context}:`, error);
  
  errorCount.value++;
  
  // If too many errors occur, show error boundary
  if (errorCount.value >= maxErrorsBeforeBoundary) {
    hasGlobalError.value = true;
    globalErrorMessage.value = 'Multiple errors occurred. The application needs to be reloaded.';
    return;
  }
  
  // Show user-friendly error message
  const message = error.userMessage || error.message || 'An unexpected error occurred.';
  showNotification(message, 'error', 7000);
};

const reloadApp = () => {
  window.location.reload();
};

// Vue error handler - catches errors in components
onErrorCaptured((error, instance, info) => {
  console.error('Component error:', { error, info });
  handleError(error, `in component (${info})`);
  // Return false to prevent error from propagating
  return false;
});

// Global JavaScript error handler
window.onerror = (message, source, lineno, colno, error) => {
  console.error('Global error:', { message, source, lineno, colno, error });
  handleError(error || new Error(message), 'in global handler');
  return true; // Prevent default browser error handling
};

// Handle unhandled promise rejections
window.onunhandledrejection = (event) => {
  console.error('Unhandled promise rejection:', event.reason);
  handleError(event.reason, 'in promise');
  event.preventDefault(); // Prevent default browser error handling
};

// Router error handling
router.onError((error) => {
  console.error('Router error:', error);
  handleError(error, 'in router');
});

// Reset error count periodically (errors might be transient)
const errorResetInterval = setInterval(() => {
  if (errorCount.value > 0 && !hasGlobalError.value) {
    errorCount.value = Math.max(0, errorCount.value - 1);
  }
}, 30000); // Decrease error count every 30 seconds

// Route loading handlers
let loadingStartTime = 0;
const MIN_LOADING_TIME = 200; // Minimum time to show loading bar (ms)

const handleRouteLoadingStart = () => {
  loadingStartTime = Date.now();
  isRouteLoading.value = true;
};

const handleRouteLoadingEnd = () => {
  const elapsed = Date.now() - loadingStartTime;
  const remaining = MIN_LOADING_TIME - elapsed;
  
  if (remaining > 0) {
    // Keep loading bar visible for minimum time
    setTimeout(() => {
      isRouteLoading.value = false;
    }, remaining);
  } else {
    // Enough time has passed, hide immediately
    isRouteLoading.value = false;
  }
};

onMounted(async () => {
  // Initialize metadata on app startup
  try {
    await initializeMetadata();
  } catch (error) {
    console.warn('Failed to initialize metadata:', error);
    // App will continue with cached or default values
  }
  
  // Listen for route loading events
  window.addEventListener('route-loading-start', handleRouteLoadingStart);
  window.addEventListener('route-loading-end', handleRouteLoadingEnd);
});

// Clean up on component unmount
onBeforeUnmount(() => {
  if (notificationTimeout.value) {
    clearTimeout(notificationTimeout.value);
  }
  clearInterval(errorResetInterval);
  window.removeEventListener('route-loading-start', handleRouteLoadingStart);
  window.removeEventListener('route-loading-end', handleRouteLoadingEnd);
});

// Provide functions to child components
provide('showNotification', showNotification);
provide('removeNotification', removeNotification);
provide('showGlobalLoading', showGlobalLoading);
provide('hideGlobalLoading', hideGlobalLoading);
provide('handleError', handleError);
</script>

<style>
.v-container {
  padding: 16px;
}

/* Navigation drawer styling */
.sidebar-drawer .v-list-item--active {
  position: relative;
  font-weight: 600;
}

.sidebar-drawer .v-list-item--active::before {
  content: "";
  position: absolute;
  top: 0;
  bottom: 0;
  left: 0;
  width: 4px;
  background-color: rgb(var(--v-theme-primary));
  border-radius: 0 3px 3px 0;
}

.sidebar-drawer .v-list-item {
  margin: 4px 8px;
  min-height: 40px;
}

/* Theme consistency variables */
:root {
  --app-card-hover-transform: translateY(-5px);
  --app-transition-speed: 0.3s;
  --app-border-radius: 8px;
  --app-surface-light: #ffffff;
  --app-surface-dark: #1e1e1e;
  --app-code-bg-light: #F5F5F5;
  --app-code-bg-dark: #212121;
  --app-code-text-light: #1A1A1A;
  --app-code-text-dark: #e0e0e0;
  --app-divider-light: rgba(0, 0, 0, 0.12);
  --app-divider-dark: rgba(255, 255, 255, 0.12);
}

/* Light mode improvements */
.v-theme--light {
  --v-border-color: #E0E0E0;
}

.v-theme--light .v-card {
  border: 1px solid #E0E0E0;
}

.v-theme--light .v-select,
.v-theme--light .v-text-field,
.v-theme--light .v-textarea {
  --v-field-border-opacity: 0.38;
}

.v-theme--light .v-field--variant-outlined .v-field__outline {
  --v-field-border-opacity: 0.38;
}

.v-theme--light .v-alert {
  border: 1px solid currentColor;
  border-opacity: 0.2;
}

/* Card hover effect used across the app */
.app-card-hover {
  transition: all var(--app-transition-speed) ease-in-out;
}

.app-card-hover:hover {
  transform: var(--app-card-hover-transform);
}

/* Consistent log containers across the app */
.app-log-container {
  font-family: 'Consolas', 'Monaco', monospace;
  white-space: pre-wrap;
  font-size: 13px;
  background-color: var(--v-theme-surface-variant);
  border-radius: var(--app-border-radius);
  padding: 12px;
  max-height: 450px;
  overflow-y: auto;
}

.app-log-text {
  color: var(--v-theme-on-surface);
  line-height: 1.5;
}

/* Dark mode specific overrides */
.v-theme--dark .app-log-container {
  background-color: var(--app-code-bg-dark);
}

.v-theme--dark .app-log-text {
  color: var(--app-code-text-dark);
}

.v-theme--light .app-log-container {
  background-color: var(--app-code-bg-light);
  border: 1px solid #E0E0E0;
}

.v-theme--light .app-log-text {
  color: var(--app-code-text-light);
}

/* Light mode card improvements */
.v-theme--light .v-card {
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.12), 0 1px 2px rgba(0, 0, 0, 0.08) !important;
}

.v-theme--light .v-card:hover {
  box-shadow: 0 4px 6px rgba(0, 0, 0, 0.12), 0 2px 4px rgba(0, 0, 0, 0.08) !important;
}

.v-theme--light .v-card-title {
  color: #1A1A1A;
  font-weight: 600;
}

.v-theme--light .v-card-text {
  color: #424242;
}

.v-theme--light .bg-surface-variant {
  background-color: #F5F5F5 !important;
}

/* Light mode error/warning cards - comprehensive selectors */
.v-theme--light .v-card.v-card--variant-outlined.text-error,
.v-theme--light .v-card.v-card--variant-outlined.bg-error,
.v-theme--light .v-card[color="error"] {
  background-color: #FFEBEE !important;
  border-color: #EF5350 !important;
  border-width: 2px !important;
}

.v-theme--light .v-card.v-card--variant-outlined.text-error *,
.v-theme--light .v-card.v-card--variant-outlined.bg-error *,
.v-theme--light .v-card[color="error"] * {
  color: #C62828 !important;
}

.v-theme--light .v-card.v-card--variant-outlined.text-error .text-subtitle-2,
.v-theme--light .v-card.v-card--variant-outlined.bg-error .text-subtitle-2,
.v-theme--light .v-card[color="error"] .text-subtitle-2 {
  color: #B71C1C !important;
  font-weight: 600;
}

.v-theme--light .v-card.v-card--variant-outlined.text-warning,
.v-theme--light .v-card.v-card--variant-outlined.bg-warning,
.v-theme--light .v-card[color="warning"] {
  background-color: #FFF3E0 !important;
  border-color: #FFA726 !important;
  border-width: 2px !important;
}

.v-theme--light .v-card.v-card--variant-outlined.text-warning *,
.v-theme--light .v-card.v-card--variant-outlined.bg-warning *,
.v-theme--light .v-card[color="warning"] * {
  color: #E65100 !important;
}

.v-theme--light .v-card.v-card--variant-outlined.text-warning .text-subtitle-2,
.v-theme--light .v-card.v-card--variant-outlined.bg-warning .text-subtitle-2,
.v-theme--light .v-card[color="warning"] .text-subtitle-2 {
  color: #E65100 !important;
  font-weight: 600;
}

/* Light mode stepper improvements */
.v-theme--light .v-stepper {
  background-color: #FFFFFF;
  border: 1px solid #E0E0E0;
}

.v-theme--light .v-stepper-header {
  box-shadow: none;
  border-bottom: 1px solid #E0E0E0;
}

.v-theme--light .v-stepper-item {
  opacity: 0.6;
}

.v-theme--light .v-stepper-item--selected,
.v-theme--light .v-stepper-item--complete {
  opacity: 1;
}

.v-theme--light .v-stepper-item__avatar {
  border: 2px solid #BDBDBD;
  background-color: #FAFAFA;
  color: #757575;
}

.v-theme--light .v-stepper-item--selected .v-stepper-item__avatar {
  border-color: var(--v-theme-primary);
  background-color: var(--v-theme-primary);
  color: #FFFFFF;
}

.v-theme--light .v-stepper-item--complete .v-stepper-item__avatar {
  border-color: var(--v-theme-success);
  background-color: var(--v-theme-success);
  color: #FFFFFF;
}

.v-theme--light .v-stepper-item__title {
  color: #424242;
}

.v-theme--light .v-stepper-item--selected .v-stepper-item__title {
  color: var(--v-theme-primary);
  font-weight: 600;
}

.v-theme--light .v-stepper-item__subtitle {
  color: #757575;
}

/* Light mode alert improvements */
.v-theme--light .v-alert {
  border-width: 1px;
  border-style: solid;
}

.v-theme--light .v-alert--variant-tonal.v-alert--type-error,
.v-theme--light .v-alert--variant-outlined.v-alert--type-error,
.v-theme--light .v-alert.text-error {
  background-color: #FFEBEE !important;
  border-color: #EF5350 !important;
  color: #C62828 !important;
}

.v-theme--light .v-alert--variant-tonal.v-alert--type-warning,
.v-theme--light .v-alert--variant-outlined.v-alert--type-warning,
.v-theme--light .v-alert.text-warning {
  background-color: #FFF3E0 !important;
  border-color: #FFA726 !important;
  color: #E65100 !important;
}

.v-theme--light .v-alert--variant-tonal.v-alert--type-success,
.v-theme--light .v-alert--variant-outlined.v-alert--type-success,
.v-theme--light .v-alert.text-success {
  background-color: #E8F5E9 !important;
  border-color: #66BB6A !important;
  color: #2E7D32 !important;
}

.v-theme--light .v-alert--variant-tonal.v-alert--type-info,
.v-theme--light .v-alert--variant-outlined.v-alert--type-info,
.v-theme--light .v-alert.text-info {
  background-color: #E3F2FD !important;
  border-color: #42A5F5 !important;
  color: #1565C0 !important;
}

/* Ensure alert text and titles are visible */
.v-theme--light .v-alert__content {
  color: inherit !important;
}

.v-theme--light .v-alert .v-alert-title {
  color: inherit !important;
  font-weight: 600;
}

/* Status chip consistency */
.app-status-chip {
  font-weight: 500;
}

/* Theme-aware scrollbars */
.custom-scrollbar::-webkit-scrollbar {
  width: 6px;
  height: 6px;
}

.custom-scrollbar::-webkit-scrollbar-thumb {
  background-color: rgba(var(--v-theme-on-surface-variant), 0.3);
  border-radius: 3px;
}

.custom-scrollbar::-webkit-scrollbar-track {
  background-color: rgba(var(--v-theme-on-surface-variant), 0.1);
}

/* Card and container consistency */
.theme-card {
  background-color: var(--v-theme-surface) !important;
  border: 1px solid var(--v-theme-outline-variant) !important;
  border-radius: var(--app-border-radius) !important;
}

/* Light mode form field improvements */
.v-theme--light .v-field--variant-outlined {
  background-color: #FFFFFF;
}

.v-theme--light .v-field--variant-outlined:hover .v-field__outline {
  --v-field-border-opacity: 0.6;
}

.v-theme--light .v-field--variant-outlined.v-field--focused .v-field__outline {
  --v-field-border-opacity: 1;
}

.v-theme--light .v-field--error .v-field__outline {
  color: #D32F2F !important;
}

/* Light mode expansion panel improvements */
.v-theme--light .v-expansion-panel {
  background-color: #FFFFFF;
  border: 1px solid #E0E0E0;
  margin-bottom: 8px;
}

.v-theme--light .v-expansion-panel-title {
  background-color: #FAFAFA;
}

.v-theme--light .v-expansion-panel-title:hover {
  background-color: #F5F5F5;
}

/* Light mode chip improvements */
.v-theme--light .v-chip {
  border: 1px solid rgba(0, 0, 0, 0.12);
}

/* Light mode divider improvements */
.v-theme--light .v-divider {
  border-color: rgba(0, 0, 0, 0.12);
  opacity: 1;
}

/* Light mode list improvements */
.v-theme--light .v-list-item:hover {
  background-color: rgba(0, 0, 0, 0.04);
}

.v-theme--light .v-list-item--active {
  background-color: rgba(21, 101, 192, 0.08);
}

.v-theme--light .v-list-item-title {
  color: #424242;
  font-weight: 500;
}

.v-theme--light .v-list-item-subtitle {
  color: #1A1A1A !important;
  opacity: 0.87;
  font-weight: 400;
}

/* Light mode table improvements */
.v-theme--light .v-table {
  background-color: #FFFFFF;
  border: 1px solid #E0E0E0;
}

.v-theme--light .v-table > .v-table__wrapper > table > thead > tr > th {
  background-color: #FAFAFA;
  color: #424242;
  font-weight: 600;
  border-bottom: 2px solid #E0E0E0;
}

.v-theme--light .v-table > .v-table__wrapper > table > tbody > tr:hover {
  background-color: #F5F5F5;
}

.v-theme--light .v-table > .v-table__wrapper > table > tbody > tr > td {
  border-bottom: 1px solid #F0F0F0;
}

/* Light mode button improvements */
.v-theme--light .v-btn--variant-outlined {
  border-color: rgba(0, 0, 0, 0.23);
}

.v-theme--light .v-btn--variant-outlined:hover {
  background-color: rgba(0, 0, 0, 0.04);
}

.v-theme--light .v-btn--variant-text:hover {
  background-color: rgba(0, 0, 0, 0.04);
}

/* Light mode navigation drawer improvements */
.v-theme--light .v-navigation-drawer {
  background-color: #FFFFFF;
  border-right: 1px solid #E0E0E0;
}

.v-theme--light .v-navigation-drawer .v-list {
  background-color: transparent;
}

.v-theme--light .v-navigation-drawer .v-list-item {
  color: #424242;
}

.v-theme--light .v-navigation-drawer .v-list-item-title {
  color: #424242 !important;
}

.v-theme--light .v-navigation-drawer .v-list-item-subtitle {
  color: #757575 !important;
}

.v-theme--light .v-navigation-drawer .v-list-item:hover {
  background-color: rgba(0, 0, 0, 0.04);
}

.v-theme--light .v-navigation-drawer .v-list-item--active {
  background-color: rgba(21, 101, 192, 0.08);
  color: var(--v-theme-primary);
}

.v-theme--light .v-navigation-drawer .v-list-item--active .v-list-item-title {
  color: var(--v-theme-primary) !important;
}

.v-theme--light .v-navigation-drawer .v-list-subheader {
  color: #757575 !important;
  opacity: 1;
}

.v-theme--light .v-navigation-drawer .text-disabled {
  color: #9E9E9E !important;
}

/* Light mode app bar improvements */
.v-theme--light .v-app-bar {
  background-color: #FFFFFF !important;
  border-bottom: 1px solid #E0E0E0;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.08) !important;
}

/* Light mode dialog improvements */
.v-theme--light .v-dialog > .v-overlay__content > .v-card {
  box-shadow: 0 11px 15px -7px rgba(0, 0, 0, 0.2),
              0 24px 38px 3px rgba(0, 0, 0, 0.14),
              0 9px 46px 8px rgba(0, 0, 0, 0.12) !important;
}

/* Light mode text utilities */
.v-theme--light .text-grey,
.v-theme--light .text-grey-darken-1 {
  color: #616161 !important;
}

.v-theme--light .text-grey-darken-2 {
  color: #424242 !important;
}

.v-theme--light .text-grey-lighten-1 {
  color: #757575 !important;
}

/* Override text-grey inside error/warning contexts */
.v-theme--light .v-card[color="error"] .text-grey,
.v-theme--light .v-card.text-error .text-grey {
  color: #C62828 !important;
  opacity: 0.87;
}

.v-theme--light .v-card[color="warning"] .text-grey,
.v-theme--light .v-card.text-warning .text-grey {
  color: #E65100 !important;
  opacity: 0.87;
}

/* Light mode heading improvements */
.v-theme--light .text-h5,
.v-theme--light .text-h6,
.v-theme--light .text-subtitle-1,
.v-theme--light .text-subtitle-2 {
  color: #1A1A1A !important;
}

/* Light mode table text in summary */
.v-theme--light .v-table tbody td {
  color: #424242 !important;
}

.v-theme--light .v-table thead th {
  color: #1A1A1A !important;
}

/* Light mode icon color improvements */
.v-theme--light .v-icon.text-blue,
.v-theme--light .v-icon[color="blue"] {
  color: #1565C0 !important;
  opacity: 1 !important;
}

.v-theme--light .v-icon.text-green,
.v-theme--light .v-icon[color="green"] {
  color: #2E7D32 !important;
  opacity: 1 !important;
}

.v-theme--light .v-icon.text-red,
.v-theme--light .v-icon[color="red"] {
  color: #C62828 !important;
  opacity: 1 !important;
}

.v-theme--light .v-icon.text-orange,
.v-theme--light .v-icon[color="orange"] {
  color: #E65100 !important;
  opacity: 1 !important;
}

.v-theme--light .v-icon.text-purple,
.v-theme--light .v-icon[color="purple"] {
  color: #6A1B9A !important;
  opacity: 1 !important;
}

.v-theme--light .v-icon.text-cyan,
.v-theme--light .v-icon[color="cyan"] {
  color: #00838F !important;
  opacity: 1 !important;
}

/* Ensure list item prepend icons are fully opaque */
.v-theme--light .v-list-item__prepend .v-icon {
  opacity: 1 !important;
}

/* Theme transition */
.v-application {
  transition: background-color var(--app-transition-speed) ease;
}

/* Timeline consistency */
.app-timeline-item {
  margin-bottom: 0 !important;
}

.app-timeline-card {
  border-radius: var(--app-border-radius);
  transition: transform var(--app-transition-speed) ease;
}

.app-timeline-card:hover {
  transform: translateX(4px);
}

/* Banner and header consistency */
.app-banner {
  position: relative;
  overflow: hidden;
  border-radius: var(--app-border-radius) var(--app-border-radius) 0 0;
}

.app-banner::before {
  content: "";
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: linear-gradient(45deg, rgba(0,0,0,0.3), rgba(0,0,0,0));
  pointer-events: none;
}

.notifications-container {
  position: fixed;
  top: 50%;
  right: 16px;
  transform: translateY(-50%);
  z-index: 9999;
  max-width: 380px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.toast-notification {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  padding: 16px;
  background: rgb(var(--v-theme-surface));
  border-radius: 8px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15), 0 0 0 1px rgba(var(--v-border-color), 0.1);
  min-width: 320px;
  backdrop-filter: blur(10px);
}

.toast-icon {
  flex-shrink: 0;
  width: 24px;
  height: 24px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.toast-content {
  flex: 1;
  min-width: 0;
}

.toast-title {
  font-weight: 600;
  font-size: 14px;
  color: rgb(var(--v-theme-on-surface));
  margin-bottom: 4px;
}

.toast-message {
  font-size: 13px;
  color: rgba(var(--v-theme-on-surface), 0.7);
  line-height: 1.4;
}

.toast-close {
  flex-shrink: 0;
  background: none;
  border: none;
  padding: 4px;
  cursor: pointer;
  color: rgba(var(--v-theme-on-surface), 0.5);
  border-radius: 4px;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s ease;
}

.toast-close:hover {
  background: rgba(var(--v-theme-on-surface), 0.1);
  color: rgba(var(--v-theme-on-surface), 0.8);
}

.notification-enter-active,
.notification-leave-active {
  transition: all 0.3s ease;
}

.notification-enter-from,
.notification-leave-to {
  opacity: 0;
  transform: translateX(30px);
}

.error-boundary {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background-color: var(--v-theme-background);
}

.loading-card {
  min-width: 320px;
  animation: fadeInScale 0.3s cubic-bezier(0.4, 0, 0.2, 1);
}

@keyframes fadeInScale {
  from {
    opacity: 0;
    transform: scale(0.95) translateY(-10px);
  }
  to {
    opacity: 1;
    transform: scale(1) translateY(0);
  }
}

/* Smooth fade out */
.v-overlay--active {
  transition: opacity 0.3s cubic-bezier(0.4, 0, 0.2, 1);
}
</style>
