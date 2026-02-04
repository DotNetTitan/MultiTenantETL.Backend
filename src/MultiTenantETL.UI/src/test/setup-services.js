import { beforeAll, afterAll, afterEach, vi } from 'vitest'
import { setupServer } from 'msw/node'
import { handlers } from './handlers'

// Mock CSS imports globally
vi.mock('*.css', () => ({}))
vi.mock('*.scss', () => ({}))
vi.mock('*.sass', () => ({}))
vi.mock('*.less', () => ({}))
vi.mock('*.styl', () => ({}))

// Mock specific Vuetify CSS files
vi.mock('vuetify/lib/components/VCode/VCode.css', () => ({}))
vi.mock('vuetify/lib/styles/main.css', () => ({}))
vi.mock('vuetify/lib/styles/main.sass', () => ({}))

// Mock all Vue-related imports
vi.mock('vue', async (importOriginal) => {
  const actual = await importOriginal()
  return {
    ...actual,
    // Override any problematic functions if needed
    createApp: vi.fn(() => ({
      use: vi.fn(),
      mount: vi.fn()
    }))
  }
})

vi.mock('vuetify', () => ({
  createVuetify: vi.fn(() => ({})),
  useTheme: vi.fn(() => ({ global: { name: { value: 'light' } } })),
  useLocale: vi.fn(() => ({ current: { value: 'en' } }))
}))

vi.mock('vue-router', () => ({
  createRouter: vi.fn(() => ({
    beforeEach: vi.fn(),
    afterEach: vi.fn(),
    push: vi.fn(),
    replace: vi.fn(),
    go: vi.fn(),
    back: vi.fn(),
    forward: vi.fn()
  })),
  createWebHistory: vi.fn(),
  useRoute: vi.fn(() => ({
    params: {},
    query: {},
    name: 'test',
    path: '/test'
  })),
  useRouter: vi.fn(() => ({
    push: vi.fn(),
    replace: vi.fn(),
    go: vi.fn(),
    back: vi.fn(),
    forward: vi.fn()
  }))
}))

vi.mock('pinia', () => ({
  createPinia: vi.fn(() => ({})),
  storeToRefs: vi.fn(),
  defineStore: vi.fn((name, setup) => setup),
  setActivePinia: vi.fn()
}))

vi.mock('@/stores/auth', () => ({
  useAuthStore: vi.fn(() => ({
    token: { value: 'mock-token' },
    isAuthenticated: { value: true },
    user: { value: { id: 1, role: 'User' } }
  }))
}))

vi.mock('@/stores/tenant', () => ({
  useTenantStore: vi.fn(() => ({
    currentTenantId: { value: 'tenant-1' },
    currentTenant: { value: { id: 'tenant-1', name: 'Test Tenant' } }
  }))
}))

// Mock axios if used
vi.mock('axios', () => ({
  default: {
    create: vi.fn(() => ({
      interceptors: {
        request: { use: vi.fn() },
        response: { use: vi.fn() }
      },
      get: vi.fn(),
      post: vi.fn(),
      put: vi.fn(),
      delete: vi.fn(),
      patch: vi.fn()
    })),
    interceptors: {
      request: { use: vi.fn() },
      response: { use: vi.fn() }
    },
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
    patch: vi.fn()
  }
}))

// Mock global objects
global.window = {
  localStorage: {
    getItem: vi.fn(() => null),
    setItem: vi.fn(),
    removeItem: vi.fn(),
    clear: vi.fn()
  },
  sessionStorage: {
    getItem: vi.fn(() => null),
    setItem: vi.fn(),
    removeItem: vi.fn(),
    clear: vi.fn()
  },
  location: {
    href: 'http://localhost:3000',
    assign: vi.fn(),
    replace: vi.fn(),
    origin: 'http://localhost:3000',
    pathname: '/',
    search: '',
    hash: ''
  }
}

// Also make localStorage available globally
global.localStorage = global.window.localStorage
global.sessionStorage = global.window.sessionStorage

Object.defineProperty(global, 'document', {
  value: {
    documentElement: {
      setAttribute: vi.fn()
    },
    createElement: vi.fn(() => ({})),
    body: {}
  },
  writable: true
})

Object.defineProperty(global, 'navigator', {
  value: {
    userAgent: 'test'
  },
  writable: true
})

// Create MSW server with all handlers
const server = setupServer(...handlers)

// Export server for use in tests
export { server }

// Start server before all tests
beforeAll(() => {
  server.listen({ onUnhandledRequest: 'error' })
})

// Reset handlers after each test
afterEach(() => {
  server.resetHandlers()
})

// Close server after all tests
afterAll(() => {
  server.close()
})