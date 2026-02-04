<template>
  <div class="space-y-4">
    <div v-if="modelValue.length === 0" class="text-gray-500 text-center py-4">
      No transformations added yet
    </div>
    
    <TransitionGroup name="list" tag="div" class="space-y-4">
      <div
        v-for="(transformation, index) in modelValue"
        :key="transformation.id"
        class="bg-gray-50 rounded-lg p-4"
      >
        <div class="flex items-center justify-between">
          <div class="flex items-center space-x-4">
            <div class="text-lg font-medium">{{ transformation.name }}</div>
            <div class="px-2 py-1 rounded text-sm" :class="getTypeClass(transformation.type)">
              {{ transformation.type }}
            </div>
          </div>
          
          <div class="flex items-center space-x-2">
            <button
              type="button"
              class="btn btn-icon"
              :disabled="index === 0"
              title="Move Up"
              @click="moveTransformation(index, 'up')"
            >
              ↑
            </button>
            <button
              type="button"
              class="btn btn-icon"
              :disabled="index === modelValue.length - 1"
              title="Move Down"
              @click="moveTransformation(index, 'down')"
            >
              ↓
            </button>
            <button
              type="button"
              class="btn btn-icon"
              title="Edit"
              @click="editTransformation(index)"
            >
              ✎
            </button>
            <button
              type="button"
              class="btn btn-icon btn-danger"
              title="Remove"
              @click="removeTransformation(index)"
            >
              ×
            </button>
          </div>
        </div>

        <div class="mt-4 text-sm text-gray-600">
          <div class="grid grid-cols-2 gap-4">
            <template v-if="transformation.type === 'Filter'">
              <div>
                <span class="font-medium">Column:</span>
                {{ transformation.config.filterColumn }}
              </div>
              <div>
                <span class="font-medium">Condition:</span>
                {{ transformation.config.operator }} {{ transformation.config.value }}
              </div>
            </template>

            <template v-else-if="transformation.type === 'Map'">
              <div>
                <span class="font-medium">From:</span>
                {{ transformation.config.sourceColumn }}
              </div>
              <div>
                <span class="font-medium">To:</span>
                {{ transformation.config.targetColumn }}
              </div>
            </template>

            <template v-else-if="transformation.type === 'Aggregation'">
              <div>
                <span class="font-medium">Group By:</span>
                {{ transformation.config.groupByColumns.join(', ') }}
              </div>
              <div>
                <span class="font-medium">{{ transformation.config.aggregationType }}:</span>
                {{ transformation.config.aggregationColumn }}
              </div>
            </template>

            <template v-else-if="transformation.type === 'Script'">
              <div class="col-span-2">
                <span class="font-medium">Custom Script</span>
                <pre class="mt-2 bg-gray-100 p-2 rounded">{{ transformation.config.script }}</pre>
              </div>
            </template>
          </div>

          <div class="mt-2">
            <span class="font-medium">Error Handling:</span>
            {{ transformation.errorHandling }}
          </div>
        </div>

        <div v-if="transformation.validationWarnings?.length" class="mt-4">
          <div v-for="warning in transformation.validationWarnings" :key="warning" class="text-yellow-600">
            ⚠️ {{ warning }}
          </div>
        </div>
      </div>
    </TransitionGroup>

    <TransformationDialog
      v-if="showEditDialog"
      :transformation="editingTransformation"
      :input-schema="getInputSchemaForIndex(editingIndex)"
      @save="handleEditSave"
      @close="closeEditDialog"
    />
  </div>
</template>

<script setup>
import { ref, computed, watch } from 'vue';
import { useTransformation } from '@/composables/useTransformation';
import TransformationDialog from './TransformationDialog.vue';

const props = defineProps({
  modelValue: {
    type: Array,
    required: true
  },
  inputSchema: {
    type: Object,
    required: true
  }
});

const emit = defineEmits(['update:modelValue', 'update:schema', 'show-error']);

const { getOutputSchema, validateSchemaImpact } = useTransformation();

const showEditDialog = ref(false);
const editingTransformation = ref(null);
const editingIndex = ref(-1);

const typeClasses = {
  Filter: 'bg-blue-100 text-blue-800',
  Map: 'bg-green-100 text-green-800',
  Aggregation: 'bg-purple-100 text-purple-800',
  Script: 'bg-yellow-100 text-yellow-800'
};

const getTypeClass = (type) => typeClasses[type] || 'bg-gray-100 text-gray-800';

const validateTransformationDependencies = async (transformations) => {
  const errors = [];
  let currentSchema = props.inputSchema;

  for (let i = 0; i < transformations.length; i++) {
    const transformation = transformations[i];
    
    try {
      // Validate transformation against current schema
      const validationResult = await validateSchemaImpact(transformation, currentSchema);
      
      if (!validationResult.isValid) {
        errors.push({
          transformationIndex: i,
          transformation: transformation.name,
          errors: validationResult.errors
        });
      } else {
        // Update schema for next transformation
        currentSchema = validationResult.outputSchema;
      }
    } catch (err) {
      errors.push({
        transformationIndex: i,
        transformation: transformation.name,
        errors: ['Failed to validate transformation']
      });
    }
  }

  return {
    isValid: errors.length === 0,
    errors,
    finalSchema: currentSchema
  };
};

const moveTransformation = async (fromIndex, toIndex) => {
  const newTransformations = [...props.modelValue];
  const [movedItem] = newTransformations.splice(fromIndex, 1);
  newTransformations.splice(toIndex, 0, movedItem);

  // Validate the new order
  const validationResult = await validateTransformationDependencies(newTransformations);
  
  if (validationResult.isValid) {
    emit('update:modelValue', newTransformations);
    emit('update:schema', validationResult.finalSchema);
  } else {
    // Show error and revert the move
    emit('show-error', {
      title: 'Invalid Transformation Order',
      message: 'The new order would break schema dependencies. Please check the transformation requirements.'
    });
  }
};

const removeTransformation = async (index) => {
  const newTransformations = props.modelValue.filter((_, i) => i !== index);
  
  // Validate remaining transformations
  const validationResult = await validateTransformationDependencies(newTransformations);
  
  emit('update:modelValue', newTransformations);
  emit('update:schema', validationResult.finalSchema);
};

const editTransformation = (index) => {
  editingTransformation.value = { ...props.modelValue[index] };
  editingIndex.value = index;
  showEditDialog.value = true;
};

const closeEditDialog = () => {
  showEditDialog.value = false;
  editingTransformation.value = null;
  editingIndex.value = -1;
};

const handleEditSave = (updatedTransformation) => {
  const newTransformations = [...props.modelValue];
  newTransformations[editingIndex.value] = updatedTransformation;
  emit('update:modelValue', newTransformations);
  closeEditDialog();
  updateSchemas();
};

const getInputSchemaForIndex = (index) => {
  let schema = props.inputSchema;
  
  for (let i = 0; i < index; i++) {
    schema = getOutputSchema(props.modelValue[i], schema);
  }
  
  return schema;
};

const updateSchemas = () => {
  let currentSchema = props.inputSchema;
  
  props.modelValue.forEach(transformation => {
    currentSchema = getOutputSchema(transformation, currentSchema);
  });

  emit('update:schema', currentSchema);
};

watch(() => props.inputSchema, async () => {
  // Revalidate all transformations when input schema changes
  const validationResult = await validateTransformationDependencies(props.modelValue);
  
  if (!validationResult.isValid) {
    emit('show-error', {
      title: 'Schema Change Warning',
      message: 'Some transformations may be invalid due to schema changes. Please review the pipeline configuration.'
    });
  }
  
  emit('update:schema', validationResult.finalSchema);
}, { deep: true });

// Helper function to get column type from schema
const getColumnType = (schema, columnName) => {
  const column = schema.columns.find(c => c.name === columnName);
  return column?.type || 'unknown';
};

// Helper function to validate data type compatibility
const validateDataTypes = (sourceType, targetType) => {
  // Define type compatibility rules
  const typeCompatibility = {
    'int': ['int', 'decimal', 'varchar'],
    'decimal': ['decimal', 'varchar'],
    'varchar': ['varchar'],
    'datetime': ['datetime', 'varchar'],
    'boolean': ['boolean', 'int', 'varchar']
  };

  const compatibleTypes = typeCompatibility[sourceType] || [];
  return compatibleTypes.includes(targetType);
};
</script>

<style scoped>
.list-move,
.list-enter-active,
.list-leave-active {
  transition: all 0.5s ease;
}

.list-enter-from,
.list-leave-to {
  opacity: 0;
  transform: translateX(30px);
}

.list-leave-active {
  position: absolute;
}
</style>