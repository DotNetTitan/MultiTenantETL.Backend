<template>
  <div>
    <v-select
      :model-value="modelValue.operator"
      :label="$t('transformation.operator')"
      :items="operators"
      variant="outlined"
      density="comfortable"
      class="mb-3"
      @update:model-value="updateConfig('operator', $event)"
    />
    <v-text-field
      v-if="!['isEmpty', 'isNotEmpty'].includes(modelValue.operator)"
      :model-value="modelValue.value"
      :label="$t('transformation.value')"
      variant="outlined"
      density="comfortable"
      @update:model-value="updateConfig('value', $event)"
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
    default: () => ({ operator: 'equals', value: '' })
  }
});

const emit = defineEmits(['update:modelValue']);

const operators = computed(() => [
  { title: t('transformation.equals'), value: 'equals' },
  { title: t('transformation.notEquals'), value: 'notEquals' },
  { title: t('transformation.contains'), value: 'contains' },
  { title: t('transformation.notContains'), value: 'notContains' },
  { title: t('transformation.startsWith'), value: 'startsWith' },
  { title: t('transformation.endsWith'), value: 'endsWith' },
  { title: t('transformation.greaterThan'), value: 'greaterThan' },
  { title: t('transformation.lessThan'), value: 'lessThan' },
  { title: t('transformation.isEmpty'), value: 'isEmpty' },
  { title: t('transformation.isNotEmpty'), value: 'isNotEmpty' }
]);

function updateConfig(key, value) {
  emit('update:modelValue', { ...props.modelValue, [key]: value });
}
</script>
