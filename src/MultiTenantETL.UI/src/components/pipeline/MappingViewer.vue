<template>
  <div class="mapping-viewer">
    <v-card variant="outlined" class="mb-4 bg-surface-lighten-1">
      <v-card-text>
        <div class="d-flex justify-space-between align-center">
          <div class="d-flex align-center">
              <div class="d-flex flex-column align-center mr-4">
                <v-icon color="primary" icon="mdi-database" size="large" class="mb-1"></v-icon>
                <span class="text-caption font-weight-medium text-primary">SOURCE</span>
              </div>
              <div>
                <div class="text-h6 font-weight-medium">{{ sourceName }}</div>
              </div>
          </div>

          <div class="d-flex flex-column align-center px-4">
            <v-icon color="grey-lighten-1" size="x-large">mdi-arrow-right</v-icon>
            <div class="text-caption text-medium-emphasis mt-1">{{ mappings.length }} {{ mappings.length === 1 ? 'field' : 'fields' }} mapped</div>
          </div>

          <div class="d-flex align-center text-right">
            <div>
              <div class="text-h6 font-weight-medium">{{ destinationName }}</div>
            </div>
            <div class="d-flex flex-column align-center ml-4">
              <v-icon color="success" icon="mdi-database" size="large" class="mb-1"></v-icon>
              <span class="text-caption font-weight-medium text-success">TARGET</span>
            </div>
          </div>
        </div>
      </v-card-text>
    </v-card>

    <v-table hover density="comfortable" class="mapping-table">
      <thead>
        <tr>
          <th class="text-left" style="width: 50px">#</th>
          <th class="text-left" style="width: 40%">Source Field</th>
          <th class="text-center" style="width: 10%">Transform</th>
          <th class="text-left" style="width: 40%">Destination Field</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="(mapping, index) in mappings" :key="index">
          <td class="text-grey text-caption">{{ index + 1 }}</td>
          <td>
            <div class="d-flex align-center">
              <span class="font-weight-medium">{{ mapping.sourceField }}</span>
              <v-chip
                v-if="mapping.sourceFieldType"
                size="x-small"
                variant="tonal"
                color="primary"
                class="ml-2"
                label
              >
                {{ mapping.sourceFieldType }}
              </v-chip>
            </div>
          </td>
          <td class="text-center">
            <div v-if="mapping.transformation" class="d-flex justify-center">
                 <v-tooltip location="top">
                    <template v-slot:activator="{ props }">
                        <v-avatar
                            v-bind="props"
                            size="32"
                            :color="getTransformationColor(mapping.transformation.type)"
                            variant="tonal"
                        >
                            <v-icon size="small">{{ getTransformationIcon(mapping.transformation.type) }}</v-icon>
                        </v-avatar>
                    </template>
                    <div class="text-center">
                        <div class="font-weight-bold">{{ mapping.transformation.type }}</div>
                        <div class="text-caption">{{ mapping.transformation.description }}</div>
                    </div>
                 </v-tooltip>
            </div>
            <v-icon v-else color="grey-lighten-2" icon="mdi-arrow-right-thin"></v-icon>
          </td>
          <td>
            <div class="d-flex align-center">
              <span class="font-weight-medium">{{ mapping.destinationField }}</span>
              <v-chip
                v-if="mapping.destinationFieldType"
                size="x-small"
                variant="tonal"
                color="success"
                class="ml-2"
                label
              >
                {{ mapping.destinationFieldType }}
              </v-chip>
            </div>
          </td>
        </tr>
        <tr v-if="mappings.length === 0">
            <td colspan="4" class="text-center py-8 text-grey">
                No mappings defined
            </td>
        </tr>
      </tbody>
    </v-table>
  </div>
</template>

<script setup>
defineProps({
  mappings: {
    type: Array,
    required: true,
    default: () => []
  },
  sourceName: {
    type: String,
    default: 'Source'
  },
  destinationName: {
    type: String,
    default: 'Destination'
  }
});

function getTransformationColor(type) {
  const colors = {
    'Filter': 'blue',
    'Map': 'green',
    'Aggregation': 'orange',
    'Script': 'purple',
    'Join': 'teal',
    'Trim': 'cyan',
    'Case Convert': 'indigo',
    'Substring': 'pink',
    'Replace': 'amber',
    'Split': 'lime'
  };
  if (type && type.includes(', ')) {
    return 'primary';
  }
  return colors[type] || 'grey';
}

function getTransformationIcon(type) {
  const icons = {
    'Filter': 'mdi-filter',
    'Map': 'mdi-map',
    'Aggregation': 'mdi-chart-bar',
    'Script': 'mdi-code-braces',
    'Join': 'mdi-link-variant',
    'Trim': 'mdi-content-cut',
    'Case Convert': 'mdi-format-letter-case',
    'Substring': 'mdi-contain',
    'Replace': 'mdi-find-replace',
    'Split': 'mdi-call-split'
  };
  if (type && type.includes(', ')) {
    return 'mdi-vector-polyline';
  }
  return icons[type] || 'mdi-cog';
}
</script>

<style scoped>
.mapping-viewer {
  width: 100%;
}

.mapping-table {
    border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
    border-radius: 8px;
    overflow: hidden;
}

.mapping-table th {
    background-color: rgb(var(--v-theme-surface-variant));
    text-transform: uppercase;
    font-size: 0.7rem;
    font-weight: 600;
    letter-spacing: 0.05em;
    color: rgb(var(--v-theme-on-surface-variant));
}

.bg-surface-lighten-1 {
    background-color: rgba(var(--v-theme-surface), 0.5); /* Subtle background for header card */
}
</style>
