<template>
  <v-row>
    <!-- Source Schema -->
    <v-col cols="12" md="6">
      <v-card>
        <v-card-title>
          <v-icon start color="blue">mdi-database-export</v-icon>
          {{ $t('pipeline.sourceFields') }}
        </v-card-title>
        <v-card-text class="pa-0">
          <v-text-field
            v-model="sourceSearch"
            :label="$t('pipeline.searchSourceFields')"
            prepend-inner-icon="mdi-magnify"
            density="compact"
            hide-details
            clearable
            class="ma-2"
          />
          <div class="field-list-container">
            <v-list density="compact">
              <v-list-item
                v-for="field in filteredSourceFields"
                :key="field.name"
                :class="{ 'mapped-field': isSourceFieldMapped(field.name) }"
              >
                <template #prepend>
                  <v-icon v-if="isSourceFieldMapped(field.name)" color="success">
                    mdi-check-circle
                  </v-icon>
                  <v-icon v-else color="grey-lighten-1">
                    mdi-circle-outline
                  </v-icon>
                </template>
                <v-list-item-title>
                  {{ field.name }}
                  <v-chip
                    v-if="field.required"
                    size="x-small"
                    color="info"
                    variant="tonal"
                    class="ml-2"
                  >
                    {{ $t('common.required') }}
                  </v-chip>
                </v-list-item-title>
                <v-list-item-subtitle>
                  <v-chip size="x-small" class="mr-1">{{ field.type }}</v-chip>
                  <v-chip v-if="field.nullable" size="x-small" color="grey-lighten-2">
                    {{ $t('connectors.nullable') }}
                  </v-chip>
                </v-list-item-subtitle>
              </v-list-item>
              <v-list-item v-if="filteredSourceFields.length === 0">
                <v-list-item-title class="text-grey text-center">
                  {{ $t('pipeline.noFieldsFound') }}
                </v-list-item-title>
              </v-list-item>
            </v-list>
          </div>
        </v-card-text>
        <v-card-actions>
          <v-chip size="small" color="success">
            {{ $t('pipeline.mapped', { count: mappedSourceFieldsCount, total: sourceSchema.fields.length }) }}
          </v-chip>
        </v-card-actions>
      </v-card>
    </v-col>

    <!-- Destination Schema -->
    <v-col cols="12" md="6">
      <v-card>
        <v-card-title>
          <v-icon start color="green">mdi-database-import</v-icon>
          {{ $t('pipeline.destinationFields') }}
        </v-card-title>
        <v-card-text class="pa-0">
          <v-text-field
            v-model="destinationSearch"
            :label="$t('pipeline.searchDestinationFields')"
            prepend-inner-icon="mdi-magnify"
            density="compact"
            hide-details
            clearable
            class="ma-2"
          />
          <div class="field-list-container">
            <v-list density="compact">
              <v-list-item
                v-for="field in filteredDestinationFields"
                :key="field.name"
                :class="getDestinationFieldClass(field)"
              >
                <template #prepend>
                  <v-icon v-if="isDestinationFieldMapped(field.name)" color="success">
                    mdi-check-circle
                  </v-icon>
                  <v-icon v-else-if="field.required" color="error">
                    mdi-alert-circle
                  </v-icon>
                  <v-icon v-else color="grey-lighten-1">
                    mdi-circle-outline
                  </v-icon>
                </template>
                <v-list-item-title>
                  {{ field.name }}
                  <v-chip
                    v-if="field.required"
                    size="x-small"
                    color="error"
                    class="ml-2"
                  >
                    {{ $t('common.required') }}
                  </v-chip>
                </v-list-item-title>
                <v-list-item-subtitle>
                  <v-chip size="x-small" class="mr-1">{{ field.type }}</v-chip>
                  <v-chip v-if="field.nullable" size="x-small" color="grey-lighten-2">
                    {{ $t('connectors.nullable') }}
                  </v-chip>
                </v-list-item-subtitle>
              </v-list-item>
              <v-list-item v-if="filteredDestinationFields.length === 0">
                <v-list-item-title class="text-grey text-center">
                  {{ $t('pipeline.noFieldsFound') }}
                </v-list-item-title>
              </v-list-item>
            </v-list>
          </div>
        </v-card-text>
        <v-card-actions>
          <v-chip size="small" color="success">
            {{ $t('pipeline.mapped', { count: mappedDestinationFieldsCount, total: destinationSchema.fields.length }) }}
          </v-chip>
          <v-spacer />
          <v-chip
            v-if="unmappedRequiredFieldsCount > 0"
            size="small"
            color="error"
          >
            {{ $t('pipeline.requiredUnmapped', { count: unmappedRequiredFieldsCount }) }}
          </v-chip>
        </v-card-actions>
      </v-card>
    </v-col>
  </v-row>
</template>

<script setup>
import { ref, computed } from 'vue';
import { useI18n } from 'vue-i18n';

const { t } = useI18n();

const props = defineProps({
  sourceSchema: {
    type: Object,
    required: true
  },
  destinationSchema: {
    type: Object,
    required: true
  },
  mappings: {
    type: Array,
    default: () => []
  }
});

// Search filters
const sourceSearch = ref('');
const destinationSearch = ref('');

// Computed properties
const filteredSourceFields = computed(() => {
  if (!sourceSearch.value) {
    return props.sourceSchema.fields || [];
  }
  const search = sourceSearch.value.toLowerCase();
  return (props.sourceSchema.fields || []).filter(field =>
    field.name.toLowerCase().includes(search) ||
    field.type.toLowerCase().includes(search)
  );
});

const filteredDestinationFields = computed(() => {
  if (!destinationSearch.value) {
    return props.destinationSchema.fields || [];
  }
  const search = destinationSearch.value.toLowerCase();
  return (props.destinationSchema.fields || []).filter(field =>
    field.name.toLowerCase().includes(search) ||
    field.type.toLowerCase().includes(search)
  );
});

const mappedSourceFields = computed(() => {
  const mapped = new Set();
  props.mappings.forEach(mapping => {
    mapping.sourceFields.forEach(field => {
      mapped.add(field);
    });
  });
  return mapped;
});

const mappedDestinationFields = computed(() => {
  return new Set(props.mappings.map(m => m.destinationField));
});

const mappedSourceFieldsCount = computed(() => {
  return mappedSourceFields.value.size;
});

const mappedDestinationFieldsCount = computed(() => {
  return mappedDestinationFields.value.size;
});

const unmappedRequiredFieldsCount = computed(() => {
  return (props.destinationSchema.fields || []).filter(field =>
    field.required && !mappedDestinationFields.value.has(field.name)
  ).length;
});

// Methods
function isSourceFieldMapped(fieldName) {
  return mappedSourceFields.value.has(fieldName);
}

function isDestinationFieldMapped(fieldName) {
  return mappedDestinationFields.value.has(fieldName);
}

function getDestinationFieldClass(field) {
  if (isDestinationFieldMapped(field.name)) {
    return 'mapped-field';
  } else if (field.required) {
    return 'required-unmapped-field';
  }
  return '';
}
</script>

<style scoped>
.field-list-container {
  max-height: 25vh;
  overflow-y: auto;
}

.v-list-item {
  transition: background-color 0.2s;
}

.mapped-field {
  background-color: rgba(76, 175, 80, 0.1) !important;
}

.v-theme--dark .mapped-field {
  background-color: rgba(76, 175, 80, 0.15) !important;
}

.required-unmapped-field {
  background-color: rgba(244, 67, 54, 0.08) !important;
  border-left: 3px solid rgb(244, 67, 54);
}

.v-theme--dark .required-unmapped-field {
  background-color: rgba(244, 67, 54, 0.12) !important;
}
</style>
