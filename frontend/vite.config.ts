import path from 'node:path'
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': path.resolve(import.meta.dirname, './src'),
    },
  },
  server: {
    port: 5173,
    // Calls to /api/... during development are forwarded to the .NET API, so the
    // browser sees one origin. That keeps cookies and CORS out of the picture while
    // developing, which is where they are hardest to debug.
    //
    // 5126 must match applicationUrl in
    // backend/src/Inventory.Api/Properties/launchSettings.json ("http" profile).
    proxy: {
      '/api': {
        target: 'http://localhost:5126',
        changeOrigin: true,
      },
      '/health': {
        target: 'http://localhost:5126',
        changeOrigin: true,
      },
    },
  },
})