<template>
  <v-card variant="outlined" class="mb-4">
    <v-card-text>
      <div class="text-center py-4">
        <v-icon size="64" color="primary">mdi-api</v-icon>
        <h3 class="text-h6 mt-4 mb-2">{{ $t('schema.detectFromApi') }}</h3>
        <p class="text-body-2 text-medium-emphasis mb-4">
          {{ $t('schema.detectFromApiDescription') }}
        </p>

        <v-select
          v-if="availableEndpoints.length > 0"
          v-model="selectedEndpoint"
          :items="availableEndpoints"
          item-title="label"
          item-value="value"
          :label="$t('schema.selectEndpoint')"
          variant="outlined"
          density="compact"
          class="mb-4"
          style="max-width: 500px; margin: 0 auto;"
        />

        <v-alert
          v-if="availableEndpoints.length === 0"
          type="warning"
          variant="tonal"
          density="compact"
          class="mb-4"
        >
          {{ $t('schema.noGetEndpointsConfigured') }}
        </v-alert>

        <v-btn
          color="primary"
          prepend-icon="mdi-auto-fix"
          :loading="detecting"
          :disabled="availableEndpoints.length === 0 || !selectedEndpoint"
          @click="detectSchema"
        >
          {{ $t('schema.detectSchema') }}
        </v-btn>

        <v-alert
          v-if="error"
          type="error"
          variant="tonal"
          density="compact"
          class="mt-4"
        >
          {{ error }}
        </v-alert>
      </div>
    </v-card-text>
  </v-card>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue';
import { useI18n } from 'vue-i18n';
import { detectSchemaPreview } from '@/services/connectorService';

const { t } = useI18n();

const props = defineProps({
  connectorType: {
    type: String,
    required: true
  },
  provider: {
    type: String,
    required: true
  },
  config: {
    type: Object,
    required: true
  }
});

const emit = defineEmits(['schema-generated']);

const detecting = ref(false);
const error = ref('');
const selectedEndpoint = ref(null);

const availableEndpoints = computed(() => {
  if (!props.config?.endpoints || props.config.endpoints.length === 0) {
    return [];
  }

  // Only show GET endpoints for schema detection
  return props.config.endpoints
    .filter(e => e.method?.toUpperCase() === 'GET')
    .map(e => ({
      label: `${e.method} ${e.path}${e.name ? ` - ${e.name}` : ''}`,
      value: e.path
    }));
});

onMounted(() => {
  // Auto-select first GET endpoint if available
  if (availableEndpoints.value.length > 0) {
    selectedEndpoint.value = availableEndpoints.value[0].value;
  }
});

async function detectSchema() {
  if (!selectedEndpoint.value) return;

  detecting.value = true;
  error.value = '';

  try {
    const result = await detectSchemaPreview(
      props.connectorType,
      props.provider,
      props.config,
      selectedEndpoint.value
    );

    if (result.success && result.schema) {
      emit('schema-generated', result.schema);
    } else {
      error.value = result.message || t('schema.schemaDetectionFailed');
    }
  } catch (err) {
    console.error('Error detecting API schema:', err);
    error.value = err.response?.data?.message || err.message || t('schema.schemaDetectionFailed');
  } finally {
    detecting.value = false;
  }
}
</script>

<style scoped>
.schema-detector {
  width: 100%;
}
</style>
