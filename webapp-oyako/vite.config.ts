// Codex developer note: Explains the purpose and flow of webapp-oyako/vite.config.ts for maintainers.
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Defines whether the Vite server is running under Playwright and should avoid noisy backend proxy attempts.
const isPlaywrightTest = process.env.PLAYWRIGHT_TEST === '1'

// Builds the same ready status shape the production UI expects from the runtime status endpoint.
function buildReadyRuntimeStatus() {
  return {
    operation: 'app',
    phase: 'ready_for_question',
    stepKey: 'ready',
    stepIndex: 1,
    stepCount: 1,
    isTerminal: true,
    message: 'Uygulama Hazır',
    severity: 'ready',
    icon: 'message',
    pageCount: null,
    updatedAtUtc: new Date().toISOString(),
  }
}

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    {
      name: 'oyako-playwright-runtime-status',
      configureServer(server) {
        // Guards this branch so normal local development keeps using the backend proxy unchanged.
        if (!isPlaywrightTest) {
          return
        }

        // Serves the earliest runtime status calls before Playwright page routes are installed.
        server.middlewares.use((req, res, next) => {
          const requestUrl = req.url ?? ''
          // Guards this branch so all non-runtime API traffic continues through the normal Vite pipeline.
          if (!requestUrl.startsWith('/api/runtime/status')) {
            next()
            return
          }

          // Ends the SSE bootstrap quietly; tests either mock it explicitly or use polling status.
          if (requestUrl.startsWith('/api/runtime/status/stream')) {
            res.statusCode = 204
            res.end()
            return
          }

          res.statusCode = 200
          res.setHeader('Content-Type', 'application/json; charset=utf-8')
          res.end(JSON.stringify(buildReadyRuntimeStatus()))
        })
      },
    },
  ],
  server: {
    host: true,
    port: 3000,
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true,
      },
    },
  },
})
