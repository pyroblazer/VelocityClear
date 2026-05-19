import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import federation from '@originjs/vite-plugin-federation'

export default defineConfig({
  plugins: [
    react(),
    tailwindcss(),
    federation({
      name: 'superapp_shell',
      remotes: {
        admin_dashboard: 'http://localhost:3002/assets/remoteEntry.js',
        risk_dashboard: 'http://localhost:3003/assets/remoteEntry.js',
        audit_dashboard: 'http://localhost:3004/assets/remoteEntry.js',
        card_operations: 'http://localhost:3005/assets/remoteEntry.js',
        transaction_ui: 'http://localhost:3001/assets/remoteEntry.js',
        integration_demo: 'http://localhost:3006/assets/remoteEntry.js',
      },
      shared: ['react', 'react-dom'],
    }),
  ],
  server: {
    port: 3000,
    proxy: {
      '/api': 'http://localhost:5000',
    },
  },
  preview: {
    port: 4173,
  },
  build: {
    target: 'esnext',
    minify: false,
    cssCodeSplit: false,
  },
})
