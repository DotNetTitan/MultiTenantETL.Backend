<template>
  <div>
    <v-alert type="info" variant="tonal" density="compact">
      {{ $t('transformation.trimDescription') }}
    </v-alert>
  </div>
</template>

<script setup>
defineProps({
  modelValue: {
    type: Object,
    default: () => ({})
  }
});
</script>
