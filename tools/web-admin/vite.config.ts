import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import path from 'path';

export default defineConfig({
  plugins: [
    react(),
    tailwindcss()
  ],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src')
    }
  },
  server: {
    port: 3000,
    proxy: {
      '/api': {
        target: process.env.VITE_API_URL || 'http://127.0.0.1:8080',
        changeOrigin: true
      },
      '/farm': {
        target: process.env.VITE_API_URL || 'http://127.0.0.1:8080',
        changeOrigin: true
      },
      '/status': {
        target: process.env.VITE_API_URL || 'http://127.0.0.1:8080',
        changeOrigin: true
      },
      '/players': {
        target: process.env.VITE_API_URL || 'http://127.0.0.1:8080',
        changeOrigin: true
      }
    }
  }
});
