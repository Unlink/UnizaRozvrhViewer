using Microsoft.JSInterop;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace RozvrhUniza.Services
{
    /// <summary>
    /// Service for interacting with the Service Worker for cache management
    /// </summary>
    public class ServiceWorkerCacheService
    {
        private readonly IJSRuntime _jsRuntime;

        public ServiceWorkerCacheService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        /// <summary>
        /// Clears the API cache in the Service Worker
        /// </summary>
        public async Task ClearApiCacheAsync()
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("serviceWorkerInterop.clearCache");
            }
            catch
            {
                // Service Worker might not be available in development
            }
        }

        /// <summary>
        /// Sets the cache duration
        /// </summary>
        /// <param name="duration">Duration key: "1h", "1d", "7d", or "30d"</param>
        public async Task SetCacheDurationAsync(string duration)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("serviceWorkerInterop.setCacheDuration", duration);
            }
            catch
            {
                // Service Worker might not be available
            }
        }

        /// <summary>
        /// Gets the current cache duration in milliseconds
        /// </summary>
        public async Task<long?> GetCacheDurationAsync()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<long?>("serviceWorkerInterop.getCacheDuration");
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Checks if Service Worker is supported and registered
        /// </summary>
        public async Task<bool> IsServiceWorkerAvailableAsync()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<bool>("serviceWorkerInterop.isAvailable");
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets cache statistics (cache size, number of entries)
        /// </summary>
        public async Task<CacheStatistics> GetCacheStatisticsAsync()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<CacheStatistics>("serviceWorkerInterop.getCacheStats");
            }
            catch
            {
                return new CacheStatistics();
            }
        }

        /// <summary>
        /// Lists all available cache names (for debugging)
        /// </summary>
        public async Task<List<string>> ListAllCachesAsync()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<List<string>>("serviceWorkerInterop.listAllCaches");
            }
            catch
            {
                return new List<string>();
            }
        }
    }

    public class CacheStatistics
    {
        public int CachedRequestsCount { get; set; }
        public long EstimatedSizeBytes { get; set; }
        public string EstimatedSizeFormatted => FormatBytes(EstimatedSizeBytes);

        private string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F2} KB";
            return $"{bytes / (1024.0 * 1024.0):F2} MB";
        }
    }
}
