using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Retention;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DM.Infrastructure.Persistence.RelationalStorage;

/// <summary>
/// One retention policy: the table, the column the term is measured from, and
/// the term. A row whose stamp is older than the term is deleted by the sweep.
/// </summary>
/// <param name="Table">Table the policy governs.</param>
/// <param name="TimestampColumn">Column the term is measured from.</param>
/// <param name="Term">How long a row lives past its stamp.</param>
public sealed record RetentionPolicy(string Table, string TimestampColumn, TimeSpan Term);

/// <summary>
/// The single registry of retention policies, replacing the TTL indexes of the
/// document store. Declarative on purpose: the architecture suite reads this
/// list and requires every table of the context to either be here or be named
/// bounded state, so silence about a growing stream is impossible.
/// </summary>
/// <remarks>
/// The first four terms carry over from the TTL era unchanged: login attempts
/// decay in the window the lockout policy reads, the security trail and the
/// notification stream hold personal data for as long as an incident can still
/// be investigated, and a tombstoned unread marker's job is over in minutes —
/// seven days is slack, not a requirement. Only the login-attempt term lives in
/// configuration, because <see cref="AuthenticationConfiguration"/> already
/// owns it; the mirror constant the index descriptor used to freeze is gone.
/// The outbox term is younger than the TTL era (W1.5) and follows the same
/// tombstone pattern: the stamp only exists on rows whose work is done.
/// </remarks>
internal static class RetentionRegistry
{
    /// <summary>
    /// Every table that is a stream, with its term.
    /// </summary>
    /// <param name="authentication">Source of the login-attempt term.</param>
    public static IReadOnlyList<RetentionPolicy> Policies(AuthenticationConfiguration authentication) =>
    [
        new("LoginAttempts", "LastAttemptUtc", TimeSpan.FromHours(authentication.LoginAttemptExpirationHours)),
        new("SecurityAuditEntries", "TimestampUtc", TimeSpan.FromDays(180)),
        new("Notifications", "CreatedUtc", TimeSpan.FromDays(180)),
        // Only tombstones carry a stamp: a live marker's RemovedUtc is NULL and
        // never matches the cutoff predicate.
        new("UnreadCounters", "RemovedUtc", TimeSpan.FromDays(7)),
        // The same trick as the tombstones above: only published rows carry the
        // stamp, so an unpublished event is never deleted however old it grows -
        // the backlog alert calls a human instead. A week of published rows
        // covers investigating a delivery incident against the consumer's log.
        new("OutboxEvents", "PublishedUtc", TimeSpan.FromDays(7)),
        // A login that stopped between the two factors. It is worth stealing for
        // five minutes and worth nothing after that; the day is slack for the
        // case where the deletion at the end of a login did not happen, and not
        // a second lifetime.
        new("TwoFactorChallenges", "CreatedUtc", TimeSpan.FromDays(1)),
    ];
}

/// <inheritdoc />
internal class RetentionSweeper : IRetentionSweepProcessor
{
    private readonly DmDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly AuthenticationConfiguration _authenticationConfiguration;

    public RetentionSweeper(
        DmDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        IOptions<AuthenticationConfiguration> authenticationConfiguration)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _authenticationConfiguration = authenticationConfiguration.Value;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RetentionSweepResult>> SweepAsync(
        CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider.Now;
        var results = new List<RetentionSweepResult>();

        foreach (var policy in RetentionRegistry.Policies(_authenticationConfiguration))
        {
            // The identifiers come from the registry above — compile-time
            // constants — and only the cutoff travels as a parameter, which is
            // why raw SQL is sound here.
            var cutoff = now - policy.Term;
            var statement =
                $$"""DELETE FROM "{{policy.Table}}" WHERE "{{policy.TimestampColumn}}" < {0}""";
            var deleted = await _dbContext.Database.ExecuteSqlRawAsync(
                statement, [cutoff], cancellationToken);
            results.Add(new RetentionSweepResult(policy.Table, deleted));
        }

        return results;
    }
}
