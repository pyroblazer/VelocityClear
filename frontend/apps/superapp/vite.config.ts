import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 3000,
    proxy: {
      '/api/hsm': 'http://localhost:5005',
      '/api/iso8583': 'http://localhost:5005',
      '/api': 'http://localhost:5000',
    },
  },
  preview: {
    port: 4173,
  },
})
