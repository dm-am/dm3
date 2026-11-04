using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Moderation.Features.Warnings;

namespace DM.Domain.Moderation.Features.Bans;

public interface IBanService
{
    Task<IEnumerable<Ban>> GetUserBans(string username, CancellationToken ct = default);

    /// <summary>
    /// Get active ban for a user (if any)
    /// </summary>
    Task<Ban?> GetActiveBan(string username, CancellationToken ct = default);

    /// <summary>
    /// Get all active bans (for moderators)
    /// </summary>
    Task<IEnumerable<Ban>> GetAllActiveBans(CancellationToken ct = default);

    /// <summary>
    /// Get a page of the ban list (for moderators): every issue and every lift as
    /// an entry of its own, newest first, each ban marked with whether the reader
    /// may lift it
    /// </summary>
    /// <param name="username">Whose bans, or everybody's when empty; a name nobody
    /// carries is an empty page</param>
    /// <param name="skip">Entries to skip</param>
    /// <param name="take">Entries to take</param>
    /// <param name="entry">The number of an entry (<see cref="BanLogEntry.Number" />)
    /// whose page is asked for instead of the one <paramref name="skip" /> names;
    /// an entry this list does not have leaves the page to <paramref name="skip" /></param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The entries of the page, how many entries the list has, and how
    /// many it skipped, which is the page an entry was asked for on</returns>
    Task<(IReadOnlyList<BanLogEntry> Entries, int TotalCount, int Skip)> GetBanHistory(
        string? username, int skip, int take, int? entry = null, CancellationToken ct = default);

    /// <summary>
    /// Whether the current reader may lift the ban: the same rule
    /// <see cref="LiftBan" /> refuses by, read without the write
    /// </summary>
    bool MayLift(Ban ban);

    /// <summary>
    /// Create a ban (senior moderators; users can create voluntary self-bans)
    /// </summary>
    Task<Ban> CreateBan(CreateBan createBan, CancellationToken ct = default);

    /// <summary>
    /// Issue the automatic ban the warning points call for: the ordinary scope,
    /// <see cref="WarningPolicy.AutoBanDurationHours" /> hours, filed under the
    /// system account.
    /// </summary>
    /// <remarks>
    /// It holds the reader off until a senior moderator or an administrator settles the
    /// case, so it answers to nobody's role and never stacks on a running ban.
    /// </remarks>
    /// <returns>The ban that was issued, or null when none was</returns>
    Task<Ban?> IssueAutomaticBan(Guid targetUserId, CancellationToken ct = default);

    /// <summary>
    /// Lift (cancel) a ban early (senior moderators; permanent bans - admins only).
    /// The reason is required: the lift stands in the ban list as a decision of its own.
    /// </summary>
    Task LiftBan(Guid banId, string reason, CancellationToken ct = default);
}
