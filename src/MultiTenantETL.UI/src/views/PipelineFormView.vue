<template>
  <div class="pipeline-form-view">
    <!-- Header -->
    <div class="page-header mb-4">
      <v-btn
        icon
        variant="text"
        class="mr-2"
        @click="handleCancel"
      >
        <v-icon>mdi-arrow-left</v-icon>
      </v-btn>
      <div>
        <h1 class="text-h4">{{ isEdit ? $t('pipelines.editPipeline') : $t('pipelines.createNewPipeline') }}</h1>
        <p class="text-caption text-grey">{{ isEdit ? $t('pipelines.updateConfiguration') : $t('pipelines.configureNewPipeline') }}</p>
      </div>
    </div>

    <!-- Loading State -->
    <div v-if="loading || loadingData" class="text-center py-12">
      <v-progress-circular indeterminate color="primary" size="64" />
      <p class="mt-4">Loading pipeline...</p>
    </div>

    <!-- Error State -->
    <v-alert v-else-if="error" type="error" class="mb-4">
      {{ error }}
      <v-btn variant="text" @click="loadPipeline">Retry</v-btn>
    </v-alert>

    <!-- Wizard -->
    <PipelineWizard
      v-else
      :pipeline="pipeline"
      :connectors="connectors"
      :timezones="timezones"
      @save="handleSave"
      @close="handleCancel"
    />
  </div>
</template>

<script setup>
import { ref, onMounted, computed } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import PipelineWizard from '@/components/pipeline/PipelineWizard.vue';
import { fetchPipelineById, savePipeline } from '@/services/pipelineService';

const router = useRouter();
const route = useRoute();

const loading = ref(false);
const loadingData = ref(true);
const error = ref(null);
const connectors = ref([]);
const timezones = ref([
  { name: 'UTC', value: 'UTC' },
  { name: 'Eastern Time (ET)', value: 'America/New_York' },
  { name: 'Central Time (CT)', value: 'America/Chicago' },
  { name: 'Mountain Time (MT)', value: 'America/Denver' },
  { name: 'Pacific Time (PT)', value: 'America/Los_Angeles' }
]);
const pipeline = ref({
  id: null,
  name: '',
  description: '',
  sourceId: null,
  destinationId: null,
  transformations: [],
  fieldMappings: [],
  isScheduled: false,
  isActive: true,
  schedule: {
    frequency: 'Daily',
    time: '00:00',
    timezone: 'UTC'
  }
});

const isEdit = computed(() => !!route.params.id);

onMounted(async () => {
  try {
    loadingData.value = true;
    await loadConnectors();
    if (isEdit.value) {
      await loadPipeline();
    }
  } finally {
    loadingData.value = false;
  }
});

async function loadConnectors() {
  try {
    const { fetchConnectors } = await import('@/services/connectorService');
    const response = await fetchConnectors({ pageSize: 100 }); // Get more connectors for selection
    connectors.value = response.connectors || [];
  } catch (err) {
    console.error('Error loading connectors:', err);
    error.value = `Failed to load connectors: ${err.message}`;
  }
}

async function loadPipeline() {
  try {
    loading.value = true;
    error.value = null;
    const data = await fetchPipelineById(route.params.id);
    
    // Map backend response to frontend format
    pipeline.value = {
      id: data.id,
      name: data.name,
      description: data.description,
      sourceId: data.sourceConnectorId,
      destinationId: data.destinationConnectorId,
      fieldMappings: data.fieldMappings || [],
      isScheduled: data.isScheduled,
      isActive: data.isActive ?? true,
      schedule: data.schedule || {
        frequency: 'Daily',
        time: '00:00',
        timezone: 'UTC'
      }
    };
  } catch (err) {
    error.value = `Failed to load pipeline: ${err.message}`;
  } finally {
    loading.value = false;
  }
}

async function handleSave(savedPipeline) {
  try {
    await savePipeline(savedPipeline);
    router.push('/pipelines');
  } catch (err) {
    error.value = `Failed to save pipeline: ${err.message}`;
  }
}

function handleCancel() {
  router.push('/pipelines');
}
</script>

<style scoped>
.pipeline-form-view {
  padding: 24px;
  background: rgb(var(--v-theme-background));
  min-height: 100vh;
}

.page-header {
  display: flex;
  align-items: center;
  margin-bottom: 24px;
}
</style>
