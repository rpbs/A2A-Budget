using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace A2A_Tech.Services
{
    public interface IPricingService
    {
        Task<decimal?> GetUnitPriceAsync(string serviceName, string region);
    }

    public class AzureRetailPricingService : IPricingService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AzureRetailPricingService> _logger;

        public AzureRetailPricingService(IHttpClientFactory httpClientFactory, IMemoryCache cache, ILogger<AzureRetailPricingService> logger)
        {
            _httpClient = httpClientFactory.CreateClient();
            _cache = cache;
            _logger = logger;
        }

        public async Task<decimal?> GetUnitPriceAsync(string serviceName, string region)
        {
            _logger.LogInformation("Fetching price for service '{ServiceName}' in region '{Region}'", serviceName, region);

            string cacheKey = $"price::{serviceName}::{region}";
            if (_cache.TryGetValue(cacheKey, out decimal cached))
            {
                _logger.LogDebug("Cache hit for service '{ServiceName}' in region '{Region}'. Price: {Price}", serviceName, region, cached);
                return cached;
            }

            _logger.LogDebug("Cache miss for service '{ServiceName}' in region '{Region}'. Fetching from Azure Pricing API", serviceName, region);

            var url = $"https://prices.azure.com/api/retail/prices?$filter=serviceName eq '{Uri.EscapeDataString(serviceName)}' and armRegionName eq '{Uri.EscapeDataString(region)}'";

            using var resp = await _httpClient.GetAsync(url);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch price from Azure Pricing API. Status Code: {StatusCode}", resp.StatusCode);
                return null;
            }

            _logger.LogDebug("Successfully received response from Azure Pricing API");

            using var stream = await resp.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);

            if (!doc.RootElement.TryGetProperty("Items", out var items))
            {
                _logger.LogWarning("Response from Azure Pricing API does not contain 'Items' property");
                return null;
            }

            var first = items.EnumerateArray().FirstOrDefault();
            if (first.ValueKind == JsonValueKind.Undefined)
            {
                _logger.LogWarning("No items found in Azure Pricing API response for service '{ServiceName}' in region '{Region}'", serviceName, region);
                return null;
            }

            if (!first.TryGetProperty("unitPrice", out var priceProp))
            {
                _logger.LogWarning("First item in Azure Pricing API response does not contain 'unitPrice' property");
                return null;
            }

            var price = priceProp.GetDecimal();
            _logger.LogInformation("Retrieved price for service '{ServiceName}' in region '{Region}': {Price}", serviceName, region, price);

            _cache.Set(cacheKey, price, TimeSpan.FromHours(1));
            _logger.LogDebug("Cached price for service '{ServiceName}' in region '{Region}' for 1 hour", serviceName, region);

            return price;
        }
    }
}
