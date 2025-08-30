using Core.Interface;
using Microsoft.Extensions.Caching.Memory;

namespace Infrastructure.Services;

public abstract class CachedService<TResponse>(IMemoryCache cache) : IService<TResponse>
{
    private readonly MemoryCacheEntryOptions _options = new()
    {
        SlidingExpiration = TimeSpan.FromHours(1),
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12)
    };
    
    public async Task<TResponse?> FetchAsync(string input, double? grams)
    {
        var key = GenerateKey(input, grams);
        if (cache.TryGetValue(key, out TResponse? cached)) return cached;

        var response = await Fetch(input, grams);
        cache.Set(key, response, _options);

        return response;
    }

    protected abstract string GenerateKey(string input, double? grams);
    protected abstract Task<TResponse?> Fetch(string input, double? grams);
}