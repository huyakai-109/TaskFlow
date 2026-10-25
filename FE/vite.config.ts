import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // Pinned, and strict on purpose: the API allow-lists this exact origin
    // (Cors:AllowedOrigins), so silently falling back to 5174 would break
    // every authenticated call instead of failing here.
    port: 5173,
    strictPort: true,
  },
})
