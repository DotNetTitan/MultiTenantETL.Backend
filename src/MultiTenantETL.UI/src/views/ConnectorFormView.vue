<template>
  <div class="connector-form-view">
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
        <h1 class="text-h4">{{ isEdit ? t('connectors.editConnector') : t('connectors.createNewConnector') }}</h1>
        <p class="text-caption text-grey">{{ isEdit ? t('connectors.updateConnectorConfiguration') : t('connectors.configureNewConnector') }}</p>
      </div>
    </div>

    <!-- Loading State -->
    <div v-if="loading" class="text-center py-12">
      <v-progress-circular indeterminate color="primary" size="64" />
      <p class="mt-4">{{ t('connectors.loadingConnector') }}</p>
    </div>

    <!-- Error State -->
    <v-alert v-else-if="error" type="error" class="mb-4">
      {{ error }}
      <v-btn variant="text" @click="loadConnector">{{ t('common.retry') }}</v-btn>
    </v-alert>

    <!-- Wizard -->
    <ConnectorWizard
      v-else
      :connector="connector"
      :connectors="[]"
      @save="handleSave"
      @close="handleCancel"
    />
  </div>
</template>

<script setup>
import { ref, onMounted, computed } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import { useI18n } from 'vue-i18n';
import ConnectorWizard from '@/components/connector/ConnectorWizard.vue';
import { fetchConnectorById, createConnector, updateConnector } from '@/services/connectorService';

const { t } = useI18n();

const router = useRouter();
const route = useRoute();

const loading = ref(false);
const error = ref(null);
const connector = ref({
  id: null,
  name: '',
  description: '',
  type: '',
  provider: '',
  direction: 'source',
  config: {},
  schema: { fields: [] }
});

const isEdit = computed(() => !!route.params.id);

onMounted(() => {
  if (isEdit.value) {
    loadConnector();
  }
});

async function loadConnector() {
  try {
    loading.value = true;
    error.value = null;
    const data = await fetchConnectorById(route.params.id);
    
    // Ensure schema property exists
    const loadedConnector = {
      ...data,
      schema: data.schema || { fields: [] }
    };
    
    // Ensure config object exists
    if (!loadedConnector.config) {
      loadedConnector.config = {};
    }
    
    // Ensure writeConfig exists for destination/both connectors
    if (loadedConnector.direction === 'destination' || loadedConnector.direction === 'both') {
      if (!loadedConnector.config.writeConfig) {
        // Initialize writeConfig based on connector type
        if (loadedConnector.type === 'Database') {
          loadedConnector.config.writeConfig = {
            tableName: '',
            operation: 'INSERT',
            primaryKeys: [],
            batchSize: 1000
          };
        } else if (loadedConnector.type === 'File') {
          loadedConnector.config.writeConfig = {
            writeMode: 'OVERWRITE',
            includeHeaders: true,
            columnOrder: [],
            filenamePattern: '',
            sheetName: 'Sheet1',
            startCell: 'A1',
            structure: 'ARRAY',
            rootKey: null
          };
        } else if (loadedConnector.type === 'API') {
          loadedConnector.config.writeConfig = {
            requestFormat: 'JSON',
            wrapInArray: false,
            rootKey: null,
            batchSize: 100
          };
        }
      }
    }
    
    connector.value = loadedConnector;
  } catch (err) {
    error.value = `Failed to load connector: ${err.message}`;
  } finally {
    loading.value = false;
  }
}

async function handleSave(savedConnector) {
  try {
    if (savedConnector.id) {
      await updateConnector(savedConnector.id, savedConnector);
    } else {
      await createConnector(savedConnector);
    }
    router.push('/connectors');
  } catch (err) {
    error.value = `Failed to save connector: ${err.message}`;
  }
}

function handleCancel() {
  router.push('/connectors');
}
</script>

<style scoped>
.connector-form-view {
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
