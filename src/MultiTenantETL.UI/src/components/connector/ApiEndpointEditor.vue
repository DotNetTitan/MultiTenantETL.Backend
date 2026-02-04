<template>
  <div class="api-endpoint-editor">
    <div class="d-flex align-center mb-4">
      <div class="flex-grow-1">
        <h4 class="text-subtitle-1">API Endpoints</h4>
        <p class="text-caption text-grey">{{ getRequirementText() }}</p>
      </div>
      <v-btn
        size="small"
        color="primary"
        prepend-icon="mdi-plus"
        @click="addEndpoint"
      >
        Add Endpoint
      </v-btn>
    </div>

    <div v-if="localEndpoints.length === 0" class="text-center py-8">
      <v-icon size="64" color="grey-lighten-2">mdi-api-off</v-icon>
      <p class="mt-2 text-grey">No endpoints configured</p>
      <p class="text-caption text-grey">Add endpoints based on data source direction</p>
    </div>

    <v-expansion-panels v-else class="mb-4">
      <v-expansion-panel
        v-for="(endpoint, index) in localEndpoints"
        :key="endpoint.id"
      >
        <v-expansion-panel-title>
          <div class="d-flex align-center">
            <v-chip :color="getMethodColor(endpoint.method)" size="small" class="mr-2">
              {{ endpoint.method }}
            </v-chip>
            <span class="font-weight-medium">{{ endpoint.path || 'Untitled Endpoint' }}</span>
            <v-spacer />
            <v-btn
              icon
              size="small"
              variant="text"
              color="error"
              @click.stop="removeEndpoint(index)"
            >
              <v-icon>mdi-delete</v-icon>
            </v-btn>
          </div>
        </v-expansion-panel-title>

        <v-expansion-panel-text>
          <v-form>
            <v-row>
              <!-- Method and Path -->
              <v-col cols="12" md="3">
                <v-select
                  v-model="endpoint.method"
                  :items="availableMethods"
                  label="HTTP Method"
                  variant="outlined"
                  density="compact"
                  required
                />
              </v-col>
              <v-col cols="12" md="9">
                <v-text-field
                  v-model="endpoint.path"
                  label="Endpoint Path"
                  placeholder="/customers or /customers/{id}"
                  variant="outlined"
                  density="compact"
                  required
                  hint="Use {id} for path parameters"
                  persistent-hint
                />
              </v-col>

              <!-- Description -->
              <v-col cols="12">
                <v-text-field
                  v-model="endpoint.description"
                  label="Description"
                  placeholder="Brief description of this endpoint"
                  variant="outlined"
                  density="compact"
                />
              </v-col>

              <!-- Request Schema (for POST/PUT/PATCH) -->
              <v-col v-if="['POST', 'PUT', 'PATCH'].includes(endpoint.method)" cols="12">
                <v-divider class="mb-3" />
                <h5 class="text-subtitle-2 mb-2">Request Configuration</h5>
                <v-textarea
                  v-model="endpoint.requestSchema"
                  label="Request Schema (JSON)"
                  placeholder="{&quot;customer&quot;: {&quot;name&quot;: &quot;&quot;, &quot;email&quot;: &quot;&quot;}}"
                  variant="outlined"
                  rows="4"
                  hint="Full request body structure"
                  persistent-hint
                />
              </v-col>

              <v-col v-if="['POST', 'PUT', 'PATCH'].includes(endpoint.method)" cols="12">
                <v-text-field
                  v-model="endpoint.requestDataPath"
                  label="Request Data Path"
                  placeholder="customer"
                  variant="outlined"
                  density="compact"
                  hint="Where to place mapped data in request (e.g., 'customer' or 'data.customer')"
                  persistent-hint
                />
              </v-col>

              <!-- Response Schema -->
              <v-col cols="12">
                <v-divider class="mb-3" />
                <h5 class="text-subtitle-2 mb-2">Response Configuration</h5>
                <v-textarea
                  v-model="endpoint.responseSchema"
                  label="Response Schema (JSON)"
                  placeholder="{&quot;status&quot;: &quot;success&quot;, &quot;data&quot;: {&quot;customer&quot;: {...}}}"
                  variant="outlined"
                  rows="4"
                  hint="Full response structure"
                  persistent-hint
                />
              </v-col>

              <v-col cols="12" md="6">
                <v-text-field
                  v-model="endpoint.responseDataPath"
                  label="Response Data Path"
                  placeholder="data.customer or data.customers"
                  variant="outlined"
                  density="compact"
                  hint="Path to business data in response"
                  persistent-hint
                  required
                />
              </v-col>

              <v-col cols="12" md="6">
                <v-text-field
                  v-model="endpoint.errorPath"
                  label="Error Path (Optional)"
                  placeholder="error.message"
                  variant="outlined"
                  density="compact"
                  hint="Path to error message in response"
                  persistent-hint
                />
              </v-col>
            </v-row>
          </v-form>
        </v-expansion-panel-text>
      </v-expansion-panel>
    </v-expansion-panels>
  </div>
</template>

<script setup>
import { ref, computed, watch } from 'vue';

const props = defineProps({
  modelValue: {
    type: Array,
    default: () => []
  },
  direction: {
    type: String,
    default: 'source'
  }
});

const emit = defineEmits(['update:modelValue']);

const localEndpoints = ref([...props.modelValue]);

// Available methods based on direction
const availableMethods = computed(() => {
  if (props.direction === 'source') {
    return ['GET'];
  } else if (props.direction === 'destination') {
    return ['POST', 'PUT', 'PATCH'];
  } else { // both
    return ['GET', 'POST', 'PUT', 'PATCH'];
  }
});

// Watch for changes - emit updates
watch(localEndpoints, (newVal) => {
  emit('update:modelValue', newVal);
}, { deep: true });

// Watch for external changes - only update if different
watch(() => props.modelValue, (newVal) => {
  // Only update if the values are actually different to avoid loops
  if (JSON.stringify(newVal) !== JSON.stringify(localEndpoints.value)) {
    localEndpoints.value = [...newVal];
  }
});

function addEndpoint() {
  const defaultMethod = props.direction === 'destination' ? 'POST' : 'GET';
  
  localEndpoints.value.push({
    id: `endpoint-${Date.now()}-${Math.random()}`,
    method: defaultMethod,
    path: '',
    description: '',
    requestSchema: '',
    requestDataPath: '',
    responseSchema: '',
    responseDataPath: '',
    errorPath: 'error.message'
  });
}

function removeEndpoint(index) {
  localEndpoints.value.splice(index, 1);
}

function getMethodColor(method) {
  const colors = {
    GET: 'blue',
    POST: 'green',
    PUT: 'orange',
    PATCH: 'purple',
    DELETE: 'red'
  };
  return colors[method] || 'grey';
}

function getRequirementText() {
  if (props.direction === 'source') {
    return 'Required: At least one GET endpoint';
  } else if (props.direction === 'destination') {
    return 'Required: At least one POST, PUT, or PATCH endpoint';
  } else { // both
    return 'Required: At least one GET endpoint and one POST/PUT/PATCH endpoint';
  }
}
</script>

<style scoped>
.api-endpoint-editor {
  width: 100%;
}
</style>
