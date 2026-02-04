<template>
  <div>
    <v-select
      :model-value="modelValue.caseType"
      :label="$t('transformation.caseType')"
      :items="caseTypes"
      variant="outlined"
      density="comfortable"
      @update:model-value="updateConfig('caseType', $event)"
    />
  </div>
</template>

<script setup>
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';

const { t } = useI18n();

const props = defineProps({
  modelValue: {
    type: Object,
    default: () => ({ caseType: 'uppercase' })
  }
});

const emit = defineEmits(['update:modelValue']);

const caseTypes = computed(() => [
  { title: t('transformation.uppercase'), value: 'uppercase' },
  { title: t('transformation.lowercase'), value: 'lowercase' },
  { title: t('transformation.titlecase'), value: 'titlecase' },
  { title: t('transformation.camelcase'), value: 'camelcase' }
]);

function updateConfig(key, value) {
  emit('update:modelValue', { ...props.modelValue, [key]: value });
}
</script>
