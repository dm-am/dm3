using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Moderation.Features.Bans;

public interface IBanRepository
{
    /// <summary>
    /// Get every ban a user ever received: running, expired and lifted
    /// </summary>
    Task<IEnumerable<Ban>> GetUserBans(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get active ban for a user (if any)
    /// </summary>
    Task<Ban?> GetActiveBan(Guid userId, CancellationToken ct = default);

    Task<Ban?> Get(Guid banId, CancellationToken ct = default);

    Task<Ban> Create(CreateBanEntity ban, CancellationToken ct = default);

    /// <summary>
    /// Lift a ban early. The row stays; only the audit below is written.
    /// </summary>
    Task Lift(Guid banId, Guid liftedByUserId, DateTimeOffset liftedUtc, string? reason,
        CancellationToken ct = default);

    Task<IEnumerable<Ban>> GetAllActiveBans(CancellationToken ct = default);

    /// <summary>
    /// Get a page of the ban list: every issue and every lift as an entry of its
    /// own, newest first, a lift before an issue of the same moment
    /// </summary>
    /// <param name="targetUserId">Whose bans, or everybody's when null</param>
    /// <param name="skip">Entries to skip</param>
    /// <param name="take">Entries to take</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The entries of the page, each with its number, and how many
    /// entries the list has</returns>
    Task<(IReadOnlyList<BanLogEntry> Entries, int TotalCount)> GetBanHistory(
        Guid? targetUserId, int skip, int take, CancellationToken ct = default);

    /// <summary>
    /// Where the entry of the given number stands in the ban list, newest first
    /// </summary>
    /// <param name="targetUserId">Whose bans, or everybody's when null</param>
    /// <param name="number">The number of the entry (<see cref="BanLogEntry.Number" />)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>How many entries of the list stand above it, or null when there
    /// is no entry of that number or it is not in this list</returns>
    Task<int?> GetBanHistoryPosition(Guid? targetUserId, int number, CancellationToken ct = default);
}
