<template>
  <div>
    <v-select
      :model-value="modelValue.scriptLanguage"
      :label="$t('transformation.scriptLanguage')"
      :items="languageOptions"
      variant="outlined"
      density="comfortable"
      class="mb-3"
      @update:model-value="updateConfig('scriptLanguage', $event)"
    />
    
    <v-textarea
      :model-value="modelValue.script"
      :label="$t('transformation.script')"
      variant="outlined"
      rows="10"
      class="code-textarea"
      @update:model-value="updateConfig('script', $event)"
    />

    <v-alert type="info" variant="tonal" density="compact" class="mt-3">
      {{ $t('transformation.scriptHint') }}
    </v-alert>
  </div>
</template>

<script setup>
import { useI18n } from 'vue-i18n';

const { t } = useI18n();

const props = defineProps({
  modelValue: {
    type: Object,
    default: () => ({ scriptLanguage: 'javascript', script: '' })
  }
});

const emit = defineEmits(['update:modelValue']);

const languageOptions = [
  { title: 'JavaScript', value: 'javascript' },
  { title: 'C#', value: 'csharp' }
];

function updateConfig(key, value) {
  emit('update:modelValue', { ...props.modelValue, [key]: value });
}
</script>

<style scoped>
.code-textarea :deep(textarea) {
  font-family: 'Fira Code', 'Courier New', monospace;
  font-size: 14px;
  line-height: 1.5;
}
</style>
