<template>
  <div>
    <div class="d-flex align-center mb-4">
      <h1 class="text-h4 mr-4">{{ $t('schedules.title') }}</h1>
      <v-spacer />
      <v-btn 
        color="primary" 
        @click="openCreateDialog"
      >
        <v-icon v-if="$vuetify.display.smAndUp" class="mr-2">mdi-plus</v-icon>
        <span v-if="$vuetify.display.xs">{{ $t('common.create') }}</span>
        <span v-else>{{ $t('schedules.createSchedule') }}</span>
      </v-btn>
    </div>

    <v-card>
      <v-card-text>
        <v-row>
          <v-col cols="12" md="4">
            <v-text-field
              v-model="search"
              :label="$t('schedules.searchSchedules')"
              prepend-inner-icon="mdi-magnify"
              density="compact"
              hide-details
              clearable
              class="mb-4"
              @update:model-value="debouncedFetch"
            />
          </v-col>
          <v-col cols="12" md="3">
            <v-select
              v-model="statusFilter"
              :label="$t('schedules.filterByStatus')"
              :items="statusOptions"
              density="compact"
              hide-details
              class="mb-4"
              @update:model-value="fetchSchedulesList"
            />
          </v-col>
          <v-col cols="12" md="3">
            <v-select
              v-model="sortBy"
              :label="$t('schedules.sortBy')"
              :items="sortOptions"
              density="compact"
              hide-details
              class="mb-4"
              @update:model-value="fetchSchedulesList"
            />
          </v-col>
          <v-col cols="12" md="2" class="d-flex align-center">
            <v-btn
              icon
              variant="text"
              :loading="loading"
              @click="fetchSchedulesList"
            >
              <v-icon>mdi-refresh</v-icon>
            </v-btn>
          </v-col>
        </v-row>

        <!-- Empty State -->
        <div v-if="!loading && schedules.length === 0" class="text-center py-8">
          <v-icon size="64" color="grey-lighten-1">mdi-calendar-clock</v-icon>
          <h3 class="text-h6 mt-4 text-grey">{{ $t('schedules.noSchedules') }}</h3>
          <p class="text-grey mt-2">{{ $t('schedules.noSchedulesDescription') }}</p>
          <v-btn color="primary" class="mt-4" @click="openCreateDialog">
            <v-icon class="mr-2">mdi-plus</v-icon>
            {{ $t('schedules.createSchedule') }}
          </v-btn>
        </div>

        <!-- Schedules Table -->
        <v-data-table
          v-else
          :headers="headers"
          :items="schedules"
          :loading="loading"
          :items-per-page="pagination.pageSize"
          class="mt-2"
        >
          <template #item.pipelineName="{ item }">
            <div class="d-flex align-center">
              <v-icon size="small" class="mr-2">mdi-pipe</v-icon>
              {{ item.pipelineName || 'Unknown Pipeline' }}
            </div>
          </template>

          <template #item.cronExpression="{ item }">
            <div>
              <code class="text-caption">{{ item.cronExpression }}</code>
              <div v-if="item.cronDescription" class="text-caption text-grey">
                {{ item.cronDescription }}
              </div>
            </div>
          </template>

          <template #item.timezone="{ item }">
            <v-chip size="small" variant="outlined">
              {{ item.timezone }}
            </v-chip>
          </template>

          <template #item.isActive="{ item }">
            <v-switch
              :model-value="item.isActive"
              color="success"
              hide-details
              density="compact"
              :loading="togglingId === item.id"
              @update:model-value="toggleActive(item)"
            />
          </template>

          <template #item.nextRunAt="{ item }">
            <div v-if="item.nextRunAt && item.isActive">
              <div>{{ formatDate(item.nextRunAt) }}</div>
              <div class="text-caption text-grey">
                {{ formatRelativeTime(item.nextRunAt) }}
              </div>
            </div>
            <span v-else class="text-grey">-</span>
          </template>

          <template #item.lastRunAt="{ item }">
            <div v-if="item.lastRunAt">
              <div>{{ formatDate(item.lastRunAt) }}</div>
              <v-chip
                v-if="item.lastRunStatus"
                :color="getStatusColor(item.lastRunStatus)"
                size="x-small"
                class="mt-1"
              >
                {{ item.lastRunStatus }}
              </v-chip>
            </div>
            <span v-else class="text-grey">{{ $t('pipelines.never') }}</span>
          </template>

          <template #item.actions="{ item }">
            <div class="d-flex flex-nowrap">
              <v-btn
                icon
                variant="text"
                size="small"
                :title="$t('schedules.trigger')"
                :loading="triggeringId === item.id"
                @click="triggerScheduleNow(item)"
              >
                <v-icon>mdi-play</v-icon>
              </v-btn>
              <v-btn
                icon
                variant="text"
                size="small"
                :title="$t('schedules.editSchedule')"
                @click="editSchedule(item)"
              >
                <v-icon>mdi-pencil</v-icon>
              </v-btn>
              <v-btn
                icon
                variant="text"
                size="small"
                color="error"
                :title="$t('schedules.deleteSchedule')"
                @click="confirmDelete(item)"
              >
                <v-icon>mdi-delete</v-icon>
              </v-btn>
            </div>
          </template>
        </v-data-table>

        <!-- Pagination -->
        <div v-if="pagination.totalPages > 1" class="d-flex justify-center mt-4">
          <v-pagination
            v-model="pagination.page"
            :length="pagination.totalPages"
            :total-visible="5"
            @update:model-value="fetchSchedulesList"
          />
        </div>
      </v-card-text>
    </v-card>

    <!-- Create/Edit Schedule Dialog -->
    <ScheduleDialog
      v-model="showDialog"
      :schedule="editingSchedule"
      :pipelines="pipelines"
      :loading-pipelines="loadingPipelines"
      @saved="onScheduleSaved"
    />

    <!-- Delete Confirmation Dialog -->
    <v-dialog
      v-model="showDeleteDialog"
      max-width="500px"
    >
      <v-card>
        <v-card-title>{{ $t('schedules.deleteSchedule') }}</v-card-title>
        <v-card-text>
          {{ $t('schedules.deleteConfirm') }}
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn
            variant="text"
            @click="showDeleteDialog = false"
          >
            {{ $t('common.cancel') }}
          </v-btn>
          <v-btn
            color="error"
            :loading="deleting"
            @click="deleteScheduleConfirmed"
          >
            {{ $t('common.delete') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Trigger Confirmation Dialog -->
    <v-dialog
      v-model="showTriggerDialog"
      max-width="500px"
    >
      <v-card>
        <v-card-title>{{ $t('schedules.trigger') }}</v-card-title>
        <v-card-text>
          {{ $t('schedules.triggerConfirm') }}
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn
            variant="text"
            @click="showTriggerDialog = false"
          >
            {{ $t('common.cancel') }}
          </v-btn>
          <v-btn
            color="primary"
            @click="triggerConfirmed"
          >
            {{ $t('schedules.trigger') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { useSchedule } from '@/composables/useSchedule'
import { useGlobalState } from '@/composables/useGlobalState'
import { fetchPipelines } from '@/services/pipelineService'
import ScheduleDialog from '@/components/schedules/ScheduleDialog.vue'

const { t } = useI18n()
const { showSuccess, showError } = useGlobalState()
const {
  schedules,
  loading,
  pagination,
  loadSchedules,
  removeSchedule,
  toggleScheduleActive,
  runScheduleNow,
  formatDate,
  formatRelativeTime,
  createEmptySchedule
} = useSchedule()

// Local state
const search = ref('')
const statusFilter = ref(null)
const sortBy = ref('next_run_asc')
const showDialog = ref(false)
const showDeleteDialog = ref(false)
const showTriggerDialog = ref(false)
const editingSchedule = ref(null)
const scheduleToDelete = ref(null)
const scheduleToTrigger = ref(null)
const deleting = ref(false)
const togglingId = ref(null)
const triggeringId = ref(null)

// Pipelines for selection
const pipelines = ref([])
const loadingPipelines = ref(false)

// Computed properties
const headers = computed(() => [
  { title: t('schedules.pipeline'), key: 'pipelineName', sortable: false },
  { title: t('schedules.cronExpression'), key: 'cronExpression', sortable: false },
  { title: t('schedules.timezone'), key: 'timezone', sortable: false },
  { title: t('schedules.isActive'), key: 'isActive', sortable: false, width: '100px' },
  { title: t('schedules.nextRun'), key: 'nextRunAt', sortable: false },
  { title: t('schedules.lastRun'), key: 'lastRunAt', sortable: false },
  { title: t('common.actions'), key: 'actions', sortable: false, width: '150px' }
])

const statusOptions = computed(() => [
  { title: t('schedules.allStatuses'), value: null },
  { title: t('schedules.active'), value: true },
  { title: t('schedules.inactive'), value: false }
])

const sortOptions = computed(() => [
  { title: t('schedules.sortNextRunAsc'), value: 'next_run_asc' },
  { title: t('schedules.sortNextRunDesc'), value: 'next_run_desc' },
  { title: t('schedules.sortCreatedDesc'), value: 'created_desc' },
  { title: t('schedules.sortPipelineAsc'), value: 'pipeline_asc' }
])

// Methods
async function fetchSchedulesList() {
  await loadSchedules({
    search: search.value || null,
    isActive: statusFilter.value,
    sortBy: sortBy.value,
    page: pagination.value.page,
    pageSize: pagination.value.pageSize
  })
}

async function loadPipelinesList() {
  loadingPipelines.value = true
  try {
    const result = await fetchPipelines()
    pipelines.value = Array.isArray(result) ? result : (result.pipelines || [])
  } catch (err) {
    console.error('Error loading pipelines:', err)
    pipelines.value = []
  } finally {
    loadingPipelines.value = false
  }
}

function openCreateDialog() {
  editingSchedule.value = null
  showDialog.value = true
  loadPipelinesList()
}

function editSchedule(schedule) {
  editingSchedule.value = { ...schedule }
  showDialog.value = true
}

function onScheduleSaved() {
  showSuccess(
    editingSchedule.value?.id ? t('schedules.updateSuccess') : t('schedules.createSuccess'),
    t('schedules.title')
  )
  fetchSchedulesList()
}

// Debounced functions
let searchTimeout = null
function debouncedFetch() {
  if (searchTimeout) clearTimeout(searchTimeout)
  searchTimeout = setTimeout(() => {
    fetchSchedulesList()
  }, 300)
}

function confirmDelete(schedule) {
  scheduleToDelete.value = schedule
  showDeleteDialog.value = true
}

async function deleteScheduleConfirmed() {
  if (!scheduleToDelete.value) return

  deleting.value = true
  try {
    await removeSchedule(scheduleToDelete.value.id)
    showSuccess(t('schedules.deleteSuccess'), t('schedules.title'))
    showDeleteDialog.value = false
    scheduleToDelete.value = null
    fetchSchedulesList()
  } catch (err) {
    showError(err.message || t('schedules.deleteError'), t('common.error'))
  } finally {
    deleting.value = false
  }
}

async function toggleActive(schedule) {
  togglingId.value = schedule.id
  try {
    await toggleScheduleActive(schedule.id)
    showSuccess(
      schedule.isActive ? t('schedules.disableSuccess') : t('schedules.enableSuccess'),
      t('schedules.title')
    )
    fetchSchedulesList()
  } catch (err) {
    showError(err.userMessage || err.message || t('schedules.toggleError'), t('common.error'))
  } finally {
    togglingId.value = null
  }
}

function triggerScheduleNow(schedule) {
  scheduleToTrigger.value = schedule
  showTriggerDialog.value = true
}

async function triggerConfirmed() {
  if (!scheduleToTrigger.value) return

  triggeringId.value = scheduleToTrigger.value.id
  showTriggerDialog.value = false
  
  try {
    await runScheduleNow(scheduleToTrigger.value.id)
    showSuccess(t('schedules.triggerSuccess'), t('schedules.title'))
    fetchSchedulesList()
  } catch (err) {
    showError(err.message || t('schedules.triggerError'), t('common.error'))
  } finally {
    triggeringId.value = null
    scheduleToTrigger.value = null
  }
}

function getStatusColor(status) {
  switch (status?.toLowerCase()) {
    case 'completed':
      return 'success'
    case 'running':
      return 'info'
    case 'failed':
      return 'error'
    case 'cancelled':
      return 'warning'
    default:
      return 'grey'
  }
}

// Lifecycle
onMounted(() => {
  fetchSchedulesList()
})
</script>

<style scoped>
code {
  background-color: rgba(var(--v-theme-surface-variant), 0.4);
  padding: 2px 6px;
  border-radius: 4px;
}
</style>
