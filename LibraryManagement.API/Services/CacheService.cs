using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using LibraryManagement.API.Configuration;

namespace LibraryManagement.API.Services;

public static class CacheGroups
{
    public const string Dashboard = "dashboard";
    public const string Categories = "categories";
    public const string Authors = "authors";
    public const string Publishers = "publishers";
    public const string Books = "books";
    public const string Members = "members";
    public const string Reports = "reports";
}

public interface ICacheService
{
    Task<(T Value, bool CacheHit)> GetOrCreateAsync<T>(string group, string key, TimeSpan absoluteExpiration, Func<Task<T>> factory);
    (T Value, bool CacheHit) GetOrCreate<T>(string group, string key, TimeSpan absoluteExpiration, Func<T> factory);
    void InvalidateGroup(string group);
    void InvalidateGroups(params string[] groups);
    void InvalidateAll();
}

// IMemoryCache wrapper using per-group CancellationChangeTokens so writes clear related cached pages together.
public sealed class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly CacheOptions _options;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _tokens = new(StringComparer.OrdinalIgnoreCase);

    public MemoryCacheService(IMemoryCache cache, IOptions<CacheOptions> options)
    {
        _cache = cache;
        _options = options.Value;
    }

    public async Task<(T Value, bool CacheHit)> GetOrCreateAsync<T>(
        string group,
        string key,
        TimeSpan absoluteExpiration,
        Func<Task<T>> factory)
    {
        if (!_options.Enabled) return (await factory(), false);

        if (_cache.TryGetValue(key, out T? cached) && cached is not null)
        {
            return (cached, true);
        }

        var value = await factory();
        SetEntry(group, key, value, absoluteExpiration);
        return (value, false);
    }

    public (T Value, bool CacheHit) GetOrCreate<T>(
        string group,
        string key,
        TimeSpan absoluteExpiration,
        Func<T> factory)
    {
        if (!_options.Enabled) return (factory(), false);

        if (_cache.TryGetValue(key, out T? cached) && cached is not null)
        {
            return (cached, true);
        }

        var value = factory();
        SetEntry(group, key, value, absoluteExpiration);
        return (value, false);
    }

    public void InvalidateGroup(string group)
    {
        if (_tokens.TryRemove(group, out var cts))
        {
            try { cts.Cancel(); }
            finally { cts.Dispose(); }
        }
    }

    public void InvalidateGroups(params string[] groups)
    {
        foreach (var group in groups)
        {
            InvalidateGroup(group);
        }
    }

    public void InvalidateAll()
    {
        foreach (var group in _tokens.Keys.ToList())
        {
            InvalidateGroup(group);
        }
    }

    private void SetEntry<T>(string group, string key, T value, TimeSpan absoluteExpiration)
    {
        var tokenSource = _tokens.GetOrAdd(group, _ => new CancellationTokenSource());
        var entryOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = absoluteExpiration
        };

        if (_options.SlidingExpirationSeconds > 0)
        {
            var sliding = TimeSpan.FromSeconds(_options.SlidingExpirationSeconds);
            if (sliding < absoluteExpiration)
            {
                entryOptions.SlidingExpiration = sliding;
            }
        }

        entryOptions.AddExpirationToken(new CancellationChangeToken(tokenSource.Token));
        _cache.Set(key, value, entryOptions);
    }
}
