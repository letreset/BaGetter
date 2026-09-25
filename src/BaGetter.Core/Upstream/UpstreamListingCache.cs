using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;

namespace BaGetter.Core.Upstream;

/// <summary>
/// The process-wide, in-memory store behind <see cref="Clients.CachingUpstreamClient"/>. It owns its
/// own <see cref="MemoryCache"/> so its size limit neither applies to nor breaks other users of
/// the app's shared <see cref="IMemoryCache"/>.
/// </summary>
public sealed class UpstreamListingCache : IDisposable
{
    // Each entry's size is the number of versions or packages it holds, so the limit bounds the
    // number of cached listing items across all feeds rather than the number of package ids.
    private const long SizeLimit = 100_000;

    private readonly MemoryCache _cache;

    public UpstreamListingCache()
        : this(clock: null)
    {
    }

    internal UpstreamListingCache(ISystemClock clock)
    {
        _cache = new MemoryCache(new MemoryCacheOptions
        {
            Clock = clock,
            SizeLimit = SizeLimit,
        });
    }

    /// <summary>
    /// Returns the cached listing for <paramref name="key"/>, or runs <paramref name="list"/> and
    /// caches its result for <paramref name="duration"/>. Empty listings are not cached: upstream
    /// clients return an empty list both for unknown packages and for failed requests.
    /// </summary>
    public async Task<IReadOnlyList<T>> GetOrListAsync<T>(object key, TimeSpan duration, Func<Task<IReadOnlyList<T>>> list)
    {
        if (_cache.TryGetValue(key, out IReadOnlyList<T> cached))
            return cached;

        var result = await list();

        if (result.Count > 0)
        {
            _cache.Set(key, result, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = duration,
                Size = result.Count,
            });
        }

        return result;
    }

    public void Dispose()
    {
        _cache.Dispose();
    }
}
