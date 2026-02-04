<template>
  <div>
    <v-text-field
      :model-value="modelValue.startIndex"
      :label="$t('transformation.startIndex')"
      type="number"
      variant="outlined"
      density="comfortable"
      class="mb-3"
      @update:model-value="updateConfig('startIndex', parseInt($event))"
    />
    <v-text-field
      :model-value="modelValue.length"
      :label="$t('transformation.length')"
      type="number"
      variant="outlined"
      density="comfortable"
      hint="Leave empty to extract to end"
      persistent-hint
      @update:model-value="updateConfig('length', $event ? parseInt($event) : null)"
    />
  </div>
</template>

<script setup>
const props = defineProps({
  modelValue: {
    type: Object,
    default: () => ({ startIndex: 0, length: null })
  }
});

const emit = defineEmits(['update:modelValue']);

function updateConfig(key, value) {
  emit('update:modelValue', { ...props.modelValue, [key]: value });
}
</script>
