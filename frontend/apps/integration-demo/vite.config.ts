import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import federation from '@originjs/vite-plugin-federation'

export default defineConfig({
  plugins: [
    react(),
    federation({
      name: 'integration_demo',
      filename: 'remoteEntry.js',
      exposes: {
        './App': './src/App.tsx',
      },
      shared: ['react', 'react-dom'],
    }),
  ],
  server: {
    port: 3006,
    proxy: {
      '/api/transactions': 'http://localhost:5001',
      '/api/auth': 'http://localhost:5000',
      '/api/apikeys': 'http://localhost:5000',
      '/api': 'http://localhost:5000',
    },
  },
  build: {
    target: 'esnext',
    minify: false,
    cssCodeSplit: false,
  },
})
