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
})
