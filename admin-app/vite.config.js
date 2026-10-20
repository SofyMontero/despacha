import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  base: '/sistema/ops/',
  server: {
    port: 5174,
    proxy: {
      '/sistema/api': {
        target: 'https://multilicoreschapinero.com',
        changeOrigin: true,
        secure: true,
      },
    },
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true,
  },
})
