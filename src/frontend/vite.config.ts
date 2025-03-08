import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    tailwindcss(),
  ],
  server: {
    proxy: {
      '/sitecore': {
        target: 'https://9382-186-4-169-151.ngrok-free.app',
        changeOrigin: true,
        secure: false
      },
      '/-': {
        target: 'https://9382-186-4-169-151.ngrok-free.app',
        changeOrigin: true,
        secure: false
      }
    }
  }
})
