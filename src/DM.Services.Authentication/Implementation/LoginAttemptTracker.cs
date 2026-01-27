using System;
using System.Threading.Tasks;
using DM.Services.Core.Caching;

namespace DM.Services.Authentication.Implementation;

/// <inheritdoc />
internal class LoginAttemptTracker : ILoginAttemptTracker
{
    private readonly ICache _cache;
    private const string CacheKeyPrefix = "login_attempts:";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(24);

    /// <inheritdoc />
    public LoginAttemptTracker(ICache cache)
    {
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<int> GetDelayForUser(string login)
    {
        var attempts = await GetAttemptCount(login);
        return CalculateDelay(attempts);
    }

    /// <inheritdoc />
    public async Task RecordFailedAttempt(string login)
    {
        var cacheKey = GetCacheKey(login);
        var currentAttempts = await GetAttemptCount(login);
        var newAttempts = currentAttempts + 1;

        // Store the new count with 24-hour expiration
        await _cache.GetOrCreate(
            cacheKey,
            () => Task.FromResult(newAttempts),
            CacheExpiration);
    }

    /// <inheritdoc />
    public async Task ResetAttempts(string login)
    {
        var cacheKey = GetCacheKey(login);
        await _cache.Invalidate(cacheKey);
    }

    private async Task<int> GetAttemptCount(string login)
    {
        var cacheKey = GetCacheKey(login);
        try
        {
            return await _cache.GetOrCreate(
                cacheKey,
                () => Task.FromResult(0),
                CacheExpiration);
        }
        catch
        {
            return 0;
        }
    }

    private static string GetCacheKey(string login) =>
        $"{CacheKeyPrefix}{login.ToLowerInvariant()}";

    private static int CalculateDelay(int attempts)
    {
        return attempts switch
        {
            < 3 => 0,      // 0-2 attempts: no delay
            < 5 => 1,      // 3-4 attempts: 1 second
            < 10 => 5,     // 5-9 attempts: 5 seconds
            _ => 30        // 10+ attempts: 30 seconds
        };
    }
}
