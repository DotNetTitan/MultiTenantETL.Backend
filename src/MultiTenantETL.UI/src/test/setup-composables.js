import { vi } from 'vitest'

// Mock CSS imports globally
vi.mock('*.css', () => ({}))

// Mock SCSS imports
vi.mock('*.scss', () => ({}))

// Mock Vuetify CSS specifically
vi.mock('vuetify/lib/components/VCode/VCode.css', () => ({}))

// Mock all vuetify styles
vi.mock('vuetify/styles', () => ({}))

// Mock any other potential CSS imports
vi.mock('@mdi/font/css/materialdesignicons.css', () => ({}))

// Mock Vue if needed for composables - don't mock it, just let it work normally
// vi.mock('vue', () => {
//   const actual = vi.importActual('vue')
//   return actual
// })