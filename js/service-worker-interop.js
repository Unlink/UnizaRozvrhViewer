// Service Worker Interop for Blazor
window.serviceWorkerInterop = {
    // Wait for service worker to be ready
    _waitForServiceWorker: async function() {
        if ('serviceWorker' in navigator) {
            try {
                await navigator.serviceWorker.ready;
                return true;
            } catch (error) {
                console.error('Error waiting for service worker:', error);
                return false;
            }
        }
        return false;
    },

    // Get the correct cache name based on environment
    _getCacheName: async function() {
        if (!('caches' in window)) {
            return null;
        }
        
        const cacheNames = await caches.keys();
        // Look for API cache (either dev or production)
        const apiCache = cacheNames.find(name => 
            name.includes('uniza-api-cache')
        );
        return apiCache || null;
    },

    // Clear API cache
    clearCache: async function () {
        if ('serviceWorker' in navigator && navigator.serviceWorker.controller) {
            navigator.serviceWorker.controller.postMessage({
                type: 'CLEAR_API_CACHE'
            });
            return true;
        }
        return false;
    },

    // Set cache duration
    setCacheDuration: async function (durationKey) {
        if ('serviceWorker' in navigator && navigator.serviceWorker.controller) {
            navigator.serviceWorker.controller.postMessage({
                type: 'SET_CACHE_DURATION',
                value: durationKey
            });
            console.log('Cache duration set to:', durationKey);
            return true;
        }
        return false;
    },

    // Get current cache duration
    getCacheDuration: async function () {
        if ('serviceWorker' in navigator && navigator.serviceWorker.controller) {
            return new Promise((resolve) => {
                const messageChannel = new MessageChannel();
                messageChannel.port1.onmessage = (event) => {
                    resolve(event.data.duration);
                };
                navigator.serviceWorker.controller.postMessage(
                    { type: 'GET_CACHE_DURATION' },
                    [messageChannel.port2]
                );
            });
        }
        return null;
    },

    // Check if service worker is available
    isAvailable: function () {
        return 'serviceWorker' in navigator && navigator.serviceWorker.controller !== null;
    },

    // Get cache statistics
    getCacheStats: async function () {
        if (!('caches' in window)) {
            console.log('Cache API not supported');
            return { cachedRequestsCount: 0, estimatedSizeBytes: 0 };
        }

        try {
            // Wait for service worker to be ready
            await this._waitForServiceWorker();
            
            // Find the correct cache name dynamically
            const cacheName = await this._getCacheName();
            
            if (!cacheName) {
                console.log('No API cache found yet - this is normal on first load');
                return { cachedRequestsCount: 0, estimatedSizeBytes: 0 };
            }
            
            console.log('Found cache:', cacheName);
            const cache = await caches.open(cacheName);
            const requests = await cache.keys();
            
            console.log('Cache requests count:', requests.length);
            
            let totalSize = 0;
            for (const request of requests) {
                const response = await cache.match(request);
                if (response) {
                    const blob = await response.blob();
                    totalSize += blob.size;
                    console.log('Request:', request.url, 'Size:', blob.size);
                }
            }

            console.log('Total cache size:', totalSize);

            return {
                cachedRequestsCount: requests.length,
                estimatedSizeBytes: totalSize
            };
        } catch (error) {
            console.error('Error getting cache stats:', error);
            return { cachedRequestsCount: 0, estimatedSizeBytes: 0 };
        }
    },

    // Force service worker update
    forceUpdate: async function () {
        if ('serviceWorker' in navigator) {
            const registration = await navigator.serviceWorker.getRegistration();
            if (registration) {
                await registration.update();
                return true;
            }
        }
        return false;
    },

    // Debug: List all caches
    listAllCaches: async function() {
        if (!('caches' in window)) {
            return [];
        }
        try {
            const cacheNames = await caches.keys();
            console.log('All caches:', cacheNames);
            return cacheNames;
        } catch (error) {
            console.error('Error listing caches:', error);
            return [];
        }
    }
};

// Log service worker status
if ('serviceWorker' in navigator) {
    navigator.serviceWorker.ready.then(registration => {
        console.log('Service Worker is ready and controlling the page');
    });

    navigator.serviceWorker.addEventListener('controllerchange', () => {
        console.log('Service Worker controller changed');
    });
}
