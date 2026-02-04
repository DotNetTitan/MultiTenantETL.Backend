import { ref, computed } from 'vue'
import {
  fetchSchedules,
  fetchScheduleById,
  fetchScheduleByPipelineId,
  createSchedule,
  updateSchedule,
  deleteSchedule,
  enableSchedule,
  disableSchedule,
  triggerSchedule,
  validateCronExpression
} from '@/services/scheduleService'

/**
 * Composable for schedule management functionality
 * @returns {Object} Schedule composable functions and reactive data
 */
export function useSchedule() {
  // Reactive state
  const schedules = ref([])
  const currentSchedule = ref(null)
  const loading = ref(false)
  const saving = ref(false)
  const error = ref(null)
  const pagination = ref({
    page: 1,
    pageSize: 20,
    totalCount: 0,
    totalPages: 0
  })

  // Validation state
  const cronValidation = ref({
    isValid: null,
    errorMessage: null,
    description: null,
    nextExecutions: []
  })
  const validating = ref(false)

  // Get all IANA timezones from the browser's Intl API
  const allTimezones = (() => {
    try {
      return Intl.supportedValuesOf('timeZone').map(tz => ({
        title: tz.replace(/_/g, ' '),
        value: tz
      }))
    } catch {
      // Fallback for older browsers
      return [
        { title: 'UTC', value: 'UTC' },
        { title: 'America/New York', value: 'America/New_York' },
        { title: 'America/Chicago', value: 'America/Chicago' },
        { title: 'America/Denver', value: 'America/Denver' },
        { title: 'America/Los Angeles', value: 'America/Los_Angeles' },
        { title: 'Europe/London', value: 'Europe/London' },
        { title: 'Europe/Paris', value: 'Europe/Paris' },
        { title: 'Europe/Berlin', value: 'Europe/Berlin' },
        { title: 'Asia/Tokyo', value: 'Asia/Tokyo' },
        { title: 'Australia/Sydney', value: 'Australia/Sydney' }
      ]
    }
  })()

  // Common/popular timezones with friendly names (shown at the top)
  const popularTimezones = [
    { title: 'UTC', value: 'UTC' },
    { title: 'Eastern Time (ET)', value: 'America/New_York' },
    { title: 'Central Time (CT)', value: 'America/Chicago' },
    { title: 'Mountain Time (MT)', value: 'America/Denver' },
    { title: 'Pacific Time (PT)', value: 'America/Los_Angeles' },
    { title: 'London (GMT/BST)', value: 'Europe/London' },
    { title: 'Paris (CET/CEST)', value: 'Europe/Paris' },
    { title: 'Berlin (CET/CEST)', value: 'Europe/Berlin' },
    { title: 'Tokyo (JST)', value: 'Asia/Tokyo' },
    { title: 'Sydney (AEST/AEDT)', value: 'Australia/Sydney' },
    { title: 'São Paulo (BRT)', value: 'America/Sao_Paulo' },
    { title: 'Mumbai (IST)', value: 'Asia/Kolkata' },
    { title: 'Singapore (SGT)', value: 'Asia/Singapore' },
    { title: 'Hong Kong (HKT)', value: 'Asia/Hong_Kong' }
  ]

  // Combined list: popular timezones first, then all others
  const commonTimezones = [
    ...popularTimezones,
    { divider: true },
    ...allTimezones.filter(tz => !popularTimezones.some(p => p.value === tz.value))
  ]

  // Common cron presets for Quartz.NET (6-field format: seconds minutes hours day-of-month month day-of-week)
  // Use '?' in day-of-month OR day-of-week (not both, and not in other fields)
  const cronPresets = [
    { text: 'Every minute', value: '0 * * * * ?', titleKey: 'schedules.presets.everyMinute' },
    { text: 'Every 5 minutes', value: '0 */5 * * * ?', titleKey: 'schedules.presets.every5Minutes' },
    { text: 'Every 15 minutes', value: '0 */15 * * * ?', titleKey: 'schedules.presets.every15Minutes' },
    { text: 'Every 30 minutes', value: '0 */30 * * * ?', titleKey: 'schedules.presets.every30Minutes' },
    { text: 'Every hour', value: '0 0 * * * ?', titleKey: 'schedules.presets.everyHour' },
    { text: 'Every 2 hours', value: '0 0 */2 * * ?', titleKey: 'schedules.presets.every2Hours' },
    { text: 'Every 6 hours', value: '0 0 */6 * * ?', titleKey: 'schedules.presets.every6Hours' },
    { text: 'Every 12 hours', value: '0 0 */12 * * ?', titleKey: 'schedules.presets.every12Hours' },
    { text: 'Daily at midnight', value: '0 0 0 * * ?', titleKey: 'schedules.presets.dailyMidnight' },
    { text: 'Daily at 6 AM', value: '0 0 6 * * ?', titleKey: 'schedules.presets.dailyMorning' },
    { text: 'Daily at noon', value: '0 0 12 * * ?', titleKey: 'schedules.presets.dailyNoon' },
    { text: 'Daily at 6 PM', value: '0 0 18 * * ?', titleKey: 'schedules.presets.dailyEvening' },
    { text: 'Weekly on Monday at midnight', value: '0 0 0 ? * MON', titleKey: 'schedules.presets.weeklyMonday' },
    { text: 'Weekly on Sunday at midnight', value: '0 0 0 ? * SUN', titleKey: 'schedules.presets.weeklySunday' },
    { text: 'First day of month at midnight', value: '0 0 0 1 * ?', titleKey: 'schedules.presets.monthlyFirst' },
    { text: 'Last day of month at midnight', value: '0 0 0 L * ?', titleKey: 'schedules.presets.lastDayOfMonth' }
  ]

  // Computed properties
  const hasSchedules = computed(() => schedules.value.length > 0)
  const activeSchedules = computed(() => schedules.value.filter(s => s.isActive))
  const inactiveSchedules = computed(() => schedules.value.filter(s => !s.isActive))

  /**
   * Load all schedules with optional filters
   * @param {Object} params - Filter parameters
   */
  async function loadSchedules(params = {}) {
    loading.value = true
    error.value = null

    try {
      const result = await fetchSchedules({
        ...params,
        page: params.page || pagination.value.page,
        pageSize: params.pageSize || pagination.value.pageSize
      })

      schedules.value = result.schedules || []
      pagination.value = {
        page: result.page || 1,
        pageSize: result.pageSize || 20,
        totalCount: result.totalCount || 0,
        totalPages: result.totalPages || 0
      }
    } catch (err) {
      error.value = err.message || 'Failed to load schedules'
      console.error('Error loading schedules:', err)
    } finally {
      loading.value = false
    }
  }

  /**
   * Load a single schedule by ID
   * @param {string} id - Schedule ID
   */
  async function loadSchedule(id) {
    loading.value = true
    error.value = null

    try {
      currentSchedule.value = await fetchScheduleById(id)
      return currentSchedule.value
    } catch (err) {
      error.value = err.message || 'Failed to load schedule'
      console.error('Error loading schedule:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Load schedule for a specific pipeline
   * @param {string} pipelineId - Pipeline ID
   */
  async function loadScheduleForPipeline(pipelineId) {
    loading.value = true
    error.value = null

    try {
      currentSchedule.value = await fetchScheduleByPipelineId(pipelineId)
      return currentSchedule.value
    } catch (err) {
      error.value = err.message || 'Failed to load pipeline schedule'
      console.error('Error loading pipeline schedule:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Create a new schedule
   * @param {Object} scheduleData - Schedule data
   */
  async function saveNewSchedule(scheduleData) {
    saving.value = true
    error.value = null

    try {
      const created = await createSchedule(scheduleData)
      schedules.value.push(created)
      return created
    } catch (err) {
      error.value = err.response?.data?.message || err.message || 'Failed to create schedule'
      throw err
    } finally {
      saving.value = false
    }
  }

  /**
   * Update an existing schedule
   * @param {string} id - Schedule ID
   * @param {Object} scheduleData - Schedule data
   */
  async function saveSchedule(id, scheduleData) {
    saving.value = true
    error.value = null

    try {
      const updated = await updateSchedule(id, scheduleData)
      
      // Update in list
      const index = schedules.value.findIndex(s => s.id === id)
      if (index !== -1) {
        schedules.value[index] = updated
      }
      
      // Update current if it's the same
      if (currentSchedule.value?.id === id) {
        currentSchedule.value = updated
      }
      
      return updated
    } catch (err) {
      error.value = err.response?.data?.message || err.message || 'Failed to update schedule'
      throw err
    } finally {
      saving.value = false
    }
  }

  /**
   * Delete a schedule
   * @param {string} id - Schedule ID
   */
  async function removeSchedule(id) {
    saving.value = true
    error.value = null

    try {
      await deleteSchedule(id)
      
      // Remove from list
      schedules.value = schedules.value.filter(s => s.id !== id)
      
      // Clear current if it was deleted
      if (currentSchedule.value?.id === id) {
        currentSchedule.value = null
      }
    } catch (err) {
      error.value = err.response?.data?.message || err.message || 'Failed to delete schedule'
      throw err
    } finally {
      saving.value = false
    }
  }

  /**
   * Enable a schedule
   * @param {string} id - Schedule ID
   */
  async function activateSchedule(id) {
    saving.value = true
    error.value = null

    try {
      const updated = await enableSchedule(id)
      
      // Update in list
      const index = schedules.value.findIndex(s => s.id === id)
      if (index !== -1) {
        schedules.value[index] = updated
      }
      
      return updated
    } catch (err) {
      error.value = err.message || 'Failed to enable schedule'
      throw err
    } finally {
      saving.value = false
    }
  }

  /**
   * Disable a schedule
   * @param {string} id - Schedule ID
   */
  async function deactivateSchedule(id) {
    saving.value = true
    error.value = null

    try {
      const updated = await disableSchedule(id)
      
      // Update in list
      const index = schedules.value.findIndex(s => s.id === id)
      if (index !== -1) {
        schedules.value[index] = updated
      }
      
      return updated
    } catch (err) {
      error.value = err.message || 'Failed to disable schedule'
      throw err
    } finally {
      saving.value = false
    }
  }

  /**
   * Toggle a schedule's active state
   * @param {string} id - Schedule ID
   */
  async function toggleScheduleActive(id) {
    const schedule = schedules.value.find(s => s.id === id)
    if (!schedule) return

    return schedule.isActive ? deactivateSchedule(id) : activateSchedule(id)
  }

  /**
   * Trigger a schedule to run immediately
   * @param {string} id - Schedule ID
   */
  async function runScheduleNow(id) {
    saving.value = true
    error.value = null

    try {
      const updated = await triggerSchedule(id)
      
      // Update in list
      const index = schedules.value.findIndex(s => s.id === id)
      if (index !== -1) {
        schedules.value[index] = updated
      }
      
      return updated
    } catch (err) {
      error.value = err.message || 'Failed to trigger schedule'
      throw err
    } finally {
      saving.value = false
    }
  }

  /**
   * Validate a cron expression
   * @param {string} cronExpression - Cron expression to validate
   * @param {string} timezone - Timezone for the schedule
   */
  async function validateCron(cronExpression, timezone = 'UTC') {
    validating.value = true
    cronValidation.value = {
      isValid: null,
      errorMessage: null,
      description: null,
      nextExecutions: []
    }

    try {
      const result = await validateCronExpression(cronExpression, timezone)
      cronValidation.value = result
      return result
    } catch (err) {
      cronValidation.value = {
        isValid: false,
        errorMessage: err.message || 'Failed to validate cron expression',
        description: null,
        nextExecutions: []
      }
      return cronValidation.value
    } finally {
      validating.value = false
    }
  }

  /**
   * Get status color for a schedule
   * @param {Object} schedule - Schedule object
   */
  function getStatusColor(schedule) {
    if (!schedule.isActive) return 'grey'
    if (schedule.lastRunStatus === 'Failed') return 'error'
    if (schedule.lastRunStatus === 'Completed') return 'success'
    if (schedule.consecutiveFailures > 0) return 'warning'
    return 'success'
  }

  /**
   * Get status icon for a schedule
   * @param {Object} schedule - Schedule object
   */
  function getStatusIcon(schedule) {
    if (!schedule.isActive) return 'mdi-pause-circle-outline'
    if (schedule.lastRunStatus === 'Failed') return 'mdi-alert-circle'
    if (schedule.lastRunStatus === 'Completed') return 'mdi-check-circle'
    if (schedule.consecutiveFailures > 0) return 'mdi-alert'
    return 'mdi-timer-outline'
  }

  /**
   * Format a date for display
   * @param {string} dateString - ISO date string
   */
  function formatDate(dateString) {
    if (!dateString) return '-'
    return new Date(dateString).toLocaleString()
  }

  /**
   * Format relative time
   * @param {string} dateString - ISO date string
   */
  function formatRelativeTime(dateString) {
    if (!dateString) return '-'
    
    const date = new Date(dateString)
    const now = new Date()
    const diffMs = date - now
    const diffMins = Math.round(diffMs / 60000)
    const diffHours = Math.round(diffMs / 3600000)
    const diffDays = Math.round(diffMs / 86400000)

    if (diffMs < 0) {
      // Past
      if (diffMins > -60) return `${Math.abs(diffMins)} minutes ago`
      if (diffHours > -24) return `${Math.abs(diffHours)} hours ago`
      return `${Math.abs(diffDays)} days ago`
    } else {
      // Future
      if (diffMins < 60) return `in ${diffMins} minutes`
      if (diffHours < 24) return `in ${diffHours} hours`
      return `in ${diffDays} days`
    }
  }

  /**
   * Create an empty schedule object
   */
  function createEmptySchedule() {
    return {
      pipelineId: null,
      cronExpression: '0 0 0 * * ?', // Daily at midnight (Quartz 6-field: sec min hour day month weekday)
      timezone: 'UTC',
      description: '',
      isActive: true
    }
  }

  /**
   * Reset state
   */
  function reset() {
    schedules.value = []
    currentSchedule.value = null
    error.value = null
    pagination.value = {
      page: 1,
      pageSize: 20,
      totalCount: 0,
      totalPages: 0
    }
    cronValidation.value = {
      isValid: null,
      errorMessage: null,
      description: null,
      nextExecutions: []
    }
  }

  return {
    // State
    schedules,
    currentSchedule,
    loading,
    saving,
    error,
    pagination,
    cronValidation,
    validating,

    // Constants
    commonTimezones,
    allTimezones,
    cronPresets,

    // Computed
    hasSchedules,
    activeSchedules,
    inactiveSchedules,

    // Methods
    loadSchedules,
    loadSchedule,
    loadScheduleForPipeline,
    saveNewSchedule,
    saveSchedule,
    removeSchedule,
    activateSchedule,
    deactivateSchedule,
    toggleScheduleActive,
    runScheduleNow,
    validateCron,
    getStatusColor,
    getStatusIcon,
    formatDate,
    formatRelativeTime,
    createEmptySchedule,
    reset
  }
}
