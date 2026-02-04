<template>
  <v-dialog
    :model-value="show"
    :max-width="width"
    @update:model-value="$emit('update:show', $event)"
  >
    <v-card>
      <v-card-title :class="titleClass">
        {{ title }}
      </v-card-title>
      <v-card-text class="pt-4">
        <slot>{{ message }}</slot>
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn
          variant="text"
          :disabled="loading"
          @click="$emit('update:show', false)"
        >
          {{ cancelText }}
        </v-btn>
        <v-btn
          :color="confirmColor"
          :loading="loading"
          @click="$emit('confirm')"
        >
          {{ confirmText }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup>
defineProps({
  show: {
    type: Boolean,
    required: true
  },
  title: {
    type: String,
    required: true
  },
  message: {
    type: String,
    default: ''
  },
  confirmText: {
    type: String,
    default: 'Confirm'
  },
  cancelText: {
    type: String,
    default: 'Cancel'
  },
  confirmColor: {
    type: String,
    default: 'primary'
  },
  titleClass: {
    type: String,
    default: 'text-h5'
  },
  width: {
    type: [Number, String],
    default: 400
  },
  loading: {
    type: Boolean,
    default: false
  }
});

defineEmits(['update:show', 'confirm']);
</script>