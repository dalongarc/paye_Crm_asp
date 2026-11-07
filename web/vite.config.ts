/// <reference types="vitest/config" />
import path from 'path'
import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { tanstackRouter } from '@tanstack/router-plugin/vite'
import { playwright } from '@vitest/browser-playwright'

// https://vite.dev/config/
export default defineConfig(({ mode, command }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const useMocks = (process.env.VITE_USE_MOCKS ?? env.VITE_USE_MOCKS) === 'true'
  const allowMockBuild = (process.env.VITE_ALLOW_MOCK_BUILD ?? env.VITE_ALLOW_MOCK_BUILD) === 'true'

  if (command === 'build' && useMocks && !allowMockBuild) {
    throw new Error(
      '[Seguridad / Fail-Closed]: Prohibido compilar un build de producción con VITE_USE_MOCKS="true". El factory mock no debe incluirse en producción.'
    )
  }

  return {
    preview: {
      port: 5182,
      headers: {
        'Content-Security-Policy':
          "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; font-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'",
      },
    },
    plugins: [
      {
        name: 'strip-sonner-insert-css',
        enforce: 'pre',
        transform(code, id) {
          if (id.includes('sonner')) {
            return {
              code: code.replace(/function __insertCSS\([^)]*\)\s*\{[\s\S]*?\}/, 'function __insertCSS() {}'),
              map: null,
            }
          }
        },
      },
      {
        name: 'strip-react-style-singleton',
        enforce: 'pre',
        transform(code, id) {
          if (id.includes('react-style-singleton')) {
            return {
              code: code
                .replace(
                  /export var stylesheetSingleton\s*=\s*function\s*\(\)\s*\{[\s\S]*?\n\};/,
                  'export var stylesheetSingleton = function () { return { add: function () {}, remove: function () {} }; };'
                )
                .replace(
                  /var stylesheetSingleton\s*=\s*function\s*\(\)\s*\{[\s\S]*?\n\};/,
                  'var stylesheetSingleton = function () { return { add: function () {}, remove: function () {} }; };'
                ),
              map: null,
            }
          }
        },
      },
      tanstackRouter({
        target: 'react',
        autoCodeSplitting: true,
      }),
      react(),
      tailwindcss(),
    ],
    resolve: {
      alias: {
        '@/api/factory': path.resolve(
          __dirname,
          useMocks ? './src/api/mock/factory.ts' : './src/api/real/factory.ts'
        ),
        '@': path.resolve(__dirname, './src'),
      },
    },
    test: {
      silent: 'passed-only',
      unstubEnvs: true,
      browser: {
        enabled: true,
        provider: playwright(),
        instances: [{ browser: 'chromium' }],
      },
      coverage: {
        exclude: [
          'src/components/ui/**',
          'src/assets/**',
          'src/tanstack-table.d.ts',
          'src/routeTree.gen.ts',
          'src/test-utils/**',
          'src/routes/**',
        ],
      },
    },
  }
})
