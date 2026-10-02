using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Services.Foods.Shared;

/// <summary>Defines consensus cache expiration and the configured stale-while-revalidate interval.</summary>
internal static class FoodCachePolicy
{
    internal static MemoryCacheEntryOptions CreateOptions() => new()
    {
        SlidingExpiration = TimeSpan.FromHours(1),
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12)
    };

    internal static TimeSpan ReadStaleAfter(IConfiguration configuration) =>
        TimeSpan.FromSeconds(Math.Max(30,
            SourceSettingsResolver.ReadGlobalInt(configuration, "StaleWhileRevalidateSeconds", 1800)));
}
