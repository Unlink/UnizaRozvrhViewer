// In development, we'll enable basic caching for testing purposes
// For production, see service-worker.published.js

const apiCacheName = 'uniza-api-cache-dev';
const settingsCacheName = 'uniza-settings-cache';

// Default cache duration (24 hours)
let API_CACHE_DURATION = 24 * 60 * 60 * 1000;

const API_ENDPOINTS = [
    'nic.uniza.sk/webservices/getUnizaScheduleContent.php',
    'corsproxy.io'
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
            console.log('Service worker (dev): Loaded cache duration:', API_CACHE_DURATION, 'ms');
        }
    } catch (error) {
        console.log('Service worker (dev): Using default cache duration');
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
        console.log('Service worker (dev): Saved cache duration:', duration, 'ms');
    } catch (error) {
        console.error('Service worker (dev): Error saving cache duration:', error);
    }
}

self.addEventListener('install', event => {
    console.log('Service worker (dev): Install');
    self.skipWaiting();
});

self.addEventListener('activate', event => {
    console.log('Service worker (dev): Activate');
    event.waitUntil(
        loadCacheDuration().then(() => self.clients.claim())
    );
});

self.addEventListener('fetch', event => {
    const requestUrl = event.request.url;

    // Only cache API requests
    if (event.request.method === 'GET' && isApiRequest(requestUrl)) {
        event.respondWith(handleApiRequest(event.request));
    }
    // For other requests, just fetch normally
});

self.addEventListener('message', event => {
    if (event && event.data && event.data.type === 'SKIP_WAITING') {
        self.skipWaiting();
    }
    if (event && event.data && event.data.type === 'CLEAR_API_CACHE') {
        event.waitUntil(clearApiCache());
    }
    if (event && event.data && event.data.type === 'SET_CACHE_DURATION') {
        const duration = CACHE_DURATIONS[event.data.value] || CACHE_DURATIONS['1d'];
        event.waitUntil(saveCacheDuration(duration));
    }
    if (event && event.data && event.data.type === 'GET_CACHE_DURATION') {
        event.ports[0].postMessage({ duration: API_CACHE_DURATION });
    }
});

function isApiRequest(url) {
    return API_ENDPOINTS.some(endpoint => url.includes(endpoint));
}

async function handleApiRequest(request) {
    const cache = await caches.open(apiCacheName);
    
    try {
        const cachedResponse = await cache.match(request);
        
        if (cachedResponse) {
            const cachedTime = cachedResponse.headers.get('sw-cache-time');
            if (cachedTime) {
                const age = Date.now() - parseInt(cachedTime);
                if (age < API_CACHE_DURATION) {
                    console.log('Service worker (dev): Serving from cache:', request.url);
                    return cachedResponse;
                }
            }
        }
        
        console.log('Service worker (dev): Fetching from network:', request.url);
        const networkResponse = await fetch(request);
        
        if (networkResponse.ok) {
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
            
            await cache.put(request, responseWithHeaders);
            console.log('Service worker (dev): Cached response:', request.url);
        }
        
        return networkResponse;
        
    } catch (error) {
        console.error('Service worker (dev): Network request failed:', error);
        
        const cachedResponse = await cache.match(request);
        if (cachedResponse) {
            console.log('Service worker (dev): Serving stale cache (offline):', request.url);
            return cachedResponse;
        }
        
        throw error;
    }
}

async function clearApiCache() {
    console.log('Service worker (dev): Clearing API cache');
    const cache = await caches.open(apiCacheName);
    const requests = await cache.keys();
    await Promise.all(requests.map(request => cache.delete(request)));
    console.log('Service worker (dev): API cache cleared');
}
