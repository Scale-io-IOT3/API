using Infrastructure.Services.Foods.Abstract;
using Infrastructure.Services.Foods.Metadata;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Tests;

public sealed class FoodCacheTests
{
    [Fact]
    public async Task ConcurrentRefreshesShareWorkAndNextRefreshCanRun()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var coordinator = new ConsensusCacheCoordinator<Entry, int>(cache, new(), TimeSpan.FromMinutes(1), entry => entry.RefreshedAt);
        var gate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var requests = Enumerable.Range(0, 20).Select(_ => coordinator.RunSharedRefreshAsync("key", () =>
        {
            Interlocked.Increment(ref calls);
            return gate.Task;
        })).ToArray();
        gate.SetResult(42);
        Assert.All(await Task.WhenAll(requests), value => Assert.Equal(42, value));
        Assert.Equal(1, calls);
        Assert.Equal(7, await coordinator.RunSharedRefreshAsync("key", () => Task.FromResult(7)));
    }

    [Fact]
    public async Task FailedRefreshDoesNotBlockRetry()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var coordinator = new ConsensusCacheCoordinator<Entry, int>(cache, new(), TimeSpan.FromMinutes(1), entry => entry.RefreshedAt);
        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.RunSharedRefreshAsync("key", () =>
            Task.FromException<int>(new InvalidOperationException("test failure"))));
        Assert.Equal(7, await coordinator.RunSharedRefreshAsync("key", () => Task.FromResult(7)));
    }

    [Fact]
    public async Task MetadataMissesAreSharedAndNegativelyCached()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = new FoodMetadataCache(memory);
        var gate = new TaskCompletionSource<OpenFoodMetadata?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var requests = Enumerable.Range(0, 20).Select(_ => cache.GetAsync("missing", () =>
        {
            Interlocked.Increment(ref calls);
            return gate.Task;
        })).ToArray();
        gate.SetResult(null);
        Assert.All(await Task.WhenAll(requests), Assert.Null);
        Assert.Null(await cache.GetAsync("missing", () => throw new InvalidOperationException("must use negative cache")));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task MetadataFetchExceptionDoesNotPoisonCacheKey()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = new FoodMetadataCache(memory);
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetAsync("key", () =>
            Task.FromException<OpenFoodMetadata?>(new InvalidOperationException("test failure"))));
        var expected = new OpenFoodMetadata("B", null);
        Assert.Equal(expected, await cache.GetAsync("key", () => Task.FromResult<OpenFoodMetadata?>(expected)));
    }

    private sealed record Entry(DateTimeOffset RefreshedAt);
}
