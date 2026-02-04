import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'

// Vuetify
import 'vuetify/styles'
import { createVuetify } from 'vuetify'
import * as components from 'vuetify/components'
import * as directives from 'vuetify/directives'
import { en, es, fr, de, it, pt } from 'vuetify/locale'

// Vue I18n
import { createI18n } from 'vue-i18n'
import { messages, defaultLocale, fallbackLocale } from './locales'

// Motion
import { MotionPlugin } from '@vueuse/motion'

// Global styles
import './styles/global.scss'

const lightTheme = {
  dark: false,
  colors: {
    primary: '#1976D2',
    secondary: '#5C6BC0',
    accent: '#00B0FF',
    error: '#FF5252',
    info: '#2196F3',
    success: '#4CAF50',
    warning: '#FB8C00',
    surface: '#FFFFFF',
    'surface-variant': '#F5F7FA',
    'on-surface': '#2C3E50',
    'on-surface-variant': '#546E7A',
    background: '#F0F2F5',
    'on-background': '#2C3E50',
    outline: '#CFD8DC',
    'outline-variant': '#ECEFF1'
  }
}

const darkTheme = {
  dark: true,
  colors: {
    primary: '#2196F3',
    secondary: '#424242',
    accent: '#FF4081',
    error: '#FF5252',
    info: '#2196F3',
    success: '#4CAF50',
    warning: '#FFC107',
    background: '#121212',
    surface: '#212121',
    'surface-variant': '#1e1e1e',
    'on-surface-variant': '#e0e0e0',
    'on-background': '#e0e0e0'
  }
}

// Get saved locale from localStorage or use default
const savedLocale = localStorage.getItem('user-locale') || defaultLocale

// Create i18n instance
const i18n = createI18n({
  legacy: false, // Use Composition API mode
  locale: savedLocale,
  fallbackLocale: fallbackLocale,
  messages,
  globalInjection: true,
  missingWarn: false,
  fallbackWarn: false
})

const vuetify = createVuetify({
  components,
  directives,
  theme: {
    defaultTheme: 'dark',
    themes: {
      light: lightTheme,
      dark: darkTheme
    }
  },
  locale: {
    locale: savedLocale,
    fallback: fallbackLocale,
    messages: { en, es, fr, de, it, pt }
  }
})

const app = createApp(App)

app.use(createPinia())
app.use(i18n)
app.use(router)
app.use(vuetify)
app.use(MotionPlugin)

// Set initial HTML lang attribute
document.documentElement.setAttribute('lang', savedLocale)

// Initialize app constants from backend
import { initializeConstants } from '@/config/constants'

// Initialize auth store
import { useAuthStore } from '@/stores/auth'

// Initialize constants before mounting app
initializeConstants().then(() => {
  const authStore = useAuthStore()
  authStore.initialize()
  app.mount('#app')
}).catch(error => {
  console.error('Failed to initialize app:', error)
  // Show error to user instead of mounting with broken state
  document.body.innerHTML = `
    <div style="display: flex; align-items: center; justify-content: center; height: 100vh; font-family: sans-serif;">
      <div style="text-align: center; max-width: 500px; padding: 20px;">
        <h1 style="color: #d32f2f;">Failed to Initialize Application</h1>
        <p style="color: #666;">Could not load configuration from backend.</p>
        <p style="color: #999; font-size: 14px;">${error.message}</p>
        <button onclick="location.reload()" style="margin-top: 20px; padding: 10px 20px; background: #1976d2; color: white; border: none; border-radius: 4px; cursor: pointer;">
          Retry
        </button>
      </div>
    </div>
  `
})
