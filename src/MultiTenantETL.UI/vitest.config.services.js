/// <reference types="vitest" />
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { fileURLToPath, URL } from 'node:url'

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url))
    }
  },
  test: {
    globals: true,
    environment: 'node', // Use node environment to avoid DOM issues
    setupFiles: ['./src/test/setup-services.js'], // Separate setup for services
    css: false, // Disable CSS processing in tests
    server: {
      deps: {
        inline: ['vuetify', '@/stores/auth', '@/stores/tenant']
      }
    },
    transform: {
      // Transform CSS files to empty exports
      '^.*\\.css$': () => 'export default {}',
      '^.*\\.scss$': () => 'export default {}',
      '^.*\\.sass$': () => 'export default {}',
      '^.*\\.less$': () => 'export default {}',
      '^.*\\.styl$': () => 'export default {}',
      // Specific Vuetify CSS files
      'vuetify/lib/components/VCode/VCode.css': () => 'export default {}',
      'vuetify/lib/styles/main.css': () => 'export default {}',
      'vuetify/lib/styles/main.sass': () => 'export default {}'
    },
    coverage: {
      provider: 'v8',
      reporter: ['text', 'json', 'html'],
      exclude: [
        'node_modules/',
        'src/test/',
        '**/*.d.ts',
        'cypress/',
        '.{idea,git,cache,output,temp}/',
        '**/{karma,rollup,webpack,vite,vitest,jest,ava,babel,nyc,cypress,tsup,build}.config.*'
      ],
      thresholds: {
        global: {
          branches: 80,
          functions: 80,
          lines: 80,
          statements: 80
        }
      }
    }
  }
})