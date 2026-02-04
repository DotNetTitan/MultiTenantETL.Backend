<template>
  <v-menu offset-y>
    <template #activator="{ props }">
      <v-btn
        v-bind="props"
        variant="text"
        class="language-btn"
        :ripple="false"
      >
        <v-icon size="20">mdi-translate</v-icon>
        <span class="language-code ml-1">{{ locale.toUpperCase() }}</span>
        <v-icon size="16" class="ml-1">mdi-chevron-down</v-icon>
      </v-btn>
    </template>

    <v-list density="compact" class="language-menu">
      <v-list-item
        v-for="loc in availableLocales"
        :key="loc.code"
        :active="locale === loc.code"
        :class="{ 'active-language': locale === loc.code }"
        @click="changeLocale(loc.code)"
      >
        <template #prepend>
          <span class="flag-icon-large">{{ loc.flag }}</span>
        </template>
        <v-list-item-title class="language-name">
          {{ loc.name }}
        </v-list-item-title>
        <template #append>
          <v-icon v-if="locale === loc.code" color="primary" size="small">
            mdi-check
          </v-icon>
        </template>
      </v-list-item>
    </v-list>
  </v-menu>
</template>

<script setup>
import { useLocale } from '@/composables/useLocale';

const { locale, availableLocales, changeLocale, currentLocaleFlag } = useLocale();
</script>

<style scoped>
.language-btn {
  display: flex;
  align-items: center;
  padding: 4px 12px !important;
  min-width: auto !important;
  height: 36px !important;
}

.language-code {
  font-size: 12px;
  font-weight: 600;
  letter-spacing: 0.5px;
  line-height: 1;
}

.language-menu {
  min-width: 180px;
}

.flag-icon-large {
  font-size: 24px;
  margin-right: 12px;
  line-height: 1;
}

.language-name {
  font-size: 14px;
  font-weight: 500;
}

.active-language {
  background: rgba(var(--v-theme-primary), 0.08);
}

/* Light mode specific */
.v-theme--light .language-btn {
  color: white !important;
}

.v-theme--light .language-btn :deep(.v-icon) {
  color: white !important;
}

.v-theme--light .language-btn:hover {
  background: rgba(255, 255, 255, 0.1) !important;
}

/* Dark mode specific */
.v-theme--dark .language-btn:hover {
  background: rgba(255, 255, 255, 0.05) !important;
}

/* Remove default button styles */
.language-btn :deep(.v-btn__overlay) {
  opacity: 0;
}
</style>
