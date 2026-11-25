using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using LibraryManagement.API.Configuration;
using LibraryManagement.API.Services;
using Xunit;

namespace LibraryManagement.Tests;

public sealed class CacheServiceTests
{
    private static MemoryCacheService CreateService(bool enabled = true)
    {
        var memory = new MemoryCache(new MemoryCacheOptions());
        var opts = Options.Create(new CacheOptions
        {
            Enabled = enabled,
            DashboardExpirationSeconds = 60,
            LookupExpirationSeconds = 300,
            BooksExpirationSeconds = 120,
            ReportsExpirationSeconds = 90,
            SlidingExpirationSeconds = 45
        });

        return new MemoryCacheService(memory, opts);
    }

    [Fact]
    public async Task GetOrCreateAsync_CachesAndInvalidatesByGroup()
    {
        var cache = CreateService();
        var calls = 0;

        var first = await cache.GetOrCreateAsync(CacheGroups.Books, "books:p1", TimeSpan.FromSeconds(60), () =>
        {
            calls++;
            return Task.FromResult("book-v1");
        });

        var second = await cache.GetOrCreateAsync(CacheGroups.Books, "books:p1", TimeSpan.FromSeconds(60), () =>
        {
            calls++;
            return Task.FromResult("book-v2");
        });

        Assert.False(first.CacheHit);
        Assert.True(second.CacheHit);
        Assert.Equal("book-v1", second.Value);
        Assert.Equal(1, calls);

        cache.InvalidateGroups(CacheGroups.Books, CacheGroups.Dashboard);

        var third = await cache.GetOrCreateAsync(CacheGroups.Books, "books:p1", TimeSpan.FromSeconds(60), () =>
        {
            calls++;
            return Task.FromResult("book-v3");
        });

        Assert.False(third.CacheHit);
        Assert.Equal("book-v3", third.Value);
        Assert.Equal(2, calls);
    }
}
