<template>
  <div>
    <div class="d-flex align-center justify-space-between mb-3">
      <span class="text-subtitle-2">{{ $t('transformation.mappings') }}</span>
      <v-btn size="small" prepend-icon="mdi-plus" @click="addMapping">
        {{ $t('common.add') }}
      </v-btn>
    </div>

    <div v-for="(mapping, index) in localMappings" :key="index" class="mapping-row mb-2">
      <v-row dense>
        <v-col cols="5">
          <v-text-field
            v-model="mapping.from"
            :label="$t('transformation.from')"
            variant="outlined"
            density="compact"
            hide-details
          />
        </v-col>
        <v-col cols="5">
          <v-text-field
            v-model="mapping.to"
            :label="$t('transformation.to')"
            variant="outlined"
            density="compact"
            hide-details
          />
        </v-col>
        <v-col cols="2" class="d-flex align-center">
          <v-btn icon size="small" variant="text" color="error" @click="removeMapping(index)">
            <v-icon>mdi-delete</v-icon>
          </v-btn>
        </v-col>
      </v-row>
    </div>

    <v-text-field
      :model-value="modelValue.defaultValue"
      :label="$t('transformation.defaultValue')"
      variant="outlined"
      density="comfortable"
      class="mt-3"
      hint="Value to use when no mapping matches"
      persistent-hint
      @update:model-value="updateConfig('defaultValue', $event)"
    />
  </div>
</template>

<script setup>
import { ref, watch } from 'vue';

const props = defineProps({
  modelValue: {
    type: Object,
    default: () => ({ mappings: [], defaultValue: '' })
  }
});

const emit = defineEmits(['update:modelValue']);

const localMappings = ref(props.modelValue.mappings || []);

watch(localMappings, () => {
  emit('update:modelValue', { ...props.modelValue, mappings: localMappings.value });
}, { deep: true });

function addMapping() {
  localMappings.value.push({ from: '', to: '' });
}

function removeMapping(index) {
  localMappings.value.splice(index, 1);
}

function updateConfig(key, value) {
  emit('update:modelValue', { ...props.modelValue, [key]: value });
}
</script>
