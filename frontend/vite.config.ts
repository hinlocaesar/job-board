/// <reference types="vitest/config" />
import vue from '@vitejs/plugin-vue'
import tailwindcss from '@tailwindcss/vite'
import { fileURLToPath, URL } from 'node:url'
import { defineConfig, loadEnv } from 'vite'

// Local config lives in the repo-root .env (gitignored, see .env.example).
const repoRoot = fileURLToPath(new URL('..', import.meta.url))

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, repoRoot, '')

  return {
    envDir: repoRoot,
    plugins: [vue(), tailwindcss()],
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('./src', import.meta.url)),
      },
    },
    server: {
      port: 5173,
      strictPort: true,
      proxy: {
        // Marketplace API (ASP.NET Core)
        '/api': {
          target: env.VITE_PROXY_API || 'http://localhost:5201',
          changeOrigin: true,
        },
        // Umbraco Content Delivery API — dev cert is self-signed, hence secure: false
        '/umbraco': {
          target: env.VITE_PROXY_UMBRACO || 'https://localhost:7123',
          changeOrigin: true,
          secure: false,
        },
      },
    },
    test: {
      environment: 'jsdom',
      // Mount pages in jsdom as if the browser loaded them from the dev server,
      // so relative `/api` + `/umbraco` requests hit the Vite proxies for real.
      environmentOptions: { jsdom: { url: 'http://localhost:5173' } },
      setupFiles: ['src/test/setup.ts'],
      include: ['src/**/*.{test,spec}.ts'],
      css: false,
    },
  }
})
