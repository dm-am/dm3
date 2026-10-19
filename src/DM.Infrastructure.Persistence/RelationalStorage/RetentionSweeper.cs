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
/// The single registry of retention policies. Declarative, so RetentionShould can
/// require every table of the context to be here or be named bounded state.
/// </summary>
/// <remarks>
/// Login attempts decay in the window the lockout policy reads, owned by
/// <see cref="AuthenticationConfiguration"/>; the security trail and the notification
/// stream hold personal data as long as an incident can be investigated; a tombstoned
/// unread marker and a published outbox row are done, and seven days is slack.
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
