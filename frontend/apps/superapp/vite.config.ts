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
      '/api/hsm': 'http://localhost:5005',
      '/api/iso8583': 'http://localhost:5005',
      '/api/transactions': 'http://localhost:5001',
      '/api/risk': 'http://localhost:5002',
      '/api/payment': 'http://localhost:5003',
      '/api/audit': 'http://localhost:5004',
      '/api/kyc': 'http://localhost:5004',
      '/api/consent': 'http://localhost:5004',
      '/api/aml': 'http://localhost:5004',
      '/api/approvals': 'http://localhost:5004',
      '/api/complaints': 'http://localhost:5004',
      '/api/soc': 'http://localhost:5004',
      '/api/reports': 'http://localhost:5004',
      '/api/digital-signature': 'http://localhost:5004',
      '/api/data-masking': 'http://localhost:5004',
      '/api/access-control': 'http://localhost:5004',
      '/api/infrastructure-compliance': 'http://localhost:5004',
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
