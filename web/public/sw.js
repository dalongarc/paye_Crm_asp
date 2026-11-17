const CACHE_NAME = 'paye-crm-shell-v6';
const PRECACHE_URLS = [
  '/',
  '/index.html',
  '/manifest.webmanifest',
  '/images/pwa-192x192.png',
  '/images/pwa-512x512.png',
  '/images/pwa-maskable-512x512.png',
  '/images/apple-touch-icon.png',
];

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches
      .open(CACHE_NAME)
      .then((cache) => cache.addAll(PRECACHE_URLS))
      .then(() => self.skipWaiting())
  );
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((keys) =>
        Promise.all(
          keys
            .filter((key) => key !== CACHE_NAME)
            .map((key) => caches.delete(key))
        )
      )
      .then(() => self.clients.claim())
  );
});

self.addEventListener('message', (event) => {
  if (event.data && event.data.type === 'SKIP_WAITING') {
    self.skipWaiting();
  }
  if (event.data && event.data.type === 'CLEAR_CACHE') {
    event.waitUntil(
      caches.keys().then((keys) => Promise.all(keys.map((k) => caches.delete(k))))
    );
  }
});

function isSensitiveOrDynamicPath(pathname) {
  return (
    pathname.startsWith('/api') ||
    pathname.startsWith('/account') ||
    pathname.startsWith('/health') ||
    pathname.startsWith('/openapi') ||
    pathname.startsWith('/error')
  );
}

self.addEventListener('fetch', (event) => {
  const { request } = event;
  if (request.method !== 'GET') {
    return;
  }

  const url = new URL(request.url);
  if (url.origin !== self.location.origin) {
    return;
  }

  // SECURITY: Never intercept or cache API, auth, health, or private endpoints
  if (isSensitiveOrDynamicPath(url.pathname)) {
    return;
  }

  // Navigation requests: Network-first, fallback to cached SPA shell (/index.html) when offline
  if (request.mode === 'navigate') {
    event.respondWith(
      fetch(request).catch(async () => {
        const cache = await caches.open(CACHE_NAME);
        const cachedShell =
          (await cache.match('/index.html')) || (await cache.match('/'));
        return cachedShell || Response.error();
      })
    );
    return;
  }

  // Static immutable assets (/assets/*, /images/*, /manifest.webmanifest): Stale-While-Revalidate
  if (
    url.pathname.startsWith('/assets/') ||
    url.pathname.startsWith('/images/') ||
    url.pathname === '/manifest.webmanifest' ||
    url.pathname === '/favicon.ico'
  ) {
    event.respondWith(
      caches.open(CACHE_NAME).then(async (cache) => {
        const cached = await cache.match(request);
        const networkFetch = fetch(request)
          .then((response) => {
            if (response && response.status === 200 && response.type === 'basic') {
              cache.put(request, response.clone());
            }
            return response;
          })
          .catch(() => cached);

        return cached || networkFetch;
      })
    );
  }
});
