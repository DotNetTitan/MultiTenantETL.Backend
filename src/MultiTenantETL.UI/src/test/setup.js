import { beforeAll, afterAll, afterEach, vi } from 'vitest'
import { setupServer } from 'msw/node'
import { handlers } from './handlers'

// Mock CSS imports
vi.mock('*.css', () => ({ default: '' }))

// Mock Vuetify CSS specifically
vi.mock('vuetify/lib/**/*.css', () => ({ default: '' }))

// Mock Vuetify
vi.mock('vuetify', () => ({
  createVuetify: vi.fn(() => ({}))
}))

// Mock Vue components that might be imported
vi.mock('vue', () => ({
  createApp: vi.fn(() => ({
    use: vi.fn(),
    mount: vi.fn()
  })),
  reactive: vi.fn(),
  ref: vi.fn(),
  computed: vi.fn(),
  watch: vi.fn(),
  onMounted: vi.fn(),
  onUnmounted: vi.fn()
}))

// Create MSW server with all handlers
const server = setupServer(...handlers)

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