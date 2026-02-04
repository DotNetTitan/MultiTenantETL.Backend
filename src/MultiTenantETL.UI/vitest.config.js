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
    environment: 'jsdom', // Use jsdom environment to handle DOM/CSS issues
    setupFiles: ['./src/test/setup-components.js'], // Use component setup for all tests
    // css: false, // Enable CSS processing in tests with jsdom
    testTimeout: 10000, // 10 second timeout per test
    hookTimeout: 15000, // 15 second timeout for hooks
  },
})