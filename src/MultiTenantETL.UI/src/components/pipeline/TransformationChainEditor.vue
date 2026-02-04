<template>
  <div class="transformation-chain-editor">
    <div class="d-flex align-center justify-space-between mb-3">
      <div>
        <h4 class="text-subtitle-1">{{ $t('pipeline.transformations') }}</h4>
        <p class="text-caption text-grey">{{ $t('pipeline.transformationsAppliedInOrder') }}</p>
      </div>
      <v-btn
        size="small"
        color="primary"
        prepend-icon="mdi-plus"
        @click="addTransformation"
      >
        {{ $t('pipeline.addTransformation') }}
      </v-btn>
    </div>

    <div v-if="localTransformations.length === 0" class="text-center py-6">
      <v-icon size="48" color="grey-lighten-2">mdi-vector-polyline</v-icon>
      <p class="text-caption text-grey mt-2">{{ $t('pipeline.noTransformations') }}</p>
    </div>

    <v-list v-else class="pa-0">
      <v-list-item
        v-for="(transformation, index) in localTransformations"
        :key="transformation.id"
        class="transformation-item mb-2"
        :class="{ 'disabled': !transformation.isEnabled }"
      >
        <template #prepend>
          <v-avatar :color="getTypeColor(transformation.type)" size="32">
            <v-icon size="18" color="white">{{ getTypeIcon(transformation.type) }}</v-icon>
          </v-avatar>
        </template>

        <v-list-item-title>
          <div class="d-flex align-center">
            <span class="text-body-2 font-weight-medium">{{ transformation.type }}</span>
            <v-chip size="x-small" class="ml-2" variant="outlined">
              {{ $t('common.order') }}: {{ transformation.order }}
            </v-chip>
          </div>
        </v-list-item-title>

        <v-list-item-subtitle>
          <span class="text-caption">{{ getTransformationSummary(transformation) }}</span>
        </v-list-item-subtitle>

        <template #append>
          <div class="d-flex align-center">
            <v-btn
              icon
              size="x-small"
              variant="text"
              :disabled="index === 0"
              @click="moveUp(index)"
            >
              <v-icon>mdi-arrow-up</v-icon>
            </v-btn>
            <v-btn
              icon
              size="x-small"
              variant="text"
              :disabled="index === localTransformations.length - 1"
              @click="moveDown(index)"
            >
              <v-icon>mdi-arrow-down</v-icon>
            </v-btn>
            <v-btn
              icon
              size="x-small"
              variant="text"
              @click="editTransformation(index)"
            >
              <v-icon>mdi-pencil</v-icon>
            </v-btn>
            <v-btn
              icon
              size="x-small"
              variant="text"
              color="error"
              @click="removeTransformation(index)"
            >
              <v-icon>mdi-delete</v-icon>
            </v-btn>
          </div>
        </template>
      </v-list-item>
    </v-list>

    <!-- Transformation Dialog -->
    <v-dialog v-model="showDialog" max-width="800" persistent>
      <v-card>
        <v-card-title class="d-flex align-center">
          {{ editingIndex >= 0 ? $t('pipeline.editTransformation') : $t('pipeline.addTransformation') }}
          <v-spacer />
          <v-btn icon variant="text" @click="closeDialog">
            <v-icon>mdi-close</v-icon>
          </v-btn>
        </v-card-title>

        <v-card-text v-if="editingTransformation">
          <v-select
            v-model="editingTransformation.type"
            :label="$t('transformation.type')"
            :items="transformationTypes"
            item-title="label"
            item-value="value"
            variant="outlined"
            density="comfortable"
            class="mb-4"
          />

          <!-- Type-specific configuration -->
          <component
            :is="getConfigComponent(editingTransformation.type)"
            v-model="editingTransformation.config"
            :source-fields="sourceFields"
          />
        </v-card-text>

        <v-card-actions>
          <v-spacer />
          <v-btn @click="closeDialog">{{ $t('common.cancel') }}</v-btn>
          <v-btn color="primary" @click="saveTransformation">{{ $t('common.save') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup>
import { ref, computed, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useTranslatedMetadata } from '@/composables/useTranslatedMetadata';
import TrimConfig from './configs/TrimConfig.vue';
import CaseConvertConfig from './configs/CaseConvertConfig.vue';
import SubstringConfig from './configs/SubstringConfig.vue';
import ReplaceConfig from './configs/ReplaceConfig.vue';
import FilterConfig from './configs/FilterConfig.vue';
import MapConfig from './configs/MapConfig.vue';
import ScriptConfig from './configs/ScriptConfig.vue';

const { t } = useI18n();
const { getTranslatedOptions } = useTranslatedMetadata();

const props = defineProps({
  modelValue: {
    type: Array,
    default: () => []
  },
  sourceFields: {
    type: Array,
    default: () => []
  }
});

const emit = defineEmits(['update:modelValue']);

const localTransformations = computed({
  get: () => props.modelValue || [],
  set: (value) => {
    emit('update:modelValue', value);
  }
});

const showDialog = ref(false);
const editingTransformation = ref(null);
const editingIndex = ref(-1);

const transformationTypes = computed(() => {
  const types = getTranslatedOptions('transformationTypes');
  return types.map(type => ({
    label: type.label,
    value: type.value
  }));
});

function addTransformation() {
  editingTransformation.value = {
    id: `trans-${Date.now()}-${Math.random()}`,
    type: 'Trim',
    config: {},
    order: localTransformations.value.length + 1,
    isEnabled: true
  };
  editingIndex.value = -1;
  showDialog.value = true;
}

function editTransformation(index) {
  editingTransformation.value = { ...localTransformations.value[index] };
  editingIndex.value = index;
  showDialog.value = true;
}

function saveTransformation() {
  const newTransformations = [...localTransformations.value];
  if (editingIndex.value >= 0) {
    newTransformations[editingIndex.value] = editingTransformation.value;
  } else {
    newTransformations.push(editingTransformation.value);
  }
  updateOrders(newTransformations);
  localTransformations.value = newTransformations;
  closeDialog();
}

function removeTransformation(index) {
  const newTransformations = [...localTransformations.value];
  newTransformations.splice(index, 1);
  updateOrders(newTransformations);
  localTransformations.value = newTransformations;
}

function moveUp(index) {
  if (index > 0) {
    const newTransformations = [...localTransformations.value];
    const temp = newTransformations[index];
    newTransformations[index] = newTransformations[index - 1];
    newTransformations[index - 1] = temp;
    updateOrders(newTransformations);
    localTransformations.value = newTransformations;
  }
}

function moveDown(index) {
  if (index < localTransformations.value.length - 1) {
    const newTransformations = [...localTransformations.value];
    const temp = newTransformations[index];
    newTransformations[index] = newTransformations[index + 1];
    newTransformations[index + 1] = temp;
    updateOrders(newTransformations);
    localTransformations.value = newTransformations;
  }
}

function updateOrders(transformations) {
  transformations.forEach((t, i) => {
    t.order = i + 1;
  });
}

function closeDialog() {
  showDialog.value = false;
  editingTransformation.value = null;
  editingIndex.value = -1;
}

function getTypeColor(type) {
  const colors = {
    Filter: 'blue',
    Map: 'green',
    Trim: 'orange',
    CaseConvert: 'purple',
    Substring: 'teal',
    Replace: 'pink',
    Script: 'amber'
  };
  return colors[type] || 'grey';
}

function getTypeIcon(type) {
  const icons = {
    Filter: 'mdi-filter',
    Map: 'mdi-map',
    Trim: 'mdi-content-cut',
    CaseConvert: 'mdi-format-letter-case',
    Substring: 'mdi-format-text',
    Replace: 'mdi-find-replace',
    Script: 'mdi-code-braces'
  };
  return icons[type] || 'mdi-cog';
}

function getTransformationSummary(transformation) {
  switch (transformation.type) {
    case 'Trim':
      return t('transformation.trimWhitespace');
    case 'CaseConvert':
      return `${t('transformation.convertTo')} ${transformation.config.caseType || 'uppercase'}`;
    case 'Substring':
      return `${t('transformation.extract')} ${transformation.config.startIndex || 0} - ${transformation.config.length || 'end'}`;
    case 'Replace':
      return `${t('transformation.replace')} "${transformation.config.findPattern}" → "${transformation.config.replaceWith}"`;
    case 'Filter':
      return `${transformation.config.operator || ''} ${transformation.config.value || ''}`;
    case 'Map':
      return `${transformation.config.mappings?.length || 0} ${t('transformation.mappings')}`;
    case 'Script':
      return transformation.config.scriptLanguage || 'JavaScript';
    default:
      return '';
  }
}

function getConfigComponent(type) {
  const components = {
    Trim: TrimConfig,
    CaseConvert: CaseConvertConfig,
    Substring: SubstringConfig,
    Replace: ReplaceConfig,
    Filter: FilterConfig,
    Map: MapConfig,
    Script: ScriptConfig
  };
  return components[type] || 'div';
}
</script>

<style scoped>
.transformation-chain-editor {
  width: 100%;
}

.transformation-item {
  border: 1px solid rgba(0, 0, 0, 0.12);
  border-radius: 8px;
  background: rgba(0, 0, 0, 0.02);
}

.v-theme--dark .transformation-item {
  border-color: rgba(255, 255, 255, 0.12);
  background: rgba(255, 255, 255, 0.02);
}

.transformation-item.disabled {
  opacity: 0.5;
}
</style>
