<template>
  <v-card variant="outlined">
    <v-card-title class="d-flex align-center">
      <span class="text-subtitle-1">{{ $t('pipeline.mapping', { number: index + 1 }) }}</span>
      <v-spacer />
      <v-btn
        icon
        variant="text"
        size="small"
        :disabled="!canMoveUp"
        @click="$emit('move-up')"
      >
        <v-icon>mdi-arrow-up</v-icon>
      </v-btn>
      <v-btn
        icon
        variant="text"
        size="small"
        :disabled="!canMoveDown"
        @click="$emit('move-down')"
      >
        <v-icon>mdi-arrow-down</v-icon>
      </v-btn>
      <v-btn
        icon
        variant="text"
        size="small"
        color="error"
        @click="$emit('remove')"
      >
        <v-icon>mdi-close</v-icon>
      </v-btn>
    </v-card-title>

    <v-card-text>
      <v-row>
        <!-- Source Fields -->
        <v-col cols="12" md="5">
          <v-select
            v-model="localMapping.sourceFields"
            :items="sourceFieldItems"
            :label="$t('pipeline.sourceFields')"
            multiple
            chips
            closable-chips
          >
            <template #chip="{ item, props }">
              <v-chip v-bind="props" size="small">
                {{ item.title }}
              </v-chip>
            </template>
            <template #item="{ item, props }">
              <v-list-item v-bind="props">
                <template #title>
                  {{ item.title }}
                </template>
                <template #subtitle>
                  <v-chip size="x-small" class="mt-1">{{ item.raw.type }}</v-chip>
                </template>
              </v-list-item>
            </template>
            <template #append-item>
              <v-divider class="mt-2" />
              <v-list-item>
                <v-list-item-subtitle class="text-caption">
                  {{ $t('pipeline.selectMultipleFields') }}
                </v-list-item-subtitle>
              </v-list-item>
            </template>
          </v-select>
        </v-col>

        <!-- Arrow -->
        <v-col cols="12" md="2" class="d-flex align-center justify-center">
          <v-icon size="large" color="primary">mdi-arrow-right</v-icon>
        </v-col>

        <!-- Destination Field -->
        <v-col cols="12" md="5">
          <v-select
            v-model="localMapping.destinationField"
            :items="destinationFieldItems"
            :label="$t('pipeline.destinationField')"
          >
            <template #item="{ item, props }">
              <v-list-item v-bind="props">
                <template #title>
                  {{ item.title }}
                  <v-chip
                    v-if="item.raw.required"
                    size="x-small"
                    color="error"
                    class="ml-2"
                  >
                    {{ $t('common.required') }}
                  </v-chip>
                </template>
                <template #subtitle>
                  <v-chip size="x-small" class="mt-1">{{ item.raw.type }}</v-chip>
                </template>
              </v-list-item>
            </template>
          </v-select>
        </v-col>

        <!-- Transformations Chain -->
        <v-col cols="12">
          <v-expansion-panels>
            <v-expansion-panel>
              <v-expansion-panel-title>
                <div class="d-flex align-center">
                  <v-icon class="mr-2">mdi-vector-polyline</v-icon>
                  <span>{{ $t('pipeline.transformationsOptional') }}</span>
                  <v-chip
                    v-if="localMapping.transformations && localMapping.transformations.length > 0"
                    size="small"
                    class="ml-2"
                    color="primary"
                  >
                    {{ localMapping.transformations.length }}
                  </v-chip>
                </div>
              </v-expansion-panel-title>
              <v-expansion-panel-text>
                <TransformationChainEditor
                  v-model="localMapping.transformations"
                  :source-fields="localMapping.sourceFields"
                />
              </v-expansion-panel-text>
            </v-expansion-panel>
          </v-expansion-panels>
        </v-col>
      </v-row>
    </v-card-text>
  </v-card>
</template>

<script setup>
import { ref, computed, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import TransformationChainEditor from './TransformationChainEditor.vue';

const { t } = useI18n();

const props = defineProps({
  mapping: {
    type: Object,
    required: true
  },
  sourceFields: {
    type: Array,
    required: true
  },
  destinationFields: {
    type: Array,
    required: true
  },
  index: {
    type: Number,
    required: true
  },
  canMoveUp: {
    type: Boolean,
    default: false
  },
  canMoveDown: {
    type: Boolean,
    default: false
  }
});

const emit = defineEmits(['update:mapping', 'remove', 'move-up', 'move-down']);

// Use computed with getter/setter for proper v-model behavior
const localMapping = computed({
  get: () => props.mapping,
  set: (value) => {
    emit('update:mapping', value);
  }
});

// Computed properties
const sourceFieldItems = computed(() => {
  if (!props.sourceFields || !Array.isArray(props.sourceFields)) {
    return [];
  }
  return props.sourceFields.map(field => ({
    title: field.name,
    value: field.name,
    type: field.type,
    raw: field
  }));
});

const destinationFieldItems = computed(() => {
  if (!props.destinationFields || !Array.isArray(props.destinationFields)) {
    return [];
  }
  return props.destinationFields.map(field => ({
    title: field.name,
    value: field.name,
    type: field.type,
    required: field.required,
    raw: field
  }));
});
</script>

<style scoped>
.v-card-title {
  padding: 8px 16px;
}

.transformations-chain {
  border: 1px dashed rgba(var(--v-theme-primary), 0.3);
  border-radius: 4px;
  padding: 8px;
  background: rgba(var(--v-theme-surface-variant), 0.3);
}
</style>
