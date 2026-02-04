<template>
  <v-dialog
    :model-value="modelValue"
    max-width="700px"
    persistent
    @update:model-value="$emit('update:modelValue', $event)"
  >
    <v-card>
      <v-card-title class="d-flex align-center">
        {{ isEditing ? $t('schedules.editSchedule') : $t('schedules.createSchedule') }}
        <v-spacer />
        <v-btn
          icon
          variant="text"
          @click="close"
        >
          <v-icon>mdi-close</v-icon>
        </v-btn>
      </v-card-title>
      <v-card-text>
        <v-form ref="formRef" @submit.prevent="save">
          <v-row>
            <!-- Pipeline Selection (only for new schedules without pre-selected pipeline) -->
            <v-col v-if="!isEditing && !pipelineId && showPipelineSelector" cols="12">
              <v-autocomplete
                v-model="form.pipelineId"
                :label="$t('schedules.selectPipeline')"
                :items="pipelines"
                item-title="name"
                item-value="id"
                :rules="[v => !!v || $t('schedules.validation.pipelineRequired')]"
                :loading="loadingPipelines"
                prepend-inner-icon="mdi-pipe"
              />
            </v-col>

            <!-- Pipeline Name (read-only display for existing schedules or pre-selected pipeline) -->
            <v-col v-else-if="pipelineName || form.pipelineName" cols="12">
              <div class="text-caption text-medium-emphasis mb-1">{{ $t('schedules.pipeline') }}</div>
              <div class="d-flex align-center pa-3 rounded border">
                <v-icon class="mr-2" color="primary">mdi-pipe</v-icon>
                <span class="text-body-1">{{ pipelineName || form.pipelineName }}</span>
              </div>
            </v-col>

            <!-- Cron Expression with Presets -->
            <v-col cols="12" md="6">
              <v-text-field
                v-model="form.cronExpression"
                :label="$t('schedules.cronExpression')"
                :hint="$t('schedules.cronExpressionHint')"
                :rules="cronRules"
                :error-messages="cronValidation.isValid === false ? [cronValidation.errorMessage] : []"
                persistent-hint
                @update:model-value="debouncedValidateCron"
              />
            </v-col>

            <v-col cols="12" md="6">
              <v-select
                v-model="selectedPreset"
                :label="$t('schedules.presets.title')"
                :items="translatedPresets"
                item-title="title"
                item-value="value"
                clearable
                @update:model-value="applyPreset"
              />
            </v-col>

            <!-- Cron Validation Feedback -->
            <v-col v-if="cronValidation.isValid && cronValidation.description" cols="12">
              <v-alert type="success" variant="tonal" density="compact">
                <div class="font-weight-medium">{{ cronValidation.description }}</div>
                <div v-if="cronValidation.nextExecutions?.length" class="mt-2">
                  <div class="text-caption text-grey-darken-1">{{ $t('schedules.nextExecutions') }}:</div>
                  <ul class="text-caption ml-4">
                    <li v-for="(exec, idx) in cronValidation.nextExecutions.slice(0, 3)" :key="idx">
                      {{ formatDate(exec) }}
                    </li>
                  </ul>
                </div>
              </v-alert>
            </v-col>

            <v-col v-if="cronValidation.isValid === false && cronValidation.errorMessage" cols="12">
              <v-alert type="error" variant="tonal" density="compact">
                {{ cronValidation.errorMessage }}
              </v-alert>
            </v-col>

            <!-- Timezone -->
            <v-col cols="12" md="6">
              <v-autocomplete
                v-model="form.timezone"
                :label="$t('schedules.timezone')"
                :items="timezoneItems"
                item-title="title"
                item-value="value"
                :rules="[v => !!v || $t('schedules.validation.timezoneRequired')]"
                @update:model-value="debouncedValidateCron"
              />
            </v-col>

            <!-- Active Status -->
            <v-col cols="12" md="6">
              <v-switch
                v-model="form.isActive"
                :label="form.isActive ? $t('schedules.enabled') : $t('schedules.disabled')"
                color="success"
              />
            </v-col>

            <!-- Description -->
            <v-col cols="12">
              <v-textarea
                v-model="form.description"
                :label="$t('schedules.description')"
                :placeholder="$t('schedules.descriptionPlaceholder')"
                rows="2"
                counter="500"
                :rules="[v => !v || v.length <= 500 || 'Max 500 characters']"
              />
            </v-col>
          </v-row>
        </v-form>
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn
          variant="text"
          @click="close"
        >
          {{ $t('common.cancel') }}
        </v-btn>
        <v-btn
          color="primary"
          :loading="saving"
          :disabled="cronValidation.isValid === false"
          @click="save"
        >
          {{ $t('common.save') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup>
import { ref, computed, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useSchedule } from '@/composables/useSchedule'

const props = defineProps({
  modelValue: {
    type: Boolean,
    default: false
  },
  schedule: {
    type: Object,
    default: null
  },
  pipelineId: {
    type: [String, Number],
    default: null
  },
  pipelineName: {
    type: String,
    default: ''
  },
  pipelines: {
    type: Array,
    default: () => []
  },
  loadingPipelines: {
    type: Boolean,
    default: false
  },
  showPipelineSelector: {
    type: Boolean,
    default: true
  }
})

const emit = defineEmits(['update:modelValue', 'save', 'saved'])

const { t } = useI18n()
const { 
  cronPresets, 
  commonTimezones, 
  cronValidation, 
  validateCron,
  saveNewSchedule,
  saveSchedule
} = useSchedule()

const formRef = ref(null)
const saving = ref(false)
const selectedPreset = ref(null)

// Form data
const form = ref(getDefaultForm())

function getDefaultForm() {
  return {
    id: null,
    pipelineId: props.pipelineId || null,
    pipelineName: props.pipelineName || '',
    cronExpression: '0 0 0 * * ?',
    timezone: Intl.DateTimeFormat().resolvedOptions().timeZone,
    isActive: true,
    description: ''
  }
}

// Computed
const isEditing = computed(() => !!form.value.id)

// commonTimezones is already an array of { title, value } objects from useSchedule
const timezoneItems = computed(() => commonTimezones)

const translatedPresets = computed(() => {
  return cronPresets.map(p => ({
    ...p,
    title: p.titleKey ? t(p.titleKey) : p.text
  }))
})

const cronRules = [
  v => !!v || t('schedules.validation.cronRequired'),
  v => {
    // Basic format validation for 6-field Quartz cron
    const parts = (v || '').trim().split(/\s+/)
    return parts.length === 6 || t('schedules.validation.cronFormat')
  }
]

// Debounced cron validation
let cronValidationTimeout = null
const debouncedValidateCron = () => {
  if (cronValidationTimeout) clearTimeout(cronValidationTimeout)
  cronValidationTimeout = setTimeout(() => {
    if (form.value.cronExpression && form.value.timezone) {
      validateCron(form.value.cronExpression, form.value.timezone)
    }
  }, 500)
}

// Apply preset
const applyPreset = (presetValue) => {
  if (presetValue) {
    form.value.cronExpression = presetValue
    debouncedValidateCron()
  }
}

// Format date for display
const formatDate = (dateStr) => {
  if (!dateStr) return ''
  const date = new Date(dateStr)
  return date.toLocaleString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}

// Close dialog
const close = () => {
  emit('update:modelValue', false)
  resetForm()
}

// Reset form
const resetForm = () => {
  form.value = getDefaultForm()
  selectedPreset.value = null
  cronValidation.value = { isValid: null }
}

// Save schedule
const save = async () => {
  const { valid } = await formRef.value?.validate() || {}
  if (!valid) return
  
  if (cronValidation.value.isValid === false) return

  saving.value = true
  
  try {
    const scheduleData = {
      pipelineId: form.value.pipelineId || props.pipelineId,
      cronExpression: form.value.cronExpression,
      timezone: form.value.timezone,
      isActive: form.value.isActive,
      description: form.value.description || ''
    }

    let result
    if (isEditing.value) {
      result = await saveSchedule(form.value.id, scheduleData)
    } else {
      result = await saveNewSchedule(scheduleData)
    }
    
    emit('saved', result)
    close()
  } catch (error) {
    console.error('Failed to save schedule:', error)
    // Error handling is done in the composable
  } finally {
    saving.value = false
  }
}

// Watch for schedule prop changes (when editing)
watch(() => props.schedule, (newSchedule) => {
  if (newSchedule) {
    form.value = {
      id: newSchedule.id,
      pipelineId: newSchedule.pipelineId,
      pipelineName: newSchedule.pipelineName || '',
      cronExpression: newSchedule.cronExpression || '0 0 0 * * ?',
      timezone: newSchedule.timezone || Intl.DateTimeFormat().resolvedOptions().timeZone,
      isActive: newSchedule.isActive ?? true,
      description: newSchedule.description || ''
    }
    // Set the preset if the cron expression matches one
    const matchingPreset = cronPresets.find(p => p.value === form.value.cronExpression)
    selectedPreset.value = matchingPreset ? matchingPreset.value : null
    
    // Validate the loaded cron expression
    if (form.value.cronExpression && form.value.timezone) {
      validateCron(form.value.cronExpression, form.value.timezone)
    }
  } else {
    resetForm()
  }
}, { immediate: true })

// Watch for dialog open to set pipelineId if provided
watch(() => props.modelValue, (isOpen) => {
  if (isOpen && !props.schedule) {
    form.value.pipelineId = props.pipelineId
    form.value.pipelineName = props.pipelineName
    // Validate default cron
    if (form.value.cronExpression && form.value.timezone) {
      validateCron(form.value.cronExpression, form.value.timezone)
    }
  }
})
</script>
