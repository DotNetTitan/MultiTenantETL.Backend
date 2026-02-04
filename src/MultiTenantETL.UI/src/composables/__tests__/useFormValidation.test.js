import { describe, it, expect, beforeEach, vi } from 'vitest'
import { useFormValidation, required, email, minLength, maxLength, numeric, pattern } from '@/composables/useFormValidation'

describe('useFormValidation', () => {
  let validation

  beforeEach(() => {
    validation = useFormValidation()
  })

  describe('initial state', () => {
    it('should initialize with empty errors and not submitting', () => {
      expect(validation.errors.value).toEqual({})
      expect(validation.isSubmitting.value).toBe(false)
      expect(validation.hasErrors.value).toBe(false)
    })
  })

  describe('validateField', () => {
    it('should validate field with no rules successfully', () => {
      const result = validation.validateField('testField', 'test value', [])

      expect(result).toBe(true)
      expect(validation.errors.value.testField).toBeUndefined()
      expect(validation.hasErrors.value).toBe(false)
    })

    it('should validate field with passing rules', () => {
      const rules = [
        (value) => value.length > 0 ? null : 'Required',
        (value) => value.length >= 3 ? null : 'Too short'
      ]

      const result = validation.validateField('testField', 'valid value', rules)

      expect(result).toBe(true)
      expect(validation.errors.value.testField).toBeUndefined()
      expect(validation.hasErrors.value).toBe(false)
    })

    it('should validate field with failing rules', () => {
      const rules = [
        (value) => value.length > 0 ? null : 'Required',
        (value) => value.length >= 5 ? null : 'Too short'
      ]

      const result = validation.validateField('testField', 'hi', rules)

      expect(result).toBe(false)
      expect(validation.errors.value.testField).toEqual(['Too short'])
      expect(validation.hasErrors.value).toBe(true)
    })

    it('should validate field with multiple failing rules', () => {
      const rules = [
        (value) => value.length > 0 ? null : 'Required',
        (value) => value.length >= 5 ? null : 'Too short',
        (value) => /^[A-Z]/.test(value) ? null : 'Must start with capital letter'
      ]

      const result = validation.validateField('testField', 'hello', rules)

      expect(result).toBe(false)
      expect(validation.errors.value.testField).toEqual(['Must start with capital letter'])
      expect(validation.hasErrors.value).toBe(true)
    })

    it('should clear previous errors when field becomes valid', () => {
      // First make it invalid
      validation.validateField('testField', '', [(value) => value ? null : 'Required'])
      expect(validation.errors.value.testField).toEqual(['Required'])

      // Then make it valid
      const result = validation.validateField('testField', 'valid', [(value) => value ? null : 'Required'])

      expect(result).toBe(true)
      expect(validation.errors.value.testField).toBeUndefined()
      expect(validation.hasErrors.value).toBe(false)
    })
  })

  describe('validateForm', () => {
    it('should validate form with all valid fields', () => {
      const fields = {
        name: {
          value: 'John Doe',
          rules: [(value) => value.length > 0 ? null : 'Required']
        },
        email: {
          value: 'john@example.com',
          rules: [
            (value) => value.length > 0 ? null : 'Required',
            (value) => value.includes('@') ? null : 'Invalid email'
          ]
        }
      }

      const result = validation.validateForm(fields)

      expect(result).toBe(true)
      expect(validation.errors.value).toEqual({})
      expect(validation.hasErrors.value).toBe(false)
    })

    it('should validate form with some invalid fields', () => {
      const fields = {
        name: {
          value: '', // Invalid
          rules: [(value) => value.length > 0 ? null : 'Required']
        },
        email: {
          value: 'john@example.com', // Valid
          rules: [
            (value) => value.length > 0 ? null : 'Required',
            (value) => value.includes('@') ? null : 'Invalid email'
          ]
        }
      }

      const result = validation.validateForm(fields)

      expect(result).toBe(false)
      expect(validation.errors.value).toEqual({
        name: ['Required']
      })
      expect(validation.hasErrors.value).toBe(true)
    })

    it('should validate form with all invalid fields', () => {
      const fields = {
        name: {
          value: '', // Invalid
          rules: [(value) => value.length > 0 ? null : 'Required']
        },
        email: {
          value: 'invalid-email', // Invalid
          rules: [
            (value) => value.length > 0 ? null : 'Required',
            (value) => value.includes('@') ? null : 'Invalid email'
          ]
        }
      }

      const result = validation.validateForm(fields)

      expect(result).toBe(false)
      expect(validation.errors.value).toEqual({
        name: ['Required'],
        email: ['Invalid email']
      })
      expect(validation.hasErrors.value).toBe(true)
    })

    it('should clear previous errors before validation', () => {
      // Set some initial errors
      validation.errors.value = { oldField: ['Old error'] }

      const fields = {
        name: {
          value: 'John Doe',
          rules: [(value) => value.length > 0 ? null : 'Required']
        }
      }

      validation.validateForm(fields)

      expect(validation.errors.value.oldField).toBeUndefined()
      expect(validation.hasErrors.value).toBe(false)
    })
  })

  describe('clearErrors', () => {
    beforeEach(() => {
      validation.errors.value = {
        field1: ['Error 1'],
        field2: ['Error 2'],
        field3: ['Error 3']
      }
    })

    it('should clear errors for specific field', () => {
      validation.clearErrors('field2')

      expect(validation.errors.value).toEqual({
        field1: ['Error 1'],
        field3: ['Error 3']
      })
      expect(validation.hasErrors.value).toBe(true)
    })

    it('should clear all errors when no field specified', () => {
      validation.clearErrors()

      expect(validation.errors.value).toEqual({})
      expect(validation.hasErrors.value).toBe(false)
    })
  })

  describe('handleSubmit', () => {
    it('should handle successful form submission', async () => {
      const mockSubmitFn = vi.fn().mockResolvedValue('success')

      const result = await validation.handleSubmit(mockSubmitFn)

      expect(validation.isSubmitting.value).toBe(false)
      expect(mockSubmitFn).toHaveBeenCalled()
      expect(result).toBe(true)
    })

    it('should handle form submission with validation errors', async () => {
      const mockSubmitFn = vi.fn().mockRejectedValue({
        response: {
          data: {
            errors: { name: ['Required'] }
          }
        }
      })

      const result = await validation.handleSubmit(mockSubmitFn)

      expect(validation.isSubmitting.value).toBe(false)
      expect(mockSubmitFn).toHaveBeenCalled()
      expect(result).toBe(false)
      expect(validation.errors.value).toEqual({ name: ['Required'] })
    })

    it('should handle form submission errors', async () => {
      const mockSubmitFn = vi.fn().mockRejectedValue(new Error('Submit failed'))

      const result = await validation.handleSubmit(mockSubmitFn)

      expect(validation.isSubmitting.value).toBe(false)
      expect(mockSubmitFn).toHaveBeenCalled()
      expect(result).toBe(false)
      expect(validation.errors.value).toEqual({ _form: ['Submit failed'] })
    })

    it('should set isSubmitting during submission', async () => {
      const mockSubmitFn = vi.fn().mockImplementation(() => {
        expect(validation.isSubmitting.value).toBe(true)
        return Promise.resolve('success')
      })

      await validation.handleSubmit(mockSubmitFn)

      expect(validation.isSubmitting.value).toBe(false)
    })

    it('should clear errors before submission', async () => {
      // Set some initial errors
      validation.errors.value = { oldField: ['Old error'] }
      const mockSubmitFn = vi.fn().mockResolvedValue('success')

      await validation.handleSubmit(mockSubmitFn)

      expect(validation.errors.value).toEqual({})
    })

    it('should handle API error with message', async () => {
      const mockSubmitFn = vi.fn().mockRejectedValue({
        response: {
          data: {
            message: 'API Error Message'
          }
        }
      })

      const result = await validation.handleSubmit(mockSubmitFn)

      expect(result).toBe(false)
      expect(validation.errors.value).toEqual({ _form: ['API Error Message'] })
    })
  })

  describe('hasErrors computed property', () => {
    it('should be true when there are errors', () => {
      validation.errors.value = { field1: ['Error'] }
      expect(validation.hasErrors.value).toBe(true)
    })

    it('should be false when there are no errors', () => {
      validation.errors.value = {}
      expect(validation.hasErrors.value).toBe(false)
    })

    it('should update reactively when errors change', () => {
      expect(validation.hasErrors.value).toBe(false)

      validation.errors.value = { field1: ['Error'] }
      expect(validation.hasErrors.value).toBe(true)

      validation.errors.value = {}
      expect(validation.hasErrors.value).toBe(false)
    })
  })
})

describe('Validation Rules', () => {
  describe('required', () => {
    it('should return error for null value', () => {
      expect(required(null)).toBe('This field is required')
    })

    it('should return error for undefined value', () => {
      expect(required(undefined)).toBe('This field is required')
    })

    it('should return error for empty string', () => {
      expect(required('')).toBe('This field is required')
    })

    it('should return error for empty array', () => {
      expect(required([])).toBe('This field is required')
    })

    it('should return null for valid values', () => {
      expect(required('test')).toBe(null)
      expect(required(0)).toBe(null)
      expect(required(false)).toBe(null)
      expect(required(['item'])).toBe(null)
    })
  })

  describe('email', () => {
    it('should return null for empty value', () => {
      expect(email('')).toBe(null)
      expect(email(null)).toBe(null)
      expect(email(undefined)).toBe(null)
    })

    it('should return null for valid email', () => {
      expect(email('test@example.com')).toBe(null)
      expect(email('user.name+tag@domain.co.uk')).toBe(null)
    })

    it('should return error for invalid email', () => {
      expect(email('invalid')).toBe('Please enter a valid email address')
      expect(email('test@')).toBe('Please enter a valid email address')
      expect(email('@example.com')).toBe('Please enter a valid email address')
      expect(email('test.example.com')).toBe('Please enter a valid email address')
    })
  })

  describe('minLength', () => {
    const rule = minLength(3)

    it('should return null for empty value', () => {
      expect(rule('')).toBe(null)
      expect(rule(null)).toBe(null)
    })

    it('should return null for valid length', () => {
      expect(rule('abc')).toBe(null)
      expect(rule('abcd')).toBe(null)
    })

    it('should return error for short value', () => {
      expect(rule('ab')).toBe('Must be at least 3 characters')
      expect(rule('a')).toBe('Must be at least 3 characters')
    })

    it('should convert number to string', () => {
      expect(rule(123)).toBe(null)
      expect(rule(12)).toBe('Must be at least 3 characters')
    })
  })

  describe('maxLength', () => {
    const rule = maxLength(5)

    it('should return null for empty value', () => {
      expect(rule('')).toBe(null)
      expect(rule(null)).toBe(null)
    })

    it('should return null for valid length', () => {
      expect(rule('abc')).toBe(null)
      expect(rule('abcde')).toBe(null)
    })

    it('should return error for long value', () => {
      expect(rule('abcdef')).toBe('Must not exceed 5 characters')
    })

    it('should convert number to string', () => {
      expect(rule(12345)).toBe(null)
      expect(rule(123456)).toBe('Must not exceed 5 characters')
    })
  })

  describe('numeric', () => {
    it('should return null for empty value', () => {
      expect(numeric('')).toBe(null)
      expect(numeric(null)).toBe(null)
    })

    it('should return null for valid numbers', () => {
      expect(numeric('123')).toBe(null)
      expect(numeric('123.45')).toBe(null)
      expect(numeric('-123')).toBe(null)
      expect(numeric(123)).toBe(null)
    })

    it('should return error for non-numeric values', () => {
      expect(numeric('abc')).toBe('Must be a number')
      expect(numeric('12a')).toBe('Must be a number')
      expect(numeric('12.34.56')).toBe('Must be a number')
    })
  })

  describe('pattern', () => {
    const rule = pattern(/^\d{3}-\d{2}-\d{4}$/, 'Invalid SSN format')

    it('should return null for empty value', () => {
      expect(rule('')).toBe(null)
      expect(rule(null)).toBe(null)
    })

    it('should return null for matching pattern', () => {
      expect(rule('123-45-6789')).toBe(null)
    })

    it('should return custom error message for non-matching pattern', () => {
      expect(rule('invalid')).toBe('Invalid SSN format')
    })

    it('should return default error message when no custom message', () => {
      const defaultRule = pattern(/^\d+$/)
      expect(defaultRule('abc')).toBe('Invalid format')
    })
  })
})