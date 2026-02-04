<template>
  <v-snackbar
    v-model="show"
    :color="type"
    :timeout="timeout"
    location="top"
  >
    {{ text }}

    <template #actions>
      <v-btn
        variant="text"
        @click="show = false"
      >
        Close
      </v-btn>
    </template>
  </v-snackbar>
</template>

<script setup>
import { ref } from 'vue';

const show = ref(false);
const text = ref('');
const type = ref('success');
const timeout = ref(3000);

function showNotification(message, notificationType = 'success', notificationTimeout = 3000) {
  text.value = message;
  type.value = notificationType;
  timeout.value = notificationTimeout;
  show.value = true;
}

defineExpose({
  showNotification
});
</script>