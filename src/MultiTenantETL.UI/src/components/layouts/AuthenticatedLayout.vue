<template>
  <v-layout>
    <v-app-bar :color="appBarColor" density="compact" elevation="1">
      <template #default>
        <div class="d-flex align-center">
          <v-app-bar-nav-icon @click="drawer = !drawer" />
          <v-app-bar-title class="text-truncate font-weight-medium">
            ETL Portal
          </v-app-bar-title>
        </div>
        
        <v-spacer />
        
        <div class="d-flex align-center ga-2">
          <tenant-selector class="flex-shrink-0" />
          <language-switcher class="flex-shrink-0" />
          
          <!-- AI Chatbot Toggle -->
          <v-tooltip :text="$t('chatbot.openAssistant')" location="bottom">
            <template #activator="{ props }">
              <v-btn
                icon
                size="small"
                v-bind="props"
                class="flex-shrink-0"
                @click="openChatbot"
              >
                <v-icon>mdi-robot</v-icon>
              </v-btn>
            </template>
          </v-tooltip>
          
          <v-switch
            v-model="isDarkMode"
            hide-details
            inset
            density="compact"
            color="purple"
            class="flex-shrink-0"
            true-icon="mdi-weather-night"
            false-icon="mdi-weather-sunny"
            @update:model-value="toggleTheme"
          />
          
          <!-- User Menu -->
          <v-menu offset-y min-width="240">
            <template #activator="{ props }">
              <v-btn
                icon
                v-bind="props"
                class="flex-shrink-0 user-menu-btn"
              >
                <v-avatar size="32" color="primary">
                  <span class="text-caption text-white font-weight-bold">{{ userInitials }}</span>
                </v-avatar>
              </v-btn>
            </template>
            
            <v-card elevation="8" class="user-menu-card">
              <!-- User Info Header -->
              <v-card-text class="pa-4 pb-3">
                <div class="d-flex align-center">
                  <v-avatar size="40" color="primary" class="mr-3">
                    <span class="text-h6 text-white font-weight-bold">{{ userInitials }}</span>
                  </v-avatar>
                  <div class="flex-grow-1">
                    <div class="text-subtitle-1 font-weight-medium">{{ userName }}</div>
                    <div class="text-caption text-medium-emphasis">{{ userRole }}</div>
                  </div>
                </div>
              </v-card-text>
              
              <v-divider />
              
              <!-- Menu Items -->
              <v-list density="compact" class="py-2">
                <v-list-item
                  to="/settings"
                  prepend-icon="mdi-cog"
                  class="menu-item"
                  rounded="lg"
                >
                  <v-list-item-title class="text-body-2">{{ $t('nav.settings') }}</v-list-item-title>
                </v-list-item>
                
                <v-list-item
                  prepend-icon="mdi-logout"
                  class="menu-item logout-item"
                  rounded="lg"
                  @click="logout"
                >
                  <v-list-item-title class="text-body-2">{{ $t('nav.logout') }}</v-list-item-title>
                </v-list-item>
              </v-list>
            </v-card>
          </v-menu>
        </div>
      </template>
    </v-app-bar>

    <v-navigation-drawer
      v-model="drawer"
      :permanent="!isMobile"
      :temporary="isMobile"
      :theme="theme.global.name.value"
      class="sidebar-drawer"
    >
      <template #default>
        <!-- User profile section -->
        <v-list>
          <v-list-item>
            <template #prepend>
              <v-avatar color="primary">
                <template #default>
                  <span class="text-h6 text-white">{{ userInitials }}</span>
                </template>
              </v-avatar>
            </template>
            <v-list-item-title>{{ userName }}</v-list-item-title>
            <v-list-item-subtitle>{{ userRole }}</v-list-item-subtitle>
          </v-list-item>
        </v-list>

        <v-divider class="my-2"></v-divider>

        <!-- Navigation Items -->
        <v-list density="compact" nav>
          <v-list-item to="/" prepend-icon="mdi-view-dashboard" :title="$t('nav.dashboard')" rounded="lg">
            <template #title>{{ $t('nav.dashboard') }}</template>
          </v-list-item>
          
          <v-list-subheader>{{ $t('common.data') }}</v-list-subheader>
          
          <v-list-item to="/pipelines" prepend-icon="mdi-pipe" rounded="lg">
            <template #title>{{ $t('nav.pipelines') }}</template>
          </v-list-item>
          <v-list-item to="/connectors" prepend-icon="mdi-connection" rounded="lg">
            <template #title>{{ $t('nav.connectors') }}</template>
          </v-list-item>
          <v-list-item to="/schedules" prepend-icon="mdi-calendar-clock" rounded="lg">
            <template #title>{{ $t('nav.schedules') }}</template>
          </v-list-item>
          <v-list-item to="/executions" prepend-icon="mdi-history" rounded="lg">
            <template #title>{{ $t('nav.executions') }}</template>
          </v-list-item>
          
          <template v-if="isAdmin">
            <v-list-subheader>{{ $t('common.administration') }}</v-list-subheader>
            
            <v-list-item to="/tenants" prepend-icon="mdi-office-building" rounded="lg">
              <template #title>{{ $t('nav.tenants') }}</template>
            </v-list-item>
            <v-list-item to="/users" prepend-icon="mdi-account-group" rounded="lg">
              <template #title>{{ $t('nav.users') }}</template>
            </v-list-item>
            <v-list-item to="/audit-logs" prepend-icon="mdi-shield-check" rounded="lg">
              <template #title>{{ $t('nav.auditLogs') }}</template>
            </v-list-item>
          </template>
        </v-list>
      </template>
      
      <template #append>
        <v-divider></v-divider>
        <div class="px-2 py-1 text-center text-caption text-disabled">
          v1.2.0 • © 2025 ETL Portal
        </div>
      </template>
    </v-navigation-drawer>

    <v-main>
      <v-container fluid>
        <router-view />
      </v-container>
    </v-main>
  </v-layout>
</template>

<script setup>
import { ref, computed } from 'vue';
import { useTheme, useDisplay } from 'vuetify';
import { useAuthStore } from '@/stores/auth';
import TenantSelector from '@/components/TenantSelector.vue';
import LanguageSwitcher from '@/components/LanguageSwitcher.vue';

const theme = useTheme();
const { mobile } = useDisplay();
const drawer = ref(true);
const authStore = useAuthStore();

const isMobile = computed(() => mobile.value);

const isDarkTheme = computed(() => theme.global.current.value.dark);
const isAdmin = computed(() => authStore.isAdmin);
const isDarkMode = computed({
  get: () => isDarkTheme.value,
  set: () => {} // Toggle is handled by toggleTheme function
});

// Compute app bar color based on theme
const appBarColor = computed(() => 
  isDarkTheme.value ? 'grey-darken-3' : 'primary'
);

// User data computed properties
const userName = computed(() => authStore.user?.name || 'User');
const userRole = computed(() => authStore.user?.role || 'User');
const userInitials = computed(() => {
  const name = userName.value;
  if (!name) return 'U';
  
  const nameParts = name.split(' ');
  if (nameParts.length > 1) {
    return `${nameParts[0].charAt(0)}${nameParts[1].charAt(0)}`.toUpperCase();
  }
  return name.charAt(0).toUpperCase();
});

function toggleTheme() {
  theme.global.name.value = isDarkTheme.value ? 'light' : 'dark';
}

function logout() {
  authStore.logout();
}

function openChatbot() {
  if (window.openAiChatbot) {
    window.openAiChatbot();
  }
}
</script>

<style>
/* Softer app bar with gradient */
.v-theme--light .v-app-bar {
  background: linear-gradient(135deg, rgb(var(--v-theme-primary)) 0%, #1565C0 100%) !important;
  color: white !important;
}

.v-theme--light .v-app-bar .v-app-bar-title,
.v-theme--light .v-app-bar .v-btn,
.v-theme--light .v-app-bar .v-icon {
  color: white !important;
}

/* Softer user avatar in light mode for all containers */
.v-theme--light .v-avatar {
  background-color: rgba(var(--v-theme-primary), 0.12) !important;
}

/* Ensure avatar text (initials) is primary blue in light mode */
.v-theme--light .v-avatar span {
  color: rgb(var(--v-theme-primary)) !important;
}

/* Special case for App Bar avatar to stand out on the blue background */
.v-theme--light .v-app-bar .v-avatar {
  background-color: white !important;
}

/* User Menu Improvements */
.user-menu-btn {
  transition: transform 0.2s ease;
}

.user-menu-btn:hover {
  transform: scale(1.05);
}

.user-menu-card {
  border-radius: 12px !important;
  overflow: hidden;
}

.menu-item {
  margin: 0 8px;
  transition: all 0.2s ease;
}

.menu-item:hover {
  background: rgba(var(--v-theme-primary), 0.08) !important;
}

.logout-item:hover {
  background: rgba(var(--v-theme-error), 0.08) !important;
}

.logout-item:hover .v-list-item-title {
  color: rgb(var(--v-theme-error)) !important;
}

.logout-item:hover .v-icon {
  color: rgb(var(--v-theme-error)) !important;
}

/* Light mode menu card */
.v-theme--light .user-menu-card {
  background: #ffffff;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.1) !important;
}

/* Sidebar refinement */
.sidebar-drawer {
  border-right: 1px solid rgba(var(--v-theme-outline), 0.5) !important;
}

.sidebar-drawer .v-list-item--active {
  background: rgba(var(--v-theme-primary), 0.08) !important;
  color: rgb(var(--v-theme-primary)) !important;
  
  .v-icon {
    color: rgb(var(--v-theme-primary)) !important;
  }
}

.sidebar-drawer .v-list-subheader {
  font-size: 0.65rem !important;
  font-weight: 700 !important;
  text-transform: uppercase;
  letter-spacing: 0.1em !important;
  color: rgb(var(--v-theme-on-surface-variant)) !important;
  opacity: 0.7;
  padding-top: 16px !important;
}

/* Dark mode menu card */
.v-theme--dark .user-menu-card {
  background: rgb(var(--v-theme-surface));
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.4) !important;
}
</style>