using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Community.Features.Statistics;

/// <summary>
/// Read model behind the community statistics block
/// </summary>
public interface ICommunityStatsRepository
{
    /// <summary>
    /// Count the live totals, today's deltas and the week's best post
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    Task<LiveStats> GetLiveStats(CancellationToken ct = default);

    /// <summary>
    /// Aggregate every top list over the [start, end) window
    /// </summary>
    /// <param name="start">Inclusive lower bound</param>
    /// <param name="end">Exclusive upper bound</param>
    /// <param name="ct">Cancellation token</param>
    Task<LeaderboardBoards> GetLeaderboards(DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default);
}

/// <summary>
/// The top lists as the database returns them: positive scores only, cut to
/// the board size, without the display ranks the service assigns
/// </summary>
public class LeaderboardBoards
{
    /// <summary>Top players by rating received</summary>
    public List<LeaderboardEntry> TopPlayersByRating { get; init; } = [];

    /// <summary>Top players by posts count</summary>
    public List<LeaderboardEntry> TopPlayersByPosts { get; init; } = [];

    /// <summary>Top games by total rating</summary>
    public List<LeaderboardEntry> TopGamesByRating { get; init; } = [];

    /// <summary>Top games by posts count</summary>
    public List<LeaderboardEntry> TopGamesByPosts { get; init; } = [];

    /// <summary>Top players by written text volume</summary>
    public List<LeaderboardEntry> TopPlayersByVolume { get; init; } = [];

    /// <summary>Top blogs by total rating</summary>
    public List<LeaderboardEntry> TopBlogsByRating { get; init; } = [];

    /// <summary>Top blogs by publications count</summary>
    public List<LeaderboardEntry> TopBlogsByPosts { get; init; } = [];

    /// <summary>Top blog authors by written text volume</summary>
    public List<LeaderboardEntry> TopBlogAuthorsByVolume { get; init; } = [];
}
