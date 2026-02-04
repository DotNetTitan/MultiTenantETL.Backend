<template>
  <v-dialog :model-value="modelValue" max-width="600" @update:model-value="$emit('update:modelValue', $event)">
    <v-card>
      <v-card-title class="d-flex align-center">
        <v-icon color="warning" class="mr-2">mdi-alert</v-icon>
        Schema Change Warning
      </v-card-title>

      <v-card-text>
        <v-alert type="warning" variant="tonal" class="mb-4">
          This data source is currently used in {{ affectedPipelines.length }} pipeline{{ affectedPipelines.length !== 1 ? 's' : '' }}.
          Changing the schema may affect these pipelines.
        </v-alert>

        <p class="mb-3">
          Modifying the schema definition may cause field mapping errors in the following pipelines:
        </p>

        <v-list density="compact" class="mb-4">
          <v-list-item
            v-for="pipeline in affectedPipelines"
            :key="pipeline.id"
            :prepend-icon="getStatusIcon(pipeline.status)"
          >
            <v-list-item-title>{{ pipeline.name }}</v-list-item-title>
            <v-list-item-subtitle>
              Used as {{ pipeline.usedAs }}
              <v-chip size="x-small" :color="getStatusColor(pipeline.status)" class="ml-2">
                {{ pipeline.status }}
              </v-chip>
            </v-list-item-subtitle>
          </v-list-item>
        </v-list>

        <v-alert type="info" variant="tonal" density="compact">
          <strong>Recommendation:</strong> After saving schema changes, review and update field mappings in affected pipelines.
        </v-alert>
      </v-card-text>

      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" @click="$emit('cancel')">
          Cancel
        </v-btn>
        <v-btn color="warning" @click="$emit('proceed')">
          Proceed Anyway
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup>
defineProps({
  modelValue: {
    type: Boolean,
    default: false
  },
  affectedPipelines: {
    type: Array,
    default: () => []
  }
});

defineEmits(['update:modelValue', 'cancel', 'proceed']);

function getStatusIcon(status) {
  switch (status) {
    case 'Running':
      return 'mdi-play-circle';
    case 'Failed':
      return 'mdi-alert-circle';
    case 'Completed':
      return 'mdi-check-circle';
    default:
      return 'mdi-pause-circle';
  }
}

function getStatusColor(status) {
  switch (status) {
    case 'Running':
      return 'info';
    case 'Failed':
      return 'error';
    case 'Completed':
      return 'success';
    default:
      return 'grey';
  }
}
</script>
