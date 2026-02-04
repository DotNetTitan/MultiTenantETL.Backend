<template>
  <v-hover v-slot="{ isHovering, props }">
    <v-card 
      v-bind="props" 
      class="mb-4" 
      :elevation="isHovering ? 5 : 2"
      :class="{'on-hover': isHovering}"
      @click="$router.push(to)"
    >
      <v-card-item>
        <v-card-title class="d-flex align-center">
          <v-icon :icon="icon" size="x-large" class="mr-2" :color="color" />
          {{ title }}
        </v-card-title>
        <div class="text-h2 text-center my-3">
          {{ value }}
          <v-progress-circular v-if="loading" indeterminate size="24" width="2" class="ml-2" />
        </div>
      </v-card-item>
      <v-card-actions>
        <v-btn :variant="buttonVariant" :color="color" block :to="to">{{ buttonText }}</v-btn>
      </v-card-actions>
    </v-card>
  </v-hover>
</template>

<script setup>
defineProps({
  title: {
    type: String,
    required: true
  },
  value: {
    type: [Number, String],
    required: true
  },
  icon: {
    type: String,
    required: true
  },
  color: {
    type: String,
    default: 'primary'
  },
  loading: {
    type: Boolean,
    default: false
  },
  to: {
    type: String,
    required: true
  },
  buttonText: {
    type: String,
    required: true
  },
  buttonVariant: {
    type: String,
    default: 'tonal'
  }
});
</script>

<style scoped>
.on-hover {
  transition: all var(--app-transition-speed) ease-in-out;
  transform: var(--app-card-hover-transform);
}
</style>