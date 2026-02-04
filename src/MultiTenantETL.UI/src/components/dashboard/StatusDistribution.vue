<template>
  <v-card elevation="2" class="fill-height">
    <v-card-title class="d-flex align-center">
      <v-icon icon="mdi-chart-pie" class="mr-2" />
      Pipeline Status
    </v-card-title>
    <v-divider />
    <v-card-text>
      <div v-if="loading" class="text-center my-4">
        <v-progress-circular indeterminate size="32" width="3" />
      </div>
      <v-sheet v-else height="250" class="d-flex align-center justify-center">
        <div class="text-center w-100">
          <div class="text-subtitle-1 mb-4">Pipeline Status Distribution</div>
          <v-row>
            <v-col v-for="(status, index) in statusDistribution" :key="index">
              <v-hover v-slot="{ isHovering, props }">
                <v-sheet 
                  v-bind="props"
                  rounded 
                  class="pa-4 status-card" 
                  :color="getStatusColor(status.name)"
                  :class="{'on-hover': isHovering}"
                >
                  <div class="text-white">{{ status.name }}</div>
                  <div class="text-h4 text-white mt-2">{{ status.count }}</div>
                  <div class="text-caption text-white-lighten-2">{{ ((status.count / totalPipelines) * 100).toFixed(1) }}%</div>
                </v-sheet>
              </v-hover>
            </v-col>
          </v-row>
        </div>
      </v-sheet>
    </v-card-text>
  </v-card>
</template>

<script setup>
import { computed } from 'vue';

const props = defineProps({
  statusDistribution: {
    type: Array,
    required: true
  },
  loading: {
    type: Boolean,
    default: false
  }
});

const totalPipelines = computed(() => 
  props.statusDistribution.reduce((sum, status) => sum + status.count, 0)
);

function getStatusColor(status) {
  switch (status?.toLowerCase()) {
    case 'active':
      return 'success';
    case 'paused':
      return 'warning';
    case 'failed':
      return 'error';
    case 'pending':
      return 'info';
    default:
      return 'grey';
  }
}
</script>

<style scoped>
.status-card {
  transition: all var(--app-transition-speed) ease;
  text-align: center;
  border-radius: var(--app-border-radius);
}

.status-card.on-hover {
  transform: scale(1.05);
  box-shadow: 0 8px 16px rgba(var(--v-theme-on-surface), 0.15);
}

:deep(.v-theme--dark .status-card) {
  box-shadow: 0 4px 8px rgba(0, 0, 0, 0.3);
}

:deep(.v-theme--light .status-card) {
  box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1);
}
</style>