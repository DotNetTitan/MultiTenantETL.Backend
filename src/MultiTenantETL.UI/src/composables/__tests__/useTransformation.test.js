import { describe, it, expect, beforeEach } from 'vitest'
import { useTransformation, isTypeCompatible } from '@/composables/useTransformation'

describe('useTransformation Composable', () => {
  let composable

  const mockInputSchema = {
    columns: [
      { name: 'id', type: 'int', nullable: false },
      { name: 'name', type: 'varchar', nullable: false },
      { name: 'email', type: 'varchar', nullable: true },
      { name: 'age', type: 'int', nullable: true },
      { name: 'salary', type: 'decimal', nullable: true },
      { name: 'created_at', type: 'datetime', nullable: false }
    ]
  }

  const mockJoinTableSchema = {
    columns: [
      { name: 'user_id', type: 'int', nullable: false },
      { name: 'department', type: 'varchar', nullable: false },
      { name: 'manager_id', type: 'int', nullable: true }
    ]
  }

  beforeEach(() => {
    composable = useTransformation()
    composable.clearSchemaCache()
  })

  describe('Initial State', () => {
    it('should initialize with empty validation errors', () => {
      expect(composable.validationErrors.value).toEqual([])
    })
  })

  describe('validateTransformation', () => {
    describe('Basic validation', () => {
      it('should require transformation type', () => {
        const result = composable.validateTransformation({}, mockInputSchema)
        expect(result.isValid).toBe(false)
        expect(result.errors).toContainEqual({
          field: 'type',
          message: 'Transformation type is required'
        })
      })

      it('should require transformation name', () => {
        const transformation = { type: 'Filter', config: {} }
        const result = composable.validateTransformation(transformation, mockInputSchema)
        expect(result.isValid).toBe(false)
        expect(result.errors).toContainEqual({
          field: 'name',
          message: 'Transformation name is required'
        })
      })

      it('should reject invalid transformation type', () => {
        const transformation = {
          type: 'InvalidType',
          name: 'Test',
          config: {}
        }
        const result = composable.validateTransformation(transformation, mockInputSchema)
        expect(result.isValid).toBe(false)
        expect(result.errors).toContainEqual({
          field: 'type',
          message: 'Invalid transformation type'
        })
      })
    })

    describe('Filter transformation validation', () => {
      it('should validate filter column is required', () => {
        const transformation = {
          type: 'Filter',
          name: 'Test Filter',
          config: { operator: 'equals', value: 'test' }
        }
        const result = composable.validateTransformation(transformation, mockInputSchema)
        expect(result.isValid).toBe(false)
        expect(result.errors).toContainEqual({
          field: 'filterColumn',
          message: 'Filter column is required'
        })
      })

      it('should validate operator is required', () => {
        const transformation = {
          type: 'Filter',
          name: 'Test Filter',
          config: { filterColumn: 'name' }
        }
        const result = composable.validateTransformation(transformation, mockInputSchema)
        expect(result.isValid).toBe(false)
        expect(result.errors).toContainEqual({
          field: 'operator',
          message: 'Operator is required'
        })
      })

      it('should validate value is required for most operators', () => {
        const transformation = {
          type: 'Filter',
          name: 'Test Filter',
          config: { filterColumn: 'name', operator: 'equals' }
        }
        const result = composable.validateTransformation(transformation, mockInputSchema)
        expect(result.isValid).toBe(false)
        expect(result.errors).toContainEqual({
          field: 'value',
          message: 'Filter value is required for this operator'
        })
      })

      it('should not require value for isEmpty and isNotEmpty operators', () => {
        const transformation = {
          type: 'Filter',
          name: 'Test Filter',
          config: { filterColumn: 'name', operator: 'isEmpty' }
        }
        const result = composable.validateTransformation(transformation, mockInputSchema)
        expect(result.isValid).toBe(true)
      })

      it('should validate filter column exists in schema', () => {
        const transformation = {
          type: 'Filter',
          name: 'Test Filter',
          config: { filterColumn: 'nonexistent', operator: 'equals', value: 'test' }
        }
        const result = composable.validateTransformation(transformation, mockInputSchema)
        expect(result.isValid).toBe(false)
        expect(result.errors).toContainEqual({
          field: 'filterColumn',
          message: "Column 'nonexistent' not found in input schema"
        })
      })

      it('should pass validation for valid filter transformation', () => {
        const transformation = {
          type: 'Filter',
          name: 'Test Filter',
          config: { filterColumn: 'name', operator: 'equals', value: 'test' }
        }
        const result = composable.validateTransformation(transformation, mockInputSchema)
        expect(result.isValid).toBe(true)
        expect(result.errors).toEqual([])
      })
    })

    describe('Map transformation validation', () => {
      it('should validate source column is required', () => {
        const transformation = {
          type: 'Map',
          name: 'Test Map',
          config: { mappings: [{ from: 'old', to: 'new' }] }
        }
        const result = composable.validateTransformation(transformation, mockInputSchema)
        expect(result.isValid).toBe(false)
        expect(result.errors).toContainEqual({
          field: 'sourceColumn',
          message: 'Source column is required'
        })
      })

      it('should validate mappings array is required and not empty', () => {
        const transformation = {
          type: 'Map',
          name: 'Test Map',
          config: { sourceColumn: 'name' }
        }
        const result = composable.validateTransformation(transformation, mockInputSchema)
        expect(result.isValid).toBe(false)
        expect(result.errors).toContainEqual({
          field: 'mappings',
          message: 'At least one mapping is required'
        })
      })

      it('should validate mapping from and to values are required', () => {
        const transformation = {
          type: 'Map',
          name: 'Test Map',
          config: {
            sourceColumn: 'name',
            mappings: [{ from: '', to: '' }, { from: 'a', to: '' }]
          }
        }
        const result = composable.validateTransformation(transformation, mockInputSchema)
        expect(result.isValid).toBe(false)
        expect(result.errors).toContainEqual({
          field: 'mappings[0].from',
          message: 'From value is required'
        })
        expect(result.errors).toContainEqual({
          field: 'mappings[0].to',
          message: 'To value is required'
        })
        expect(result.errors).toContainEqual({
          field: 'mappings[1].to',
          message: 'To value is required'
        })
      })

      it('should validate source column exists in schema', () => {
        const transformation = {
          type: 'Map',
          name: 'Test Map',
          config: {
            sourceColumn: 'nonexistent',
            mappings: [{ from: 'old', to: 'new' }]
          }
        }
        const result = composable.validateTransformation(transformation, mockInputSchema)
        expect(result.isValid).toBe(false)
        expect(result.errors).toContainEqual({
          field: 'sourceColumn',
          message: "Column 'nonexistent' not found in input schema"
        })
      })

      it('should pass validation for valid map transformation', () => {
        const transformation = {
          type: 'Map',
          name: 'Test Map',
          config: {
            sourceColumn: 'name',
            mappings: [{ from: 'old', to: 'new' }]
          }
        }
        const result = composable.validateTransformation(transformation, mockInputSchema)
        expect(result.isValid).toBe(true)
        expect(result.errors).toEqual([])
      })
    })

    describe('Aggregation transformation validation', () => {
      it('should validate groupByColumns is required and not empty', () => {
        const transformation = {
          type: 'Aggregation',
          name: 'Test Aggregation',
          config: { aggregationType: 'SUM', resultColumn: 'total' }
        }
        const result = composable.validateTransformation(transformation, mockInputSchema)
        expect(result.isValid).toBe(false)
        expect(result.errors).toContainEqual({
          field: 'groupByColumns',
          message: 'At least one group by column is required'
        })
      })

      it('should validate aggregationType is required', () => {
        const transformation = {
          type: 'Aggregation',
          name: 'Test Aggregation',
          config: { groupByColumns: ['name'], resultColumn: 'total' }
        }
        const result = composable.validateTransformation(transformation, mockInputSchema)
        expect(result.isValid).toBe(false)
        expect(result.errors).toContainEqual({
          field: 'aggregationType',
          message: 'Aggregation type is required'
        })
      })

      it('should validate resultColumn is required', () => {
        const transformation = {
          type: 'Aggregation',
          name: 'Test Aggregation',
          config: { groupByColumns: ['name'], aggregationType: 'SUM' }
        }
        const result = composable.validateTransformation(transformation, mockInputSchema)
        expect(result.isValid).toBe(false)
        expect(result.errors).toContainEqual({
          field: 'resultColumn',
          message: 'Result column name is required'
        })
      })

      it('should validate aggregationColumn is required for non-count aggregations', () => {
        const transformation = {
          type: 'Aggregation',
          name: 'Test Aggregation',
          config: {
            groupByColumns: ['name'],
            aggregationType: 'SUM',
            resultColumn: 'total'
          }
        }
        const result = composable.validateTransformation(transformation, mockInputSchema)
        expect(result.isValid).toBe(false)
        expect(result.errors).toContainEqual({
          field: 'aggregationColumn',
          message: 'Aggregation column is required for this aggregation type'
        })
      })

      it('should not require aggregationColumn for count aggregation', () => {
        const transformation = {
          type: 'Aggregation',
          name: 'Test Aggregation',
          config: {
            groupByColumns: ['name'],
            aggregationType: 'count',
            resultColumn: 'count'
          }
        }
        const result = composable.validateTransformation(transformation, mockInputSchema)
        expect(result.isValid).toBe(true)
      })

      it('should validate groupByColumns exist in schema', () => {
        const transformation = {
          type: 'Aggregation',
          name: 'Test Aggregation',
          config: {
            groupByColumns: ['name', 'nonexistent'],
            aggregationType: 'SUM',
            aggregationColumn: 'salary',
            resultColumn: 'total'
          }
        }
        const result = composable.validateTransformation(transformation, mockInputSchema)
        expect(result.isValid).toBe(false)
        expect(result.errors).toContainEqual({
          field: 'groupByColumns',
          message: "Group by column 'nonexistent' not found in input schema"
        })
      })

      it('should validate aggregationColumn exists in schema', () => {
        const transformation = {
          type: 'Aggregation',
          name: 'Test Aggregation',
          config: {
            groupByColumns: ['name'],
            aggregationType: 'SUM',
            aggregationColumn: 'nonexistent',
            resultColumn: 'total'
          }
        }
        const result = composable.validateTransformation(transformation, mockInputSchema)
        expect(result.isValid).toBe(false)
        expect(result.errors).toContainEqual({
          field: 'aggregationColumn',
          message: "Aggregation column 'nonexistent' not found in input schema"
        })
      })

      it('should pass validation for valid aggregation transformation', () => {
        const transformation = {
          type: 'Aggregation',
          name: 'Test Aggregation',
          config: {
            groupByColumns: ['name'],
            aggregationType: 'SUM',
            aggregationColumn: 'salary',
            resultColumn: 'total'
          }
        }
        const result = composable.validateTransformation(transformation, mockInputSchema)
        expect(result.isValid).toBe(true)
        expect(result.errors).toEqual([])
      })
    })

    describe('Script transformation validation', () => {
      it('should validate script is required', () => {
        const transformation = {
          type: 'Script',
          name: 'Test Script',
          config: {}
        }
        const result = composable.validateTransformation(transformation, mockInputSchema)
        expect(result.isValid).toBe(false)
        expect(result.errors).toContainEqual({
          field: 'script',
          message: 'Script is required'
        })
      })

      it('should validate script syntax', () => {
        const transformation = {
          type: 'Script',
          name: 'Test Script',
          config: { script: 'invalid syntax {{{' }
        }
        const result = composable.validateTransformation(transformation, mockInputSchema)
        expect(result.isValid).toBe(false)
        expect(result.errors[0].field).toBe('script')
        expect(result.errors[0].message).toContain('Script syntax error')
      })

      it('should pass validation for valid script', () => {
        const transformation = {
          type: 'Script',
          name: 'Test Script',
          config: { script: 'return row.name.toUpperCase();' }
        }
        const result = composable.validateTransformation(transformation, mockInputSchema)
        expect(result.isValid).toBe(true)
        expect(result.errors).toEqual([])
      })
    })
  })

  describe('getOutputSchema', () => {
    it('should return unchanged schema for Filter transformation', () => {
      const transformation = {
        type: 'Filter',
        config: { filterColumn: 'name', operator: 'equals', value: 'test' }
      }
      const result = composable.getOutputSchema(transformation, mockInputSchema)
      expect(result).toEqual(mockInputSchema)
    })

    it('should handle Map transformation schema changes', () => {
      const transformation = {
        type: 'Map',
        config: {
          sourceColumn: 'name',
          targetColumn: 'mapped_name',
          mapping: { type: 'varchar' }
        }
      }
      const result = composable.getOutputSchema(transformation, mockInputSchema)

      // The Map transformation should add a new column or update existing
      expect(result.columns.length).toBeGreaterThanOrEqual(mockInputSchema.columns.length)
      const mappedColumn = result.columns.find(c => c.name === 'mapped_name')
      expect(mappedColumn).toBeDefined()
      expect(mappedColumn.type).toBe('varchar')
    })

    it('should handle Join transformation schema changes', () => {
      const transformation = {
        type: 'Join',
        config: {
          joinTable: 'departments',
          joinType: 'LEFT',
          joinConditions: [{ leftColumn: 'id', rightColumn: 'user_id' }],
          joinTableSchema: mockJoinTableSchema
        }
      }
      const result = composable.getOutputSchema(transformation, mockInputSchema)

      expect(result.columns.length).toBeGreaterThan(mockInputSchema.columns.length)
      expect(result.columns.find(c => c.name === 'departments_department')).toBeDefined()
    })

    it('should handle Aggregate transformation schema changes', () => {
      const transformation = {
        type: 'Aggregate',
        config: {
          groupByColumns: ['name'],
          aggregations: [{ type: 'SUM', column: 'salary', alias: 'total_salary' }]
        }
      }
      const result = composable.getOutputSchema(transformation, mockInputSchema)

      expect(result.columns).toHaveLength(2) // name + total_salary
      expect(result.columns.find(c => c.name === 'name')).toBeDefined()
      expect(result.columns.find(c => c.name === 'total_salary')).toEqual({
        name: 'total_salary',
        type: 'decimal',
        nullable: false
      })
    })

    it('should handle Script transformation with declared output schema', () => {
      const transformation = {
        type: 'Script',
        config: {
          script: 'return row;',
          outputSchema: {
            columns: [
              { name: 'processed_name', type: 'varchar', nullable: false },
              { name: 'processed_age', type: 'int', nullable: true }
            ]
          }
        }
      }
      const result = composable.getOutputSchema(transformation, mockInputSchema)

      expect(result.columns).toEqual(transformation.config.outputSchema.columns)
    })

    it('should throw error for unknown transformation type', () => {
      const transformation = { type: 'Unknown' }
      expect(() => composable.getOutputSchema(transformation, mockInputSchema))
        .toThrow('Unknown transformation type: Unknown')
    })
  })

  describe('validateSchemaImpact', () => {
    it('should validate and cache schema impact successfully', async () => {
      const transformation = {
        id: 'test-transform',
        type: 'Filter',
        config: { filterColumn: 'name', operator: 'equals', value: 'test' }
      }

      const result = await composable.validateSchemaImpact(transformation, mockInputSchema)

      expect(result.isValid).toBe(true)
      expect(result.outputSchema).toEqual(mockInputSchema)
    })

    it('should return validation errors for invalid transformations', async () => {
      const transformation = {
        id: 'test-transform',
        type: 'Map',
        config: {
          sourceColumn: 'nonexistent',
          targetColumn: 'target',
          mapping: {}
        }
      }

      const result = await composable.validateSchemaImpact(transformation, mockInputSchema)

      expect(result.isValid).toBe(false)
      expect(result.errors).toContain("Source column 'nonexistent' not found in schema")
    })
  })

  describe('clearSchemaCache', () => {
    it('should clear the schema cache', () => {
      // Test that the function exists and can be called
      expect(typeof composable.clearSchemaCache).toBe('function')
      composable.clearSchemaCache()
    })
  })

  describe('determineTargetType', () => {
    // This is a private function, but we can test it indirectly through getOutputSchema
    it('should determine correct target types for mappings', () => {
      const transformation = {
        type: 'Map',
        config: {
          sourceColumn: 'name',
          targetColumn: 'mapped_name',
          mapping: {}
        }
      }
      const result = composable.getOutputSchema(transformation, mockInputSchema)
      const mappedColumn = result.columns.find(c => c.name === 'mapped_name')
      expect(mappedColumn.type).toBe('varchar') // default for varchar source
    })
  })

  describe('determineAggregationType', () => {
    // Test indirectly through getOutputSchema
    it('should determine correct types for aggregations', () => {
      const transformation = {
        type: 'Aggregate',
        config: {
          groupByColumns: ['name'],
          aggregations: [
            { type: 'COUNT', column: 'id', alias: 'count' },
            { type: 'SUM', column: 'salary', alias: 'total' },
            { type: 'AVG', column: 'age', alias: 'average' }
          ]
        }
      }
      const result = composable.getOutputSchema(transformation, mockInputSchema)

      expect(result.columns.find(c => c.name === 'count').type).toBe('int')
      expect(result.columns.find(c => c.name === 'total').type).toBe('decimal')
      expect(result.columns.find(c => c.name === 'average').type).toBe('decimal')
    })
  })
})

describe('isTypeCompatible', () => {
  describe('Implicit conversions', () => {
    it('should allow compatible implicit conversions', () => {
      expect(isTypeCompatible('int', 'decimal')).toBe(true)
      expect(isTypeCompatible('decimal', 'varchar')).toBe(true)
      expect(isTypeCompatible('varchar', 'varchar')).toBe(true)
      expect(isTypeCompatible('boolean', 'int')).toBe(true)
    })

    it('should reject incompatible implicit conversions', () => {
      expect(isTypeCompatible('varchar', 'int')).toBe(false)
      expect(isTypeCompatible('datetime', 'int')).toBe(false)
      expect(isTypeCompatible('decimal', 'datetime')).toBe(false)
    })
  })

  describe('Explicit conversions', () => {
    it('should allow compatible explicit conversions', () => {
      expect(isTypeCompatible('varchar', 'int', 'explicit')).toBe(true)
      expect(isTypeCompatible('int', 'boolean', 'explicit')).toBe(true)
      expect(isTypeCompatible('datetime', 'varchar', 'explicit')).toBe(true)
    })

    it('should reject incompatible explicit conversions', () => {
      expect(isTypeCompatible('boolean', 'datetime', 'explicit')).toBe(false)
    })
  })

  describe('Edge cases', () => {
    it('should handle unknown source types', () => {
      expect(isTypeCompatible('unknown', 'varchar')).toBe(false)
    })

    it('should handle unknown target types', () => {
      expect(isTypeCompatible('int', 'unknown')).toBe(false)
    })

    it('should default to implicit conversion', () => {
      expect(isTypeCompatible('int', 'decimal')).toBe(true)
      expect(isTypeCompatible('varchar', 'int')).toBe(false)
    })
  })
})