<template>
  <div>
    <!-- Success State -->
    <v-alert v-if="isValid" type="success" variant="tonal" prominent>
      <v-alert-title>
        <v-icon start>mdi-check-circle</v-icon>
        {{ $t('pipeline.allFieldMappingsValid') }}
      </v-alert-title>
      {{ $t('pipeline.allRequiredFieldsMapped') }}
    </v-alert>

    <!-- Error State - Separate Cards Like Data Source Page -->
    <div v-else>
      <!-- Unique Identifier Validation -->
      <v-card v-if="uniqueIdentifierErrors.length > 0" variant="outlined" color="error" class="mb-3">
        <v-card-text class="py-3">
          <div class="d-flex align-start">
            <v-icon color="error" class="mr-3 mt-1">mdi-key-alert</v-icon>
            <div class="flex-grow-1">
              <div class="text-subtitle-2 mb-1">{{ $t('pipeline.uniqueIdentifierMappingRequired') }}</div>
              <div v-for="(error, index) in uniqueIdentifierErrors" :key="index" class="text-caption mb-1">
                • {{ error }}
              </div>
            </div>
          </div>
        </v-card-text>
      </v-card>

      <!-- Other Validation Errors -->
      <v-card v-if="otherErrors.length > 0" variant="outlined" color="error" class="mb-3">
        <v-card-text class="py-3">
          <div class="d-flex align-start">
            <v-icon color="error" class="mr-3 mt-1">mdi-alert</v-icon>
            <div class="flex-grow-1">
              <div class="text-subtitle-2 mb-2">{{ $t('pipeline.mappingIssues') }}</div>
              <div v-for="(error, index) in otherErrors" :key="index" class="text-caption mb-1">
                • {{ error }}
              </div>
            </div>
          </div>
        </v-card-text>
      </v-card>
    </div>
  </div>
</template>

<script setup>
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';

const { t } = useI18n();

const props = defineProps({
  validationErrors: {
    type: Array,
    default: () => []
  },
  unmappedRequiredFields: {
    type: Array,
    default: () => []
  },
  isValid: {
    type: Boolean,
    default: false
  }
});

// Separate unique identifier errors from other errors
const uniqueIdentifierErrors = computed(() => {
  return props.validationErrors.filter(error => 
    error.includes('unique identifier') || 
    error.includes('Unique identifier')
  );
});

const otherErrors = computed(() => {
  return props.validationErrors.filter(error => 
    !error.includes('unique identifier') && 
    !error.includes('Unique identifier')
  );
});


</script>

<style scoped>
ul {
  list-style-type: disc;
}

li {
  color: rgba(0, 0, 0, 0.87);
}

.v-theme--dark li {
  color: rgba(255, 255, 255, 0.87);
}
</style>
