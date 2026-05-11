import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    headers: {
      'Referrer-Policy': 'strict-origin-when-cross-origin',
    },
    proxy: { '/api': 'http://localhost:7071' },
  },
  preview: {
    headers: {
      'Referrer-Policy': 'strict-origin-when-cross-origin',
    },
  },
})
