<template>
  <div>
    <h1 class="text-h4 mb-6">{{ $t('settings.title') }}</h1>

    <v-row>
      <v-col cols="12" lg="8">
        <!-- User Profile Card -->
        <v-card class="mb-6">
          <v-card-title class="d-flex align-center">
            <v-icon class="mr-2">mdi-account</v-icon>
            {{ $t('settings.userProfile') }}
          </v-card-title>
          <v-divider />
          <v-card-text>
            <v-alert
              v-if="profileError"
              type="error"
              variant="tonal"
              closable
              class="mb-4"
              @click:close="profileError = null"
            >
              {{ profileError }}
            </v-alert>
            <v-alert
              v-if="profileSuccess"
              type="success"
              variant="tonal"
              closable
              class="mb-4"
              @click:close="profileSuccess = null"
            >
              {{ profileSuccess }}
            </v-alert>

            <v-form ref="profileForm" @submit.prevent="saveProfile">
              <v-row>
                <v-col cols="12" md="6">
                  <FormInput
                    v-model="profile.firstName"
                    :label="$t('users.firstName')"
                    prepend-inner-icon="mdi-account"
                    :error="profileErrors.firstName"
                    :disabled="loadingProfile || savingProfile"
                    required
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <FormInput
                    v-model="profile.lastName"
                    :label="$t('users.lastName')"
                    prepend-inner-icon="mdi-account"
                    :error="profileErrors.lastName"
                    :disabled="loadingProfile || savingProfile"
                    required
                  />
                </v-col>
                <v-col cols="12">
                  <FormInput
                    v-model="profile.email"
                    :label="$t('users.email')"
                    type="email"
                    prepend-inner-icon="mdi-email"
                    :error="profileErrors.email"
                    :disabled="loadingProfile || savingProfile"
                    required
                  />
                </v-col>
              </v-row>
              
              <v-btn 
                color="primary" 
                type="submit" 
                class="mt-4"
                :loading="savingProfile"
                :disabled="loadingProfile || !isProfileChanged"
              >
                <v-icon class="mr-2">mdi-content-save</v-icon>
                {{ $t('settings.saveChanges') }}
              </v-btn>
            </v-form>
          </v-card-text>
        </v-card>

        <!-- Change Password Card -->
        <v-card>
          <v-card-title class="d-flex align-center">
            <v-icon class="mr-2">mdi-lock-reset</v-icon>
            {{ $t('settings.changePassword') }}
          </v-card-title>
          <v-divider />
          <v-card-text>
            <v-alert
              v-if="passwordError"
              type="error"
              variant="tonal"
              closable
              class="mb-4"
              @click:close="passwordError = null"
            >
              {{ passwordError }}
            </v-alert>
            <v-alert
              v-if="passwordSuccess"
              type="success"
              variant="tonal"
              closable
              class="mb-4"
              @click:close="passwordSuccess = null"
            >
              {{ passwordSuccess }}
            </v-alert>

            <v-form ref="passwordForm" @submit.prevent="changePassword">
              <v-row>
                <v-col cols="12">
                  <FormInput
                    v-model="passwordChange.current"
                    :label="$t('settings.currentPassword')"
                    type="password"
                    prepend-inner-icon="mdi-lock"
                    :error="passwordErrors.current"
                    :disabled="changingPassword"
                    autocomplete="current-password"
                    required
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <FormInput
                    v-model="passwordChange.new"
                    :label="$t('settings.newPassword')"
                    type="password"
                    prepend-inner-icon="mdi-lock-plus"
                    :error="passwordErrors.new"
                    :disabled="changingPassword"
                    autocomplete="new-password"
                    hint="Must be at least 8 characters with uppercase, lowercase, digit, and special character"
                    required
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <FormInput
                    v-model="passwordChange.confirm"
                    :label="$t('settings.confirmNewPassword')"
                    type="password"
                    prepend-inner-icon="mdi-lock-check"
                    :error="passwordErrors.confirm"
                    :disabled="changingPassword"
                    autocomplete="new-password"
                    required
                  />
                </v-col>
              </v-row>
              
              <v-btn 
                color="primary" 
                type="submit" 
                class="mt-4"
                :loading="changingPassword"
                :disabled="!isPasswordFormValid"
              >
                <v-icon class="mr-2">mdi-lock-reset</v-icon>
                {{ $t('settings.changePassword') }}
              </v-btn>
            </v-form>
          </v-card-text>
        </v-card>
      </v-col>

      <!-- Info Sidebar -->
      <v-col cols="12" lg="4">
        <v-card>
          <v-card-title class="d-flex align-center">
            <v-icon class="mr-2">mdi-information</v-icon>
            {{ $t('settings.accountInfo') }}
          </v-card-title>
          <v-divider />
          <v-card-text class="pa-0">
            <v-list lines="two">
              <v-list-item>
                <template #prepend>
                  <v-icon color="primary">mdi-calendar-clock</v-icon>
                </template>
                <v-list-item-title class="text-body-2 font-weight-medium mb-1">
                  {{ $t('settings.accountCreated') }}
                </v-list-item-title>
                <v-list-item-subtitle class="text-body-2">
                  {{ formatDate(userDetails?.createdAt) }}
                </v-list-item-subtitle>
              </v-list-item>
              
              <v-divider />
              
              <v-list-item>
                <template #prepend>
                  <v-icon :color="userDetails?.emailConfirmed ? 'success' : 'warning'">
                    {{ userDetails?.emailConfirmed ? 'mdi-email-check' : 'mdi-email-alert' }}
                  </v-icon>
                </template>
                <v-list-item-title class="text-body-2 font-weight-medium mb-1">
                  {{ $t('settings.emailConfirmed') }}
                </v-list-item-title>
                <v-list-item-subtitle>
                  <v-chip
                    :color="userDetails?.emailConfirmed ? 'success' : 'warning'"
                    size="small"
                    variant="flat"
                  >
                    {{ userDetails?.emailConfirmed ? $t('common.yes') : $t('common.no') }}
                  </v-chip>
                </v-list-item-subtitle>
              </v-list-item>
              
              <v-divider />
              
              <v-list-item>
                <template #prepend>
                  <v-icon color="primary">mdi-domain</v-icon>
                </template>
                <v-list-item-title class="text-body-2 font-weight-medium mb-1">
                  {{ $t('settings.currentTenant') }}
                </v-list-item-title>
                <v-list-item-subtitle class="text-body-2">
                  {{ userDetails?.currentTenantName || '-' }}
                </v-list-item-subtitle>
              </v-list-item>
              
              <v-divider />
              
              <v-list-item>
                <template #prepend>
                  <v-icon color="primary">mdi-shield-account</v-icon>
                </template>
                <v-list-item-title class="text-body-2 font-weight-medium mb-1">
                  {{ $t('settings.role') }}
                </v-list-item-title>
                <v-list-item-subtitle>
                  <template v-if="userDetails?.roles && userDetails.roles.length > 0">
                    <v-chip
                      v-for="role in userDetails.roles"
                      :key="role"
                      :color="getRoleColor(role)"
                      size="small"
                      variant="flat"
                      class="mr-1"
                    >
                      {{ role }}
                    </v-chip>
                  </template>
                  <v-chip
                    v-else
                    :color="getRoleColor(authStore.user?.role)"
                    size="small"
                    variant="flat"
                  >
                    {{ authStore.user?.role }}
                  </v-chip>
                </v-list-item-subtitle>
              </v-list-item>
            </v-list>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>
  </div>
</template>

<script setup>
import { ref, onMounted, computed, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useAuthStore } from '@/stores/auth';
import { userService } from '@/services/userService';
import FormInput from '@/components/form/FormInput.vue';
import { useFormValidation, required, minLength } from '@/composables/useFormValidation';

const { t } = useI18n();
const authStore = useAuthStore();
const { errors: profileErrors, validateField: validateProfileField, validateForm: validateProfileForm, clearErrors: clearProfileErrors } = useFormValidation();
const { errors: passwordErrors, validateField: validatePasswordField, validateForm: validatePasswordForm, clearErrors: clearPasswordErrors } = useFormValidation();

// Profile state
const profile = ref({
  firstName: '',
  lastName: '',
  email: ''
});
const originalProfile = ref({});
const userDetails = ref(null); // Full user details from API
const loadingProfile = ref(false);
const savingProfile = ref(false);
const profileError = ref(null);
const profileSuccess = ref(null);

// Password state
const passwordChange = ref({
  current: '',
  new: '',
  confirm: ''
});
const changingPassword = ref(false);
const passwordError = ref(null);
const passwordSuccess = ref(null);

// Computed
const isProfileChanged = computed(() => {
  return profile.value.firstName !== originalProfile.value.firstName ||
         profile.value.lastName !== originalProfile.value.lastName ||
         profile.value.email !== originalProfile.value.email;
});

const isPasswordFormValid = computed(() => {
  return passwordChange.value.current && 
         passwordChange.value.new && 
         passwordChange.value.confirm &&
         passwordChange.value.new === passwordChange.value.confirm &&
         passwordChange.value.new.length >= 8;
});

// Watch for validation
watch(() => profile.value.firstName, (val) => {
  if (val) validateProfileField('firstName', val, [required]);
});

watch(() => profile.value.lastName, (val) => {
  if (val) validateProfileField('lastName', val, [required]);
});

watch(() => profile.value.email, (val) => {
  if (val) validateProfileField('email', val, [required]);
});

watch(() => passwordChange.value.current, (val) => {
  if (val) validatePasswordField('current', val, [required]);
});

watch(() => passwordChange.value.new, (val) => {
  if (val) {
    validatePasswordField('new', val, [required, minLength(8)]);
    // Re-validate confirm if it has a value
    if (passwordChange.value.confirm) {
      validatePasswordField('confirm', passwordChange.value.confirm, [required, matchesNewPassword]);
    }
  }
});

watch(() => passwordChange.value.confirm, (val) => {
  if (val) validatePasswordField('confirm', val, [required, matchesNewPassword]);
});

// Custom validation
const matchesNewPassword = (value) => {
  return value === passwordChange.value.new ? null : 'Passwords do not match';
};

// Helper functions
function formatDate(dateString) {
  if (!dateString) return '-';
  const date = new Date(dateString);
  return date.toLocaleDateString() + ' ' + date.toLocaleTimeString();
}

function getRoleColor(role) {
  switch (role?.toLowerCase()) {
    case 'superadmin':
      return 'red';
    case 'tenantadmin':
      return 'deep-purple';
    default:
      return 'blue';
  }
}

// Load user profile
async function loadUserProfile() {
  try {
    loadingProfile.value = true;
    profileError.value = null;
    
    const userData = await userService.getMe();
    
    // Store full user details for account info display
    userDetails.value = userData;
    
    profile.value = {
      firstName: userData.firstName,
      lastName: userData.lastName,
      email: userData.email
    };
    
    // Store original for comparison
    originalProfile.value = { ...profile.value };
  } catch (error) {
    console.error('Error loading user profile:', error);
    profileError.value = 'Failed to load profile. Please try again.';
  } finally {
    loadingProfile.value = false;
  }
}

// Save profile
async function saveProfile() {
  // Validate form
  const isValid = validateProfileForm({
    firstName: { value: profile.value.firstName, rules: [required] },
    lastName: { value: profile.value.lastName, rules: [required] },
    email: { value: profile.value.email, rules: [required] }
  });

  if (!isValid) return;

  try {
    savingProfile.value = true;
    profileError.value = null;
    profileSuccess.value = null;
    
    await userService.updateMe({
      firstName: profile.value.firstName,
      lastName: profile.value.lastName,
      email: profile.value.email
    });
    
    // Update original profile
    originalProfile.value = { ...profile.value };
    
    // Update auth store
    if (authStore.user) {
      authStore.user.firstName = profile.value.firstName;
      authStore.user.lastName = profile.value.lastName;
      authStore.user.email = profile.value.email;
    }
    
    profileSuccess.value = 'Profile updated successfully!';
    
    // Clear success message after 5 seconds
    setTimeout(() => {
      profileSuccess.value = null;
    }, 5000);
  } catch (error) {
    console.error('Error saving profile:', error);
    profileError.value = error.response?.data?.message || 'Failed to update profile. Please try again.';
  } finally {
    savingProfile.value = false;
  }
}

// Change password
async function changePassword() {
  // Validate form
  const isValid = validatePasswordForm({
    current: { value: passwordChange.value.current, rules: [required] },
    new: { value: passwordChange.value.new, rules: [required, minLength(8)] },
    confirm: { value: passwordChange.value.confirm, rules: [required, matchesNewPassword] }
  });

  if (!isValid) return;

  try {
    changingPassword.value = true;
    passwordError.value = null;
    passwordSuccess.value = null;
    
    await authStore.changePassword(
      passwordChange.value.current,
      passwordChange.value.new,
      passwordChange.value.confirm
    );
    
    // Clear form
    passwordChange.value = {
      current: '',
      new: '',
      confirm: ''
    };
    clearPasswordErrors();
    
    passwordSuccess.value = 'Password changed successfully! You will be logged out shortly.';
    
    // Logout after 3 seconds (backend revokes all tokens)
    setTimeout(() => {
      authStore.logout();
    }, 3000);
  } catch (error) {
    console.error('Error changing password:', error);
    
    // Handle specific error messages
    if (error.response?.data?.message) {
      passwordError.value = error.response.data.message;
    } else if (error.response?.data?.errors) {
      passwordError.value = error.response.data.errors.join(', ');
    } else {
      passwordError.value = 'Failed to change password. Please check your current password and try again.';
    }
  } finally {
    changingPassword.value = false;
  }
}

onMounted(async () => {
  await loadUserProfile();
});
</script>
