import { ref } from 'vue';

export function useTransformation() {
  const schemaCache = ref(new Map());
  const validationErrors = ref([]);

  const validateTransformation = (transformation, inputSchema) => {
    const errors = [];
    
    // Basic validation
    if (!transformation.type) {
      errors.push({ field: 'type', message: 'Transformation type is required' });
      return { isValid: false, errors };
    }

    if (!transformation.name?.trim()) {
      errors.push({ field: 'name', message: 'Transformation name is required' });
    }

    try {
      switch (transformation.type) {
        case 'Filter':
          if (!transformation.config.filterColumn) {
            errors.push({ field: 'filterColumn', message: 'Filter column is required' });
          }
          if (!transformation.config.operator) {
            errors.push({ field: 'operator', message: 'Operator is required' });
          }
          if (!['isEmpty', 'isNotEmpty'].includes(transformation.config.operator) && 
              !transformation.config.value) {
            errors.push({ field: 'value', message: 'Filter value is required for this operator' });
          }
          // Validate column exists in schema
          if (inputSchema && !inputSchema.columns.find(c => c.name === transformation.config.filterColumn)) {
            errors.push({ field: 'filterColumn', message: `Column '${transformation.config.filterColumn}' not found in input schema` });
          }
          break;

        case 'Map':
          if (!transformation.config.sourceColumn) {
            errors.push({ field: 'sourceColumn', message: 'Source column is required' });
          }
          if (!Array.isArray(transformation.config.mappings) || transformation.config.mappings.length === 0) {
            errors.push({ field: 'mappings', message: 'At least one mapping is required' });
          } else {
            transformation.config.mappings.forEach((mapping, index) => {
              if (!mapping.from) {
                errors.push({ field: `mappings[${index}].from`, message: 'From value is required' });
              }
              if (!mapping.to) {
                errors.push({ field: `mappings[${index}].to`, message: 'To value is required' });
              }
            });
          }
          // Validate source column exists
          if (inputSchema && !inputSchema.columns.find(c => c.name === transformation.config.sourceColumn)) {
            errors.push({ field: 'sourceColumn', message: `Column '${transformation.config.sourceColumn}' not found in input schema` });
          }
          break;

        case 'Aggregation':
          if (!Array.isArray(transformation.config.groupByColumns) || transformation.config.groupByColumns.length === 0) {
            errors.push({ field: 'groupByColumns', message: 'At least one group by column is required' });
          }
          if (!transformation.config.aggregationType) {
            errors.push({ field: 'aggregationType', message: 'Aggregation type is required' });
          }
          if (!transformation.config.resultColumn) {
            errors.push({ field: 'resultColumn', message: 'Result column name is required' });
          }
          if (transformation.config.aggregationType !== 'count' && !transformation.config.aggregationColumn) {
            errors.push({ field: 'aggregationColumn', message: 'Aggregation column is required for this aggregation type' });
          }
          // Validate columns exist
          if (inputSchema) {
            if (Array.isArray(transformation.config.groupByColumns)) {
              transformation.config.groupByColumns.forEach(col => {
                if (!inputSchema.columns.find(c => c.name === col)) {
                  errors.push({ field: 'groupByColumns', message: `Group by column '${col}' not found in input schema` });
                }
              });
            }
            if (transformation.config.aggregationColumn &&
                !inputSchema.columns.find(c => c.name === transformation.config.aggregationColumn)) {
              errors.push({ field: 'aggregationColumn', message: `Aggregation column '${transformation.config.aggregationColumn}' not found in input schema` });
            }
          }
          break;

        case 'Script':
          if (!transformation.config.script?.trim()) {
            errors.push({ field: 'script', message: 'Script is required' });
          } else {
            try {
              // Basic syntax check for JavaScript
              new Function('row', transformation.config.script);
            } catch (e) {
              errors.push({ field: 'script', message: `Script syntax error: ${e.message}` });
            }
          }
          break;

        default:
          errors.push({ field: 'type', message: 'Invalid transformation type' });
      }

      return {
        isValid: errors.length === 0,
        errors
      };
    } catch (err) {
      console.error('Validation error:', err);
      return {
        isValid: false,
        errors: [{ field: 'general', message: 'An error occurred while validating the transformation' }]
      };
    }
  };

  const getOutputSchema = (transformation, inputSchema) => {
    try {
      switch (transformation.type) {
        case 'Filter':
          // Filter doesn't change schema structure
          return { ...inputSchema };

        case 'Map':
          return handleMapTransformationSchema(transformation, inputSchema);

        case 'Join':
          return handleJoinTransformationSchema(transformation, inputSchema);

        case 'Aggregate':
          return handleAggregateTransformationSchema(transformation, inputSchema);

        case 'Script':
          return handleScriptTransformationSchema(transformation, inputSchema);

        default:
          throw new Error(`Unknown transformation type: ${transformation.type}`);
      }
    } catch (err) {
      console.error('Error calculating output schema:', err);
      throw err;
    }
  };

  const handleMapTransformationSchema = (transformation, inputSchema) => {
    const outputSchema = { ...inputSchema };
    const { sourceColumn, targetColumn, mapping } = transformation.config;

    // Validate source column exists
    if (!outputSchema.columns.find(c => c.name === sourceColumn)) {
      throw new Error(`Source column '${sourceColumn}' not found in schema`);
    }

    // Add or update target column
    const sourceColumnDef = outputSchema.columns.find(c => c.name === sourceColumn);
    const targetColumnDef = {
      name: targetColumn,
      type: determineTargetType(sourceColumnDef.type, mapping),
      nullable: sourceColumnDef.nullable
    };

    const existingTargetIndex = outputSchema.columns.findIndex(c => c.name === targetColumn);
    if (existingTargetIndex >= 0) {
      outputSchema.columns[existingTargetIndex] = targetColumnDef;
    } else {
      outputSchema.columns.push(targetColumnDef);
    }

    return outputSchema;
  };

  const handleJoinTransformationSchema = (transformation, inputSchema) => {
    const { joinTable, joinType, joinConditions } = transformation.config;

    // In a real implementation, you would fetch the join table schema
    // For now, we'll assume it's provided in the config
    const joinTableSchema = transformation.config.joinTableSchema;

    if (!joinTableSchema) {
      throw new Error('Join table schema is required');
    }

    // Validate join conditions
    for (const condition of joinConditions) {
      if (!inputSchema.columns.find(c => c.name === condition.leftColumn)) {
        throw new Error(`Left join column '${condition.leftColumn}' not found in schema`);
      }
      if (!joinTableSchema.columns.find(c => c.name === condition.rightColumn)) {
        throw new Error(`Right join column '${condition.rightColumn}' not found in join table schema`);
      }
    }

    // Combine schemas based on join type
    const outputColumns = [...inputSchema.columns];
    joinTableSchema.columns.forEach(column => {
      if (!joinConditions.some(c => c.rightColumn === column.name)) {
        const newColumn = {
          ...column,
          nullable: joinType !== 'INNER',
          name: `${joinTable}_${column.name}` // Prefix to avoid name conflicts
        };
        outputColumns.push(newColumn);
      }
    });

    return {
      ...inputSchema,
      columns: outputColumns
    };
  };

  const handleAggregateTransformationSchema = (transformation, inputSchema) => {
    const { groupByColumns, aggregations } = transformation.config;

    // Validate group by columns exist
    groupByColumns.forEach(column => {
      if (!inputSchema.columns.find(c => c.name === column)) {
        throw new Error(`Group by column '${column}' not found in schema`);
      }
    });

    // Create new schema with group by columns and aggregations
    const outputColumns = [
      // Keep group by columns with same type
      ...groupByColumns.map(colName => {
        const originalCol = inputSchema.columns.find(c => c.name === colName);
        return { ...originalCol };
      }),
      // Add aggregation columns
      ...aggregations.map(agg => ({
        name: agg.alias,
        type: determineAggregationType(agg.type, inputSchema.columns.find(c => c.name === agg.column)?.type),
        nullable: false
      }))
    ];

    return {
      ...inputSchema,
      columns: outputColumns
    };
  };

  const handleScriptTransformationSchema = (transformation, inputSchema) => {
    // For script transformations, we rely on the declared output schema in the config
    if (!transformation.config.outputSchema) {
      throw new Error('Script transformation must declare its output schema');
    }

    return {
      ...inputSchema,
      columns: transformation.config.outputSchema.columns
    };
  };

  const determineTargetType = (sourceType, mapping) => {
    if (mapping?.type) {
      return mapping.type; // Explicit type conversion
    }

    // Default type mapping rules
    const typeMapping = {
      'int': 'int',
      'decimal': 'decimal',
      'varchar': 'varchar',
      'datetime': 'datetime',
      'boolean': 'boolean'
    };

    return typeMapping[sourceType] || 'varchar';
  };

  const determineAggregationType = (aggregationType, sourceType) => {
    const aggregationTypeMap = {
      'COUNT': 'int',
      'SUM': sourceType === 'int' ? 'int' : 'decimal',
      'AVG': 'decimal',
      'MIN': sourceType,
      'MAX': sourceType
    };

    return aggregationTypeMap[aggregationType] || sourceType;
  };

  const validateSchemaImpact = async (transformation, inputSchema) => {
    try {
      const outputSchema = getOutputSchema(transformation, inputSchema);
      
      // Cache the calculated schema for this transformation
      const cacheKey = `${transformation.id}_${JSON.stringify(inputSchema)}`;
      schemaCache.value.set(cacheKey, outputSchema);

      return {
        isValid: true,
        outputSchema
      };
    } catch (err) {
      return {
        isValid: false,
        errors: [err.message]
      };
    }
  };

  const clearSchemaCache = () => {
    schemaCache.value.clear();
  };

  return {
    validateTransformation,
    getOutputSchema,
    validateSchemaImpact,
    clearSchemaCache,
    validationErrors
  };
}

// Add type compatibility validation
export const isTypeCompatible = (sourceType, targetType, conversionType = 'implicit') => {
  const implicitConversions = {
    'int': ['int', 'decimal', 'varchar'],
    'decimal': ['decimal', 'varchar'],
    'varchar': ['varchar'],
    'datetime': ['datetime', 'varchar'],
    'boolean': ['boolean', 'int', 'varchar']
  };

  const explicitConversions = {
    'int': ['int', 'decimal', 'varchar', 'boolean'],
    'decimal': ['int', 'decimal', 'varchar'],
    'varchar': ['int', 'decimal', 'datetime', 'boolean', 'varchar'],
    'datetime': ['datetime', 'varchar', 'int'],
    'boolean': ['boolean', 'int', 'varchar']
  };

  const conversions = conversionType === 'implicit' ? implicitConversions : explicitConversions;
  return conversions[sourceType]?.includes(targetType) || false;
};