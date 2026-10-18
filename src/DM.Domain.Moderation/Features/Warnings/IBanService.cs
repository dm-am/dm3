using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;

namespace DM.Domain.Moderation.Features.Warnings;

/// <summary>
/// Service for ban management
/// </summary>
public interface IBanService
{
    /// <summary>
    /// Get bans for a user
    /// </summary>
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
    /// Not a decision and not a sanction of its own: it holds the reader at arm's
    /// length for two days so that a senior moderator or an administrator has time
    /// to come and settle the case. It answers to nobody's role - the moderator
    /// whose warning tipped the scale need not be able to ban - and it refuses
    /// itself when a ban is already running, so it never stacks on one.
    /// </remarks>
    /// <param name="targetUserId">Who the points belong to</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The ban that was issued, or null when none was</returns>
    Task<Ban?> IssueAutomaticBan(Guid targetUserId, CancellationToken ct = default);

    /// <summary>
    /// Lift (cancel) a ban early (senior moderators; permanent bans - admins only)
    /// </summary>
    /// <remarks>
    /// The reason is required: the lift is drawn in the ban list as a decision of
    /// its own under the ban, and one without words would be the only decision in
    /// the list nobody explained.
    /// </remarks>
    Task LiftBan(Guid banId, string reason, CancellationToken ct = default);
}

/// <summary>
/// DTO for creating a ban
/// </summary>
public class CreateBan
{
    /// <summary>
    /// Target username
    /// </summary>
    public string Username { get; set; } = "";

    /// <summary>
    /// Ban expiration time (null for permanent)
    /// </summary>
    public DateTimeOffset? ExpiresUtc { get; set; }

    /// <summary>
    /// Ban duration in hours (alternative to ExpiresUtc)
    /// </summary>
    public int? DurationHours { get; set; }

    /// <summary>
    /// Ban comment/reason
    /// </summary>
    public string Comment { get; set; } = "";

    /// <summary>
    /// Ban access restriction scope ("Тип бана", 4.2.4.2):
    /// <see cref="AccessPolicy.DemocraticBan"/> or <see cref="AccessPolicy.FullBan"/>.
    /// What either scope forbids is stated once — on those members and in
    /// <see cref="DM.Domain.Core.Authorization.AccessRestrictions"/> — and is not
    /// retold here: the paraphrase that used to stand in this place had drifted
    /// into calling the ordinary ban read-only, which it never was. The service
    /// refuses anything other than the two with 400; the service still coerces
    /// behind it, so an in-process caller cannot produce a weaker ban than the safe
    /// default either. Defaults to FullBan so that
    /// existing callers (e.g. ticket resolution) keep the full restriction.
    /// </summary>
    public AccessPolicy AccessRestrictionPolicy { get; set; } = AccessPolicy.FullBan;

    /// <summary>
    /// Whether this is a voluntary self-ban
    /// </summary>
    public bool IsVoluntary { get; set; }
}
