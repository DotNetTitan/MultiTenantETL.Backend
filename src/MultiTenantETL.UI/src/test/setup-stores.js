import { vi } from 'vitest'

// Mock CSS imports globally
vi.mock('*.css', () => ({}))

// Mock SCSS imports
vi.mock('*.scss', () => ({}))

// Mock Vuetify completely
vi.mock('vuetify', () => ({
  createVuetify: vi.fn(() => ({})),
  useTheme: vi.fn(() => ({ global: { name: { value: 'light' } } })),
  useLocale: vi.fn(() => ({ current: { value: 'en' } }))
}))

// Mock Vue components
vi.mock('vue', async () => {
  const actual = await vi.importActual('vue')
  return {
    ...actual,
    // Override any problematic functions if needed
  }
})

// Mock Vue Router
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

// Mock Pinia
vi.mock('pinia', () => ({
  defineStore: vi.fn((name, setup) => setup),
  setActivePinia: vi.fn(),
  createPinia: vi.fn(() => ({}))
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