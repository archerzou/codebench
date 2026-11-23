/// <reference types="vitest/config" />
import path from 'node:path'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: { '@': path.resolve(import.meta.dirname, './src') },
  },
  server: {
    port: 5173,
    strictPort: true,
    // src/index.css imports ../../../docs/design/tokens.css (one file, never copied)
    fs: { allow: [import.meta.dirname, path.resolve(import.meta.dirname, '../../docs/design')] },
  },
  build: {
    sourcemap: false, // FE-14 / RA-05: no source maps in dist/
    // Monaco (~2.8 MB) lives in the lazily loaded editor chunk; the entry chunk is ~750 kB (Clerk + Radix).
    chunkSizeWarningLimit: 3000,
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    css: false,
    env: { VITE_API_URL: 'http://api.test', VITE_CLERK_PUBLISHABLE_KEY: 'pk_test_placeholder' },
  },
})
