import { ref } from 'vue';
import { usePipeline } from './usePipeline'; // To get createEmptyPipeline
import { fetchConnectors } from '@/services/connectorService';

// Common timezones list
const timezonesList = [
  { value: 'UTC', name: 'UTC (Coordinated Universal Time)' },
  { value: 'America/New_York', name: 'Eastern Time (US & Canada)' },
  { value: 'America/Chicago', name: 'Central Time (US & Canada)' },
  { value: 'America/Denver', name: 'Mountain Time (US & Canada)' },
  { value: 'America/Los_Angeles', name: 'Pacific Time (US & Canada)' },
  { value: 'Europe/London', name: 'London (GMT)' },
  { value: 'Europe/Paris', name: 'Paris (Central European Time)' },
  { value: 'Asia/Tokyo', name: 'Tokyo (Japan Standard Time)' },
  { value: 'Asia/Shanghai', name: 'China Standard Time' },
  { value: 'Australia/Sydney', name: 'Sydney (Australian Eastern Time)' }
];

export function usePipelineForm() {
  const { createEmptyPipeline } = usePipeline(); // Get helper from main composable

  // State moved from PipelinesView
  const connectors = ref([]);
  const editedPipeline = ref(createEmptyPipeline());
  const form = ref(null); // Ref for the main pipeline form
  const timezones = ref(timezonesList); // Make timezones available to components

  // Function to fetch connectors
  async function fetchConnectorsData() {
    try {
      connectors.value = await fetchConnectors();
    } catch (error) {
      console.error('Error fetching connectors:', error);
    }
  }

  // Extend createEmptyPipeline to include timezone in schedule and field mappings
  function getEmptyPipeline() {
    const pipeline = createEmptyPipeline();
    if (pipeline.schedule) {
      pipeline.schedule.timezone = 'UTC'; // Default to UTC
    } else {
      pipeline.schedule = {
        frequency: 'Daily',
        time: '00:00',
        cronExpression: '0 0 * * *',
        timezone: 'UTC'
      };
    }
    // Ensure fieldMappings array exists
    if (!pipeline.fieldMappings) {
      pipeline.fieldMappings = [];
    }
    return pipeline;
  }

  // Function to prepare the form for editing an existing pipeline
  function prepareEditPipeline(pipeline) {
    // Clone the pipeline to avoid modifying the original directly
    // Need to find the full source/destination objects from the fetched connectors
    const source = connectors.value.find(c => c.id === pipeline.sourceId || c.name === pipeline.sourceName) || null;
    const destination = connectors.value.find(c => c.id === pipeline.destinationId || c.name === pipeline.destinationName) || null;

    editedPipeline.value = {
      id: pipeline.id,
      name: pipeline.name,
      description: pipeline.description,
      // Store the full object or just the ID depending on how v-select is configured
      // Assuming v-select uses item-value="id" and returns the object
      sourceId: source,
      destinationId: destination,
      transformations: JSON.parse(JSON.stringify(pipeline.transformations || [])), // Deep clone transformations
      fieldMappings: JSON.parse(JSON.stringify(pipeline.fieldMappings || [])), // Deep clone field mappings
      isScheduled: pipeline.isScheduled,
      isActive: pipeline.isActive ?? true, // Preserve active status, default to true if not set
      schedule: pipeline.schedule ? { 
        ...pipeline.schedule,
        // Ensure timezone exists, default to UTC if not present
        timezone: pipeline.schedule.timezone || 'UTC' 
      } : {
        frequency: 'Daily',
        time: '00:00',
        cronExpression: '0 0 * * *',
        timezone: 'UTC' 
      }
    };
  }

  // Function to reset the form to a blank state for creating a new pipeline
  function resetForm() {
    editedPipeline.value = getEmptyPipeline();
    if (form.value) {
      form.value.resetValidation(); // Reset Vuetify form validation if needed
    }
  }

  return {
    // State
    form,
    editedPipeline,
    connectors,
    timezones,

    // Methods
    fetchConnectors: fetchConnectorsData,
    prepareEditPipeline,
    resetForm,
  };
}
