using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Core.Abstractions;
using Microsoft.Extensions.Options;

namespace DM.Domain.Account.Features.Authentication;

/// <inheritdoc />
/// <remarks>
/// Uses MongoDB for cluster-safe storage of login attempts.
/// All nodes share the same state, enabling proper rate limiting across the cluster.
/// </remarks>
internal class LoginAttemptTracker : ILoginAttemptTracker
{
    private readonly ILoginAttemptRepository _repository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly AuthenticationConfiguration _config;

    /// <inheritdoc />
    public LoginAttemptTracker(
        ILoginAttemptRepository repository,
        IDateTimeProvider dateTimeProvider,
        IOptions<AuthenticationConfiguration> authConfig)
    {
        _repository = repository;
        _dateTimeProvider = dateTimeProvider;
        _config = authConfig.Value;
    }

    /// <inheritdoc />
    public async Task<int> GetDelayForUser(string email)
    {
        var attempts = await _repository.GetFailedAttemptCount(email);
        return CalculateDelay(attempts);
    }

    /// <inheritdoc />
    public async Task<bool> IsAccountLocked(string email)
    {
        var lockoutStart = await _repository.GetLockoutStart(email);
        if (lockoutStart == null)
        {
            return false;
        }

        var lockoutEnd = lockoutStart.Value.AddMinutes(_config.AccountLockoutDurationMinutes);
        var isLocked = _dateTimeProvider.Now.UtcDateTime < lockoutEnd;

        // Clear expired lockout
        if (!isLocked)
        {
            await _repository.ResetAttempts(email);
        }

        return isLocked;
    }

    /// <inheritdoc />
    public async Task<int> GetRemainingLockoutSeconds(string email)
    {
        var lockoutStart = await _repository.GetLockoutStart(email);
        if (lockoutStart == null)
        {
            return 0;
        }

        var lockoutEnd = lockoutStart.Value.AddMinutes(_config.AccountLockoutDurationMinutes);
        var remaining = lockoutEnd - _dateTimeProvider.Now.UtcDateTime;

        return remaining.TotalSeconds > 0 ? (int)remaining.TotalSeconds : 0;
    }

    /// <inheritdoc />
    public async Task RecordFailedAttempt(string email)
    {
        var newAttempts = await _repository.RecordFailedAttempt(email);

        // Check if we should lock the account
        if (newAttempts >= _config.AccountLockoutThreshold)
        {
            await _repository.SetLockout(email, _dateTimeProvider.Now.UtcDateTime);
        }
    }

    /// <inheritdoc />
    public Task ResetAttempts(string email)
    {
        return _repository.ResetAttempts(email);
    }

    private int CalculateDelay(int attempts)
    {
        // Use configured delay schedule, or fallback to defaults
        var schedule = _config.LoginDelaySchedule;
        if (schedule == null || schedule.Length == 0)
        {
            // Default schedule if not configured
            return attempts switch
            {
                < 3 => 0,
                < 5 => 1,
                < 10 => 5,
                _ => 30
            };
        }

        // Find the highest matching threshold
        var matchingEntry = schedule
            .Where(entry => entry.Length >= 2 && attempts >= entry[0])
            .OrderByDescending(entry => entry[0])
            .FirstOrDefault();

        return matchingEntry?[1] ?? 0;
    }
}
