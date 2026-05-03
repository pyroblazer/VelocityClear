import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 3006,
    proxy: {
      '/api/transactions': 'http://localhost:5001',
      '/api/auth': 'http://localhost:5000',
      '/api/apikeys': 'http://localhost:5000',
      '/api': 'http://localhost:5000',
    },
  },
})
