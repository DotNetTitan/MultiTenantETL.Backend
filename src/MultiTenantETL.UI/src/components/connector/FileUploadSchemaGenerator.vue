<template>
  <v-card variant="outlined" class="mb-4">
    <v-card-text>
      <div class="text-center">
        <v-icon size="48" color="primary" class="mb-2">mdi-file-upload</v-icon>
        <h4 class="text-h6 mb-2">{{ $t('schema.uploadSampleFile') }}</h4>
        <p class="text-caption text-grey mb-4">
          {{ $t('schema.uploadDescription') }}
        </p>

        <input
          ref="fileInput"
          type="file"
          accept=".csv,.json,.xlsx,.xls"
          style="display: none"
          @change="handleFileSelect"
        />

        <v-btn
          color="primary"
          prepend-icon="mdi-file-upload"
          :loading="analyzing"
          @click="fileInput.click()"
        >
          {{ $t('schema.chooseFile') }}
        </v-btn>

        <div class="mt-2 text-caption text-grey">
          {{ $t('schema.supportedFormats') }}
        </div>
      </div>

      <v-alert v-if="analyzing" type="info" class="mt-4" density="compact">
        <v-progress-linear indeterminate class="mb-2" />
        {{ $t('schema.analyzingFile', { filename: selectedFileName }) }}
      </v-alert>

      <v-card v-if="analysisResult" variant="outlined" class="mt-4">
        <v-card-title class="d-flex align-center">
          <v-icon start color="success">mdi-check-circle</v-icon>
          <span>{{ $t('schema.foundFields', { count: analysisResult.fields.length, filename: selectedFileName }) }}</span>
          <v-spacer />
          <v-btn 
            size="small" 
            variant="text"
            @click="showPreview = !showPreview"
          >
            {{ showPreview ? $t('schema.hidePreview') : $t('schema.showPreview') }}
            <v-icon end>{{ showPreview ? 'mdi-chevron-up' : 'mdi-chevron-down' }}</v-icon>
          </v-btn>
        </v-card-title>

        <!-- Schema Preview -->
        <v-expand-transition>
          <div v-if="showPreview">
            <v-divider />
            <v-card-text>
              <div class="text-subtitle-2 mb-3">{{ $t('schema.detectedFields') }}</div>
              <div class="schema-preview">
                <v-chip
                  v-for="field in analysisResult.fields"
                  :key="field.id"
                  size="small"
                  class="mr-2 mb-2"
                  color="primary"
                  variant="outlined"
                >
                  <strong>{{ field.name }}</strong>
                  <span class="text-grey ml-1">({{ getTypeLabel(field.type) }})</span>
                  <v-icon v-if="field.nullable" size="x-small" class="ml-1" color="grey" :title="$t('connectors.nullable')">mdi-help-circle-outline</v-icon>
                </v-chip>
              </div>
              <v-divider class="my-3" />
              <div class="text-caption text-grey">
                <v-icon size="small" class="mr-1">mdi-information</v-icon>
                {{ $t('schema.editAfterApply') }}
              </div>
            </v-card-text>
          </div>
        </v-expand-transition>

        <v-divider />
        <v-card-actions>
          <v-spacer />
          <v-btn
            color="success"
            variant="elevated"
            prepend-icon="mdi-check"
            @click="applySchema"
          >
            {{ $t('schema.applySchema') }}
          </v-btn>
        </v-card-actions>
      </v-card>

      <v-alert v-if="error" type="error" class="mt-4" density="compact" closable @click:close="error = null">
        {{ error }}
      </v-alert>
    </v-card-text>
  </v-card>
</template>

<script setup>
import { ref } from 'vue';
import { useI18n } from 'vue-i18n';
import Papa from 'papaparse';
import * as XLSX from 'xlsx';
import { useGlobalState } from '@/composables/useGlobalState';

const { t } = useI18n();

const emit = defineEmits(['schema-generated']);

const { showGlobalLoading, hideGlobalLoading, showError: showErrorNotification } = useGlobalState();

const fileInput = ref(null);
const analyzing = ref(false);
const selectedFileName = ref('');
const analysisResult = ref(null);
const error = ref(null);
const showPreview = ref(false);

const MAX_FILE_SIZE = 10 * 1024 * 1024;

// Data types mapping for display
const DATA_TYPES = [
  { value: 'varchar', label: 'String' },
  { value: 'int', label: 'Integer' },
  { value: 'bigint', label: 'Big Integer' },
  { value: 'decimal', label: 'Decimal' },
  { value: 'boolean', label: 'Boolean' },
  { value: 'date', label: 'Date' },
  { value: 'datetime', label: 'Date Time' },
  { value: 'timestamp', label: 'Timestamp' },
  { value: 'json', label: 'JSON' },
  { value: 'text', label: 'Text (Long)' }
];

function getTypeLabel(type) {
  const dataType = DATA_TYPES.find(dt => dt.value === type);
  return dataType ? dataType.label : type;
}

function handleFileSelect(event) {
  const file = event.target.files[0];
  if (!file) return;

  error.value = null;
  analysisResult.value = null;

  if (file.size > MAX_FILE_SIZE) {
    error.value = 'File size exceeds 10MB limit. Please use a smaller file.';
    showErrorNotification?.('File size exceeds 10MB limit. Please use a smaller file.');
    return;
  }

  selectedFileName.value = file.name;
  analyzing.value = true;

  const extension = file.name.split('.').pop().toLowerCase();

  // Small delay to allow UI to update
  setTimeout(() => {
    try {
      if (extension === 'csv') {
        analyzeCSV(file);
      } else if (extension === 'json') {
        analyzeJSON(file);
      } else if (extension === 'xlsx' || extension === 'xls') {
        analyzeExcel(file);
      } else {
        error.value = 'Unsupported file format';
        showErrorNotification?.('Unsupported file format. Please use CSV, JSON, or Excel files.');
        analyzing.value = false;
      }
    } catch (err) {
      error.value = `Error analyzing file: ${err.message}`;
      showErrorNotification?.(`Error analyzing file: ${err.message}`);
      analyzing.value = false;
    }
  }, 50);

  event.target.value = '';
}

function analyzeCSV(file) {
  Papa.parse(file, {
    header: true,
    preview: 1000,
    worker: true, // Use web worker for better performance
    complete: (results) => {
      try {
        if (!results.data || results.data.length === 0) {
          error.value = 'CSV file is empty or invalid';
          showErrorNotification?.('CSV file is empty or invalid');
          analyzing.value = false;
          return;
        }

        const fields = inferFieldsFromData(results.meta.fields, results.data);
        analysisResult.value = { fields };
        analyzing.value = false;
      } catch (err) {
        error.value = `Error parsing CSV: ${err.message}`;
        showErrorNotification?.(`Error parsing CSV: ${err.message}`);
        analyzing.value = false;
      }
    },
    error: (err) => {
      error.value = `CSV parsing error: ${err.message}`;
      showErrorNotification?.(`CSV parsing error: ${err.message}`);
      analyzing.value = false;
    }
  });
}

function analyzeJSON(file) {
  const reader = new FileReader();

  reader.onload = (e) => {
    try {
      const json = JSON.parse(e.target.result);
      let data = json;

      if (Array.isArray(json)) {
        data = json;
      } else if (json.data && Array.isArray(json.data)) {
        data = json.data;
      } else {
        data = [json];
      }

      if (data.length === 0) {
        error.value = 'JSON file contains no data';
        showErrorNotification?.('JSON file contains no data');
        analyzing.value = false;
        return;
      }

      const firstObject = data[0];
      const fieldNames = Object.keys(firstObject);

      const fields = inferFieldsFromData(fieldNames, data);
      analysisResult.value = { fields };
      analyzing.value = false;
    } catch (err) {
      error.value = `Error parsing JSON: ${err.message}`;
      showErrorNotification?.(`Error parsing JSON: ${err.message}`);
      analyzing.value = false;
    }
  };

  reader.onerror = () => {
    error.value = 'Error reading file';
    showErrorNotification?.('Error reading file');
    analyzing.value = false;
  };

  reader.readAsText(file);
}

function analyzeExcel(file) {
  const reader = new FileReader();

  reader.onload = (e) => {
    try {
      const data = new Uint8Array(e.target.result);
      const workbook = XLSX.read(data, { type: 'array' });

      const firstSheetName = workbook.SheetNames[0];
      const worksheet = workbook.Sheets[firstSheetName];

      const jsonData = XLSX.utils.sheet_to_json(worksheet, { header: 1 });

      if (jsonData.length < 2) {
        error.value = 'Excel file must have at least a header row and one data row';
        showErrorNotification?.('Excel file must have at least a header row and one data row');
        analyzing.value = false;
        return;
      }

      const headers = jsonData[0];
      const dataRows = jsonData.slice(1, Math.min(1001, jsonData.length));

      const objects = dataRows.map(row => {
        const obj = {};
        headers.forEach((header, index) => {
          obj[header] = row[index];
        });
        return obj;
      });

      const fields = inferFieldsFromData(headers, objects);
      analysisResult.value = { fields };
      analyzing.value = false;
    } catch (err) {
      error.value = `Error parsing Excel: ${err.message}`;
      showErrorNotification?.(`Error parsing Excel: ${err.message}`);
      analyzing.value = false;
    }
  };

  reader.onerror = () => {
    error.value = 'Error reading file';
    showErrorNotification?.('Error reading file');
    analyzing.value = false;
  };

  reader.readAsArrayBuffer(file);
}

function inferFieldsFromData(fieldNames, data) {
  return fieldNames.map((name, index) => {
    const sampleValues = data
      .slice(0, 100)
      .map(row => row[name])
      .filter(val => val !== null && val !== undefined && val !== '');

    const type = inferDataType(sampleValues);

    const hasNulls = data.slice(0, 100).some(row => {
      const val = row[name];
      return val === null || val === undefined || val === '';
    });

    // Auto-detect potential unique identifiers (id, _id, etc.)
    const nameLower = String(name).toLowerCase();
    const isPotentialPrimaryKey = nameLower === 'id' || nameLower === '_id' || nameLower.endsWith('_id');

    return {
      id: `field-${Date.now()}-${index}`,
      name: String(name),
      type,
      isPrimaryKey: isPotentialPrimaryKey && !hasNulls,
      required: isPotentialPrimaryKey && !hasNulls,
      nullable: hasNulls,
      description: '',
      order: index + 1
    };
  });
}

function inferDataType(values) {
  if (values.length === 0) return 'varchar';

  let intCount = 0;
  let decimalCount = 0;
  let boolCount = 0;
  let dateCount = 0;
  let datetimeCount = 0;

  values.forEach(val => {
    const strVal = String(val).trim();

    if (/^-?\d+$/.test(strVal)) {
      intCount++;
    } else if (/^-?\d+\.\d+$/.test(strVal)) {
      decimalCount++;
    } else if (/^(true|false|yes|no|0|1)$/i.test(strVal)) {
      boolCount++;
    } else if (/^\d{4}-\d{2}-\d{2}$/.test(strVal)) {
      dateCount++;
    } else if (/^\d{4}-\d{2}-\d{2}[T\s]\d{2}:\d{2}/.test(strVal)) {
      datetimeCount++;
    }
  });

  const total = values.length;
  const threshold = 0.8;

  if (datetimeCount / total >= threshold) return 'datetime';
  if (dateCount / total >= threshold) return 'date';
  if (boolCount / total >= threshold) return 'boolean';
  if (decimalCount / total >= threshold) return 'decimal';
  if (intCount / total >= threshold) return 'int';

  return 'varchar';
}

function applySchema() {
  if (analysisResult.value) {
    emit('schema-generated', analysisResult.value.fields);
    analysisResult.value = null;
    selectedFileName.value = '';
    showPreview.value = false;
  }
}
</script>
