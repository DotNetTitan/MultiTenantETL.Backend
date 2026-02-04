import { ref, computed } from 'vue';
import { useI18n } from 'vue-i18n';
import { useTransformation } from './useTransformation';
import { useConnector } from './useConnector';
import { fetchPipelines, fetchPipelineById, savePipeline as apiSavePipeline, deletePipeline as apiDeletePipeline, executePipeline as apiExecutePipeline, togglePipelineStatus as apiTogglePipelineStatus } from '@/services/pipelineService';
import { useTenantStore } from '@/stores/tenant';

export function usePipeline() {
  const { t } = useI18n();
  const { validateTransformation, getOutputSchema } = useTransformation();
  const { detectSchema } = useConnector();
  const tenantStore = useTenantStore();

  // State
  const error = ref(null);
  const pipelines = ref([]);
  const loading = ref(false);
  const savingPipeline = ref(false);
  const deletingPipeline = ref(false);

  // Form data
  const search = ref('');
  const statusFilter = ref('All');
  const isActiveFilter = ref('All');
  const sortBy = ref('name_asc');

  // Status and sort options for UI - computed to support i18n
  const statusOptions = computed(() => [
    { title: t('filters.allStatuses'), value: 'All' },
    { title: t('executions.idle'), value: 'Idle' },
    { title: t('executions.running'), value: 'Running' },
    { title: t('executions.failed'), value: 'Failed' }
  ]);

  const activeOptions = computed(() => [
    { title: t('common.all'), value: 'All' },
    { title: t('common.active'), value: 'Active' },
    { title: t('common.inactive'), value: 'Inactive' }
  ]);

  const sortOptions = computed(() => [
    { title: t('filters.nameAsc'), value: 'name_asc' },
    { title: t('filters.nameDesc'), value: 'name_desc' },
    { title: t('filters.lastRunNewest'), value: 'lastRun_desc' },
    { title: t('filters.lastRunOldest'), value: 'lastRun_asc' }
  ]);

  // Fetch pipeline list with optional filters
  const loadPipelines = async () => {
    try {
      loading.value = true;

      const filters = {
        search: search.value,
        status: statusFilter.value,
        sortBy: sortBy.value
      };

      if (isActiveFilter.value === 'Active') {
        filters.isActive = true;
      } else if (isActiveFilter.value === 'Inactive') {
        filters.isActive = false;
      }

      const pipelineList = await fetchPipelines(filters);

      // Backend returns camelCase JSON (sourceConnectorName, destinationConnectorName)
      // Map to sourceName/destinationName for table display
      pipelines.value = pipelineList.map(pipeline => ({
        ...pipeline,
        sourceName: pipeline.sourceConnectorName || 'Unknown',
        destinationName: pipeline.destinationConnectorName || 'Unknown'
      }));
    } catch (err) {
      error.value = err.message;
      console.error('Error loading pipelines:', err);
    } finally {
      loading.value = false;
    }
  };

  // Fetch pipeline by ID
  const getPipeline = async (id) => {
    try {
      loading.value = true;
      return await fetchPipelineById(id);
    } catch (err) {
      error.value = err.message;
      console.error(`Error getting pipeline ${id}:`, err);
      return null;
    } finally {
      loading.value = false;
    }
  };

  // Save (create or update) a pipeline
  const savePipeline = async (pipeline) => {
    try {
      savingPipeline.value = true;
      const result = await apiSavePipeline(pipeline);
      return result;
    } catch (err) {
      error.value = err.message;
      console.error('Error saving pipeline:', err);
      throw err;
    } finally {
      savingPipeline.value = false;
    }
  };

  // Delete a pipeline
  const deletePipeline = async (id) => {
    try {
      deletingPipeline.value = true;
      await apiDeletePipeline(id);
      // Remove the pipeline from the local state after successful deletion
      const index = pipelines.value.findIndex(p => p.id === id);
      if (index !== -1) {
        pipelines.value.splice(index, 1);
      }
      return true;
    } catch (err) {
      error.value = err.message;
      console.error(`Error deleting pipeline ${id}:`, err);
      return false;
    } finally {
      deletingPipeline.value = false;
    }
  };

  // Execute a pipeline
  const executePipeline = async (id) => {
    try {
      const result = await apiExecutePipeline(id);
      // Update the pipeline status in the local state after successful execution start
      const pipeline = pipelines.value.find(p => p.id === id);
      if (pipeline) {
        pipeline.status = 'Running'; // Or update based on actual API response if available
        pipeline.lastRunAt = new Date().toISOString(); // Update last run time
      }
      return result;
    } catch (err) {
      error.value = err.message;
      console.error(`Error executing pipeline ${id}:`, err);
      throw err;
    }
  };

  // Toggle pipeline active status
  const togglePipelineStatus = async (id) => {
    try {
      const result = await apiTogglePipelineStatus(id);
      // Update the pipeline in the local state after successful toggle
      const pipeline = pipelines.value.find(p => p.id === id);
      if (pipeline) {
        pipeline.isActive = result.isActive;
      }
      return result;
    } catch (err) {
      error.value = err.message;
      console.error(`Error toggling pipeline status ${id}:`, err);
      throw err;
    }
  };

  // Create an empty pipeline object
  const createEmptyPipeline = () => {
    return {
      id: null,
      name: '',
      description: '',
      sourceId: null,
      destinationId: null,
      transformations: [],
      fieldMappings: [], // NEW: Field mappings array
      isScheduled: false,
      isActive: true, // Default to active when creating new pipeline
      schedule: {
        frequency: 'Daily',
        time: '00:00',
        cronExpression: '0 0 * * *'
      }
    };
  };

  // Get color for a status
  const getStatusColor = (status) => {
    switch (status?.toLowerCase()) {
      case 'completed':
        return 'success';
      case 'running':
        return 'info';
      case 'failed':
        return 'error';
      case 'idle':
        return 'grey';
      default:
        return 'grey';
    }
  };

  // Format a date string
  const formatDate = (dateString) => {
    if (!dateString) return '-';
    const date = new Date(dateString);
    return date.toLocaleString();
  };

  // Setup tenant subscription to refresh data when tenant changes
  const setupTenantSubscription = () => {
    tenantStore.$subscribe(() => {
      if (tenantStore.currentTenantId) {
        loadPipelines();
      }
    });
  };

  // Validate entire pipeline including data flow and dependencies
  const validatePipeline = async (pipeline) => {
    const errors = [];

    if (!pipeline.sourceId) {
      errors.push('Source is required');
    }
    if (!pipeline.destinationId) {
      errors.push('Destination is required');
    }

    // Validate transformations chain
    try {
      const sourceSchema = await detectSchema(pipeline.sourceId);
      let currentSchema = sourceSchema;

      for (const transformation of pipeline.transformations) {
        // Validate individual transformation
        const validationResult = validateTransformation(transformation, currentSchema);
        if (!validationResult.isValid) {
          errors.push(...validationResult.errors.map(err =>
            `Transformation "${transformation.name}": ${err}`
          ));
        }

        // Update schema for next transformation
        currentSchema = getOutputSchema(transformation, currentSchema);
      }

      // Validate final schema matches destination requirements
      const destinationSchema = await detectSchema(pipeline.destinationId);
      if (destinationSchema) {
        validateSchemaCompatibility(currentSchema, destinationSchema, errors);
      }
    } catch (err) {
      errors.push(`Schema validation error: ${err.message}`);
    }

    // Validate schedule if enabled
    if (pipeline.isScheduled) {
      validateSchedule(pipeline.schedule, errors);
    }

    return {
      isValid: errors.length === 0,
      errors
    };
  };

  // Validate schema compatibility between source and destination
  const validateSchemaCompatibility = (sourceSchema, destSchema, errors) => {
    destSchema.columns.forEach(destCol => {
      const sourceCol = sourceSchema.columns.find(c => c.name === destCol.name);

      if (!sourceCol && !destCol.nullable) {
        errors.push(`Required destination column '${destCol.name}' is missing from pipeline output`);
      } else if (sourceCol && !isCompatibleType(sourceCol.type, destCol.type)) {
        errors.push(
          `Type mismatch for column '${destCol.name}': ` +
          `expected ${destCol.type}, got ${sourceCol.type}`
        );
      }
    });
  };

  // Check if data types are compatible
  const isCompatibleType = (sourceType, destType) => {
    // Define type compatibility rules
    const compatibility = {
      'int': ['int', 'decimal', 'varchar', 'nvarchar'],
      'decimal': ['decimal', 'varchar', 'nvarchar'],
      'varchar': ['varchar', 'nvarchar', 'text'],
      'nvarchar': ['nvarchar', 'text'],
      'datetime': ['datetime', 'varchar', 'nvarchar'],
      'boolean': ['boolean', 'int', 'varchar', 'nvarchar']
    };

    return compatibility[sourceType]?.includes(destType) || sourceType === destType;
  };

  // Validate schedule configuration
  const validateSchedule = (schedule, errors) => {
    if (!schedule.frequency) {
      errors.push('Schedule frequency is required');
    }
    if (!schedule.time) {
      errors.push('Schedule time is required');
    }
    if (schedule.frequency === 'Custom' && !schedule.cronExpression) {
      errors.push('Cron expression is required for custom schedule');
    }
  };

  // Get pipeline dependencies
  const getDependencies = (pipelines) => {
    const dependencies = new Map();

    pipelines.forEach(pipeline => {
      const deps = [];

      // Check for shared data sources
      pipelines.forEach(otherPipeline => {
        if (otherPipeline.id === pipeline.id) return;

        if (otherPipeline.destinationId === pipeline.sourceId) {
          deps.push({
            id: otherPipeline.id,
            name: otherPipeline.name,
            type: 'source'
          });
        }
      });

      dependencies.set(pipeline.id, deps);
    });

    return dependencies;
  };

  // Calculate optimal execution order for multiple pipelines
  const getExecutionOrder = (pipelines) => {
    const dependencies = getDependencies(pipelines);
    const visited = new Set();
    const order = [];

    const visit = (pipelineId) => {
      if (visited.has(pipelineId)) return;
      visited.add(pipelineId);

      dependencies.get(pipelineId)?.forEach(dep => {
        visit(dep.id);
      });

      order.push(pipelineId);
    };

    pipelines.forEach(pipeline => {
      if (!visited.has(pipeline.id)) {
        visit(pipeline.id);
      }
    });

    return order;
  };

  // Define error handling strategies
  const errorStrategies = {
    STOP: 'stop',             // Stop pipeline execution on error
    CONTINUE: 'continue',     // Continue with next transformation
    RETRY: 'retry',           // Retry the failed transformation
    SKIP: 'skip'             // Skip records that cause errors
  };

  return {
    // State
    error,
    pipelines,
    loading,
    savingPipeline,
    deletingPipeline,
    search,
    statusFilter,
    isActiveFilter,
    sortBy,
    statusOptions,
    activeOptions,
    sortOptions,

    // Methods - API related
    loadPipelines,
    getPipeline,
    savePipeline,
    deletePipeline,
    executePipeline,
    togglePipelineStatus,
    createEmptyPipeline,
    setupTenantSubscription,

    // UI helper methods
    getStatusColor,
    formatDate,

    // Validation methods
    validatePipeline,
    getDependencies,
    getExecutionOrder,
    errorStrategies
  };
}