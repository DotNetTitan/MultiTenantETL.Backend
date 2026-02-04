<template>
  <v-card variant="outlined" class="schema-preview">
    <v-card-text>
      <div class="d-flex align-center mb-3">
        <v-icon color="primary" class="mr-2">mdi-table-eye</v-icon>
        <h4 class="text-subtitle-1">Schema Overview</h4>
      </div>

      <!-- Statistics -->
      <v-row class="mb-3">
        <v-col cols="6" sm="3">
          <div class="stat-card">
            <div class="stat-value">{{ totalFields }}</div>
            <div class="stat-label">Total Fields</div>
          </div>
        </v-col>
        <v-col cols="6" sm="3">
          <div class="stat-card">
            <div class="stat-value text-error">{{ requiredFields }}</div>
            <div class="stat-label">Required</div>
          </div>
        </v-col>
        <v-col cols="6" sm="3">
          <div class="stat-card">
            <div class="stat-value text-success">{{ nullableFields }}</div>
            <div class="stat-label">Nullable</div>
          </div>
        </v-col>
        <v-col cols="6" sm="3">
          <div class="stat-card">
            <div class="stat-value text-info">{{ uniqueTypes }}</div>
            <div class="stat-label">Data Types</div>
          </div>
        </v-col>
      </v-row>

      <!-- Field List Preview -->
      <div v-if="fields.length > 0" class="field-list">
        <div class="text-caption text-grey mb-2">Fields:</div>
        <v-chip-group column>
          <v-chip
            v-for="field in fields"
            :key="field.id"
            size="small"
            :prepend-icon="getTypeIcon(field.type)"
            :color="field.required ? 'error' : 'default'"
            :variant="field.required ? 'tonal' : 'outlined'"
          >
            <span class="font-weight-medium">{{ field.name }}</span>
            <span class="text-grey ml-1">({{ getTypeLabel(field.type) }})</span>
            <v-icon v-if="field.required" size="x-small" class="ml-1">mdi-asterisk</v-icon>
          </v-chip>
        </v-chip-group>
      </div>

      <!-- Empty State -->
      <div v-else class="text-center py-4">
        <v-icon size="48" color="grey-lighten-2">mdi-table-off</v-icon>
        <p class="text-caption text-grey mt-2">No schema defined</p>
      </div>

      <!-- Type Distribution -->
      <div v-if="fields.length > 0" class="mt-4">
        <div class="text-caption text-grey mb-2">Type Distribution:</div>
        <div class="type-distribution">
          <v-chip
            v-for="(count, type) in typeDistribution"
            :key="type"
            size="small"
            variant="tonal"
            :prepend-icon="getTypeIcon(type)"
            class="mr-2 mb-2"
          >
            {{ getTypeLabel(type) }}: {{ count }}
          </v-chip>
        </div>
      </div>
    </v-card-text>
  </v-card>
</template>

<script setup>
import { computed } from 'vue';

const props = defineProps({
  fields: {
    type: Array,
    default: () => []
  }
});

// Data types mapping
const DATA_TYPES = [
  { value: 'varchar', label: 'String', icon: 'mdi-text' },
  { value: 'int', label: 'Integer', icon: 'mdi-numeric' },
  { value: 'bigint', label: 'Big Integer', icon: 'mdi-numeric' },
  { value: 'decimal', label: 'Decimal', icon: 'mdi-decimal' },
  { value: 'boolean', label: 'Boolean', icon: 'mdi-checkbox-marked' },
  { value: 'date', label: 'Date', icon: 'mdi-calendar' },
  { value: 'datetime', label: 'Date Time', icon: 'mdi-calendar-clock' },
  { value: 'timestamp', label: 'Timestamp', icon: 'mdi-clock' },
  { value: 'json', label: 'JSON', icon: 'mdi-code-json' },
  { value: 'text', label: 'Text (Long)', icon: 'mdi-text-long' }
];

// Computed statistics
const totalFields = computed(() => props.fields.length);

const requiredFields = computed(() => {
  return props.fields.filter(f => f.required).length;
});

const nullableFields = computed(() => {
  return props.fields.filter(f => f.nullable).length;
});

const uniqueTypes = computed(() => {
  const types = new Set(props.fields.map(f => f.type));
  return types.size;
});

const typeDistribution = computed(() => {
  const distribution = {};
  props.fields.forEach(field => {
    const type = field.type;
    distribution[type] = (distribution[type] || 0) + 1;
  });
  return distribution;
});

// Helper functions
function getTypeLabel(type) {
  const dataType = DATA_TYPES.find(dt => dt.value === type);
  return dataType ? dataType.label : type;
}

function getTypeIcon(type) {
  const dataType = DATA_TYPES.find(dt => dt.value === type);
  return dataType ? dataType.icon : 'mdi-help-circle';
}
</script>

<style scoped>
.schema-preview {
  border-radius: 8px;
}

.stat-card {
  text-align: center;
  padding: 8px;
  border-radius: 8px;
  background-color: rgba(var(--v-theme-surface-variant), 0.3);
}

.stat-value {
  font-size: 1.5rem;
  font-weight: 600;
  line-height: 1.2;
}

.stat-label {
  font-size: 0.75rem;
  color: rgb(var(--v-theme-on-surface-variant));
  margin-top: 4px;
}

.field-list {
  max-height: 200px;
  overflow-y: auto;
}

.type-distribution {
  display: flex;
  flex-wrap: wrap;
}
</style>
