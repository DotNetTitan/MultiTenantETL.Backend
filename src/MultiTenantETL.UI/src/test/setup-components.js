import { beforeAll, afterAll, afterEach, vi } from 'vitest'
import { cleanup } from '@vue/test-utils'
import { setupServer } from 'msw/node'
import { createI18n } from 'vue-i18n'
import { createPinia } from 'pinia'

// Mock vue-i18n
vi.mock('vue-i18n', async () => {
  const actual = await vi.importActual('vue-i18n')
  return {
    ...actual,
    useI18n: () => ({
      t: vi.fn((key) => `translated_${key}`),
      tm: vi.fn((key) => ({ message: `translated_${key}` })),
      te: vi.fn(() => true),
      d: vi.fn((date) => date?.toISOString() || 'mock-date'),
      n: vi.fn((num) => num?.toString() || '0'),
      locale: { value: 'en' }
    })
  }
})

// Mock Pinia stores
vi.mock('pinia', async () => {
  const actual = await vi.importActual('pinia')
  return {
    ...actual,
    defineStore: vi.fn((name, setup) => setup),
    createPinia: vi.fn(() => ({
      use: vi.fn(),
      install: vi.fn()
    }))
  }
})

// Mock router
vi.mock('vue-router', async () => {
  const actual = await vi.importActual('vue-router')
  return {
    ...actual,
    useRouter: () => ({
      push: vi.fn(),
      replace: vi.fn(),
      go: vi.fn(),
      back: vi.fn(),
      forward: vi.fn(),
      currentRoute: { value: { path: '/', query: {}, params: {} } }
    }),
    useRoute: () => ({
      path: '/',
      query: {},
      params: {},
      name: 'home'
    })
  }
})

// Import handlers from composables setup
import { handlers } from './handlers'

// Create MSW server for component tests
const server = setupServer(...handlers)

// Setup global test environment
beforeAll(() => {
  server.listen({ onUnhandledRequest: 'error' })
})

afterAll(() => {
  server.close()
})

afterEach(() => {
  server.resetHandlers()
  vi.clearAllMocks()
})

// Global test utilities
global.createI18n = createI18n
global.createPinia = createPinia

// Mock CSS imports
vi.mock('*.css', () => ({}))

// Mock SCSS imports
vi.mock('*.scss', () => ({}))

// Mock Vuetify styles import
vi.mock('vuetify/styles', () => ({}))

// Mock Vuetify components
vi.mock('vuetify/components', () => ({
  VApp: {},
  VMain: {},
  // Add other components as needed
}))

// Mock Vuetify directives
vi.mock('vuetify/directives', () => ({
  Ripple: {},
  // Add other directives as needed
}))

// Mock Vuetify locale
vi.mock('vuetify/locale', () => ({
  en: {},
  es: {},
  fr: {},
  de: {},
  it: {},
  pt: {}
}))

// Mock Vuetify to avoid CSS import issues
vi.mock('vuetify', () => ({
  createVuetify: vi.fn(() => ({
    install: vi.fn()
  }))
}))

// Mock ResizeObserver
global.ResizeObserver = vi.fn().mockImplementation(() => ({
  observe: vi.fn(),
  unobserve: vi.fn(),
  disconnect: vi.fn(),
}))

// Mock IntersectionObserver
global.IntersectionObserver = vi.fn().mockImplementation(() => ({
  observe: vi.fn(),
  unobserve: vi.fn(),
  disconnect: vi.fn(),
}))