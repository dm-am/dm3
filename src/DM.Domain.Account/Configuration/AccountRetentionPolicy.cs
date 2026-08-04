using System;

namespace DM.Domain.Account.Configuration;

/// <summary>
/// How long the account area keeps data that has served its purpose.
/// </summary>
/// <remarks>
/// These are product decisions — how long a spent token stays readable, how long
/// an unfinished registration holds its email address, how long a moderator has
/// to look at a name change — and every one of them used to be a literal inside
/// a background job of the HTTP host, where nothing outside a running host could
/// read it, call it or test it.
/// </remarks>
public static class AccountRetentionPolicy
{
    /// <summary>
    /// How long a token stays in the store after it was issued.
    /// </summary>
    /// <remarks>
    /// Longer than every token lifetime in <see cref="TokenConfiguration" />, so a
    /// token is expired well before it is deleted: a user who follows a stale link
    /// is told the link expired instead of being told nothing was ever issued.
    /// </remarks>
    public static readonly TimeSpan TokenRetention = TimeSpan.FromDays(7);

    /// <summary>
    /// How long a registration that was never activated holds its email address.
    /// </summary>
    /// <remarks>
    /// Until this passes the address cannot be registered again, which is what
    /// makes the window a product decision rather than a cleanup detail.
    /// </remarks>
    public static readonly TimeSpan PendingRegistrationLifetime = TimeSpan.FromDays(7);

    /// <summary>
    /// How long a name change request waits for a moderator before it expires
    /// itself.
    /// </summary>
    public static readonly TimeSpan UsernameChangeReviewWindow = TimeSpan.FromDays(7);
}
