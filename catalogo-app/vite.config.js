import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { VitePWA } from 'vite-plugin-pwa'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    VitePWA({
      registerType: 'autoUpdate',
      includeAssets: [
        'icons/*.png',
        'brand/*.png',
        'favicon.svg',
      ],
      manifest: {
        name: 'Multilicores',
        short_name: 'Multilicores',
        description: 'Catálogo Multilicores — pide por WhatsApp. Producto Monteblanco.',
        theme_color: '#1d4e89',
        background_color: '#1d4e89',
        display: 'standalone',
        orientation: 'portrait-primary',
        lang: 'es',
        dir: 'ltr',
        start_url: '/sistema/app/',
        scope: '/sistema/app/',
        id: '/sistema/app/',
        categories: ['shopping', 'business'],
        icons: [
          {
            src: 'icons/icon-192.png',
            sizes: '192x192',
            type: 'image/png',
          },
          {
            src: 'icons/icon-512.png',
            sizes: '512x512',
            type: 'image/png',
          },
          {
            src: 'icons/maskable-512.png',
            sizes: '512x512',
            type: 'image/png',
            purpose: 'maskable',
          },
        ],
      },
      workbox: {
        navigateFallback: '/sistema/app/index.html',
        globPatterns: ['**/*.{js,css,html,ico,png,svg,woff2}'],
        runtimeCaching: [
          {
            urlPattern: ({ url }) => url.pathname.startsWith('/sistema/api/'),
            handler: 'NetworkFirst',
            options: {
              cacheName: 'api-catalogo',
              networkTimeoutSeconds: 8,
              expiration: {
                maxEntries: 64,
                maxAgeSeconds: 60 * 30,
              },
              cacheableResponse: {
                statuses: [0, 200],
              },
            },
          },
          {
            urlPattern: ({ url }) =>
              url.pathname.startsWith('/sistema/assets/img/'),
            handler: 'CacheFirst',
            options: {
              cacheName: 'img-productos',
              expiration: {
                maxEntries: 200,
                maxAgeSeconds: 60 * 60 * 24 * 7,
              },
              cacheableResponse: {
                statuses: [0, 200],
              },
            },
          },
        ],
      },
      devOptions: {
        enabled: false,
      },
    }),
  ],
  base: '/sistema/app/',
  server: {
    port: 5173,
    proxy: {
      '/sistema/api': {
        target: 'https://multilicoreschapinero.com',
        changeOrigin: true,
        secure: true,
      },
      '/sistema/assets': {
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
