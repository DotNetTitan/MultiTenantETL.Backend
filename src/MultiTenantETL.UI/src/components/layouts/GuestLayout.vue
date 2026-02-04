<template>
  <v-layout>
    <v-app-bar 
      :color="appBarColor" 
      density="compact" 
      elevation="1"
    >
      <v-app-bar-title class="text-truncate font-weight-medium" :class="titleColor">
        ETL Portal
      </v-app-bar-title>
      
      <v-spacer />
      
      <v-switch
        v-model="isDarkMode"
        hide-details
        inset
        density="compact"
        color="purple"
        class="mt-1 mr-3"
        true-icon="mdi-weather-night"
        false-icon="mdi-weather-sunny"
        @update:model-value="toggleTheme"
      />
    </v-app-bar>

    <v-main>
      <v-container fluid class="fill-height">
        <router-view v-if="!route.meta.requiresAuth" />
      </v-container>
    </v-main>
  </v-layout>
</template>

<script setup>
import { computed } from 'vue';
import { useTheme } from 'vuetify';
import { useRoute } from 'vue-router';

const theme = useTheme();
const route = useRoute();

const isDarkTheme = computed(() => theme.global.current.value.dark);
const isDarkMode = computed({
  get: () => isDarkTheme.value,
  set: () => {} // Toggle is handled by toggleTheme function
});

// Compute app bar color based on theme - matching AuthenticatedLayout
const appBarColor = computed(() => 
  isDarkTheme.value ? 'grey-darken-3' : 'primary'
);

// Ensure text is visible in both themes
const titleColor = computed(() => 
  isDarkTheme.value ? 'text-white' : 'text-white'
);

function toggleTheme() {
  theme.global.name.value = isDarkTheme.value ? 'light' : 'dark';
}
</script>

<style>
/* Override global app bar styles for guest layout - needs to be unscoped to override App.vue */
.v-theme--light .v-layout .v-app-bar {
  background-color: rgb(var(--v-theme-primary)) !important;
}

.v-theme--dark .v-layout .v-app-bar {
  background-color: #424242 !important;
}

/* Ensure app bar title is visible in both themes */
.v-layout .v-app-bar .v-app-bar-title {
  color: white !important;
}

/* Make sun icon orange/yellow in light mode */
.v-theme--light .v-layout .v-app-bar .v-switch .v-icon {
  color: #FFA726 !important;
  opacity: 1 !important;
}
</style>