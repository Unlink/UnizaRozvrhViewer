// Caution! Be sure you understand the caveats before publishing an application with
// offline support. See https://aka.ms/blazor-offline-considerations

// Load the assets manifest generated during publish
self.importScripts('./service-worker-assets.js');

// Fix: define manifestUrlList (was referenced but not defined causing SW failure).
// Use absolute URLs for comparison with event.request.url.
const manifestUrlList = self.assetsManifest && self.assetsManifest.assets
    ? self.assetsManifest.assets.map(a => new URL(a.url, self.location).href)
    : [];

self.addEventListener('install', event => {
    // Activate this service worker immediately on install
    self.skipWaiting();
    event.waitUntil(onInstall(event));
});

self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));

// Allow the page to tell the SW to activate immediately
self.addEventListener('message', event => {
    if (event && event.data && event.data.type === 'SKIP_WAITING') {
        self.skipWaiting();
    }
    // Handle cache clear message
    if (event && event.data && event.data.type === 'CLEAR_API_CACHE') {
        event.waitUntil(clearApiCache());
    }
    // Handle cache duration setting
    if (event && event.data && event.data.type === 'SET_CACHE_DURATION') {
        const duration = CACHE_DURATIONS[event.data.value] || CACHE_DURATIONS['1d'];
        event.waitUntil(saveCacheDuration(duration));
    }
    // Handle cache duration get request
    if (event && event.data && event.data.type === 'GET_CACHE_DURATION') {
        event.ports[0].postMessage({ duration: API_CACHE_DURATION });
    }
});

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const apiCacheName = 'uniza-api-cache-v1';
const settingsCacheName = 'uniza-settings-cache';
const offlineAssetsInclude = [ /\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.blat$/, /\.dat$/ ];
const offlineAssetsExclude = [ /^service-worker\.js$/ ];

// Default cache duration (24 hours)
let API_CACHE_DURATION = 24 * 60 * 60 * 1000;

const API_ENDPOINTS = [
    'nic.uniza.sk/webservices/getUnizaScheduleContent.php',
    'corsproxy.io' // For teacher/room/group searches
];

// Cache duration presets (in milliseconds)
const CACHE_DURATIONS = {
    '1h': 60 * 60 * 1000,
    '1d': 24 * 60 * 60 * 1000,
    '7d': 7 * 24 * 60 * 60 * 1000,
    '30d': 30 * 24 * 60 * 60 * 1000
};

// Load cache duration from settings
async function loadCacheDuration() {
    try {
        const settingsCache = await caches.open(settingsCacheName);
        const response = await settingsCache.match('cache-duration-setting');
        if (response) {
            const data = await response.json();
            API_CACHE_DURATION = data.duration;
            console.log('Service worker: Loaded cache duration:', API_CACHE_DURATION, 'ms');
        }
    } catch (error) {
        console.log('Service worker: Using default cache duration');
    }
}

// Save cache duration to settings
async function saveCacheDuration(duration) {
    try {
        const settingsCache = await caches.open(settingsCacheName);
        const response = new Response(JSON.stringify({ duration, timestamp: Date.now() }), {
            headers: { 'Content-Type': 'application/json' }
        });
        await settingsCache.put('cache-duration-setting', response);
        API_CACHE_DURATION = duration;
        console.log('Service worker: Saved cache duration:', duration, 'ms');
    } catch (error) {
        console.error('Service worker: Error saving cache duration:', error);
    }
}

async function onInstall(event) {
    console.info('Service worker: Install');

    // Fetch and cache all matching items from the assets manifest
    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));
    await caches.open(cacheName).then(cache => cache.addAll(assetsRequests));
}

async function onActivate(event) {
    console.info('Service worker: Activate');

    // Load cache duration setting
    await loadCacheDuration();

    // Delete unused caches
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));

    // Start controlling clients without waiting for next navigation
    await self.clients.claim();
}

async function onFetch(event) {
    const requestUrl = event.request.url;

    // Check if this is an API request
    if (event.request.method === 'GET' && isApiRequest(requestUrl)) {
        return await handleApiRequest(event.request);
    }

    // Handle regular asset requests
    let cachedResponse = null;
    if (event.request.method === 'GET') {
        // For all navigation requests, try to serve index.html from cache,
        // unless that request is for an offline resource.
        // If you need some URLs to be server-rendered, edit the following check to exclude those URLs
        const shouldServeIndexHtml = event.request.mode === 'navigate'
            && !manifestUrlList.some(url => url === event.request.url);

        const request = shouldServeIndexHtml ? 'index.html' : event.request;
        const cache = await caches.open(cacheName);
        cachedResponse = await cache.match(request);
    }

    return cachedResponse || fetch(event.request);
}

function isApiRequest(url) {
    return API_ENDPOINTS.some(endpoint => url.includes(endpoint));
}

async function handleApiRequest(request) {
    const cache = await caches.open(apiCacheName);
    
    try {
        // Try to get from cache first
        const cachedResponse = await cache.match(request);
        
        if (cachedResponse) {
            // Check if cache is still valid
            const cachedTime = cachedResponse.headers.get('sw-cache-time');
            if (cachedTime) {
                const age = Date.now() - parseInt(cachedTime);
                if (age < API_CACHE_DURATION) {
                    console.log('Service worker: Serving from cache:', request.url);
                    return cachedResponse;
                } else {
                    console.log('Service worker: Cache expired:', request.url);
                }
            }
        }
        
        // If not in cache or expired, fetch from network
        console.log('Service worker: Fetching from network:', request.url);
        const networkResponse = await fetch(request);
        
        // Only cache successful responses
        if (networkResponse.ok) {
            // Clone the response and add cache timestamp
            const responseToCache = networkResponse.clone();
            const headers = new Headers(responseToCache.headers);
            headers.append('sw-cache-time', Date.now().toString());
            
            const cachedResponseInit = {
                status: responseToCache.status,
                statusText: responseToCache.statusText,
                headers: headers
            };
            
            const body = await responseToCache.blob();
            const responseWithHeaders = new Response(body, cachedResponseInit);
            
            // Store in cache
            await cache.put(request, responseWithHeaders);
            console.log('Service worker: Cached response:', request.url);
        }
        
        return networkResponse;
        
    } catch (error) {
        console.error('Service worker: Network request failed:', error);
        
        // Try to serve from cache even if expired (offline fallback)
        const cachedResponse = await cache.match(request);
        if (cachedResponse) {
            console.log('Service worker: Serving stale cache (offline):', request.url);
            return cachedResponse;
        }
        
        // If no cache available, throw error
        throw error;
    }
}

async function clearApiCache() {
    console.log('Service worker: Clearing API cache');
    const cache = await caches.open(apiCacheName);
    const requests = await cache.keys();
    await Promise.all(requests.map(request => cache.delete(request)));
    console.log('Service worker: API cache cleared');
}
