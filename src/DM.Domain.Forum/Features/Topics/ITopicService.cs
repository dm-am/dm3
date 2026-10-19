using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Forum.Features.Topics;

public interface ITopicService
{
    Task<Topic> CreateAsync(CreateTopic createTopic, CancellationToken ct = default);

    /// <summary>
    /// Whether the current user may open a topic on the board: true exactly when
    /// <see cref="CreateAsync" /> takes one there, the title and the text aside.
    /// The page offers "Создать топик" by this answer instead of refusing what
    /// was typed.
    /// </summary>
    /// <param name="board">The board, read for the current user</param>
    bool MayCreate(Boards.Board board);

    /// <summary>
    /// Whether signing in is the way to a topic on the board: the current user
    /// is a guest, and an ordinary signed-in member may open topics there. False
    /// for anybody signed in, and on a board whose topics come from the
    /// moderation (the news, the contests): signing in opens nothing there for a
    /// member, so the page does not invite a guest to it.
    /// </summary>
    /// <param name="board">The board, read for the current user</param>
    bool MayCreateAfterSignIn(Boards.Board board);

    Task<Topic> GetAsync(Guid topicId, CancellationToken ct = default);

    Task<Topic> GetByBoardAndNumberAsync(string boardAlias, int topicNumber, CancellationToken ct = default);

    /// <summary>
    /// Get topics page of certain board by its title with filtering and sorting
    /// </summary>
    /// <returns>Topics list and paging data (paging is null for attached-only queries)</returns>
    Task<(IEnumerable<Topic> topics, PagingResult? paging)> GetListAsync(
        string boardTitle, TopicsQuery query, CancellationToken ct = default);

    /// <summary>
    /// Get topics page across all boards visible to the current viewer, for the
    /// user-profile "Topics" tab. Topics on boards the viewer cannot see are
    /// filtered out at the SQL layer.
    /// </summary>
    Task<(IEnumerable<Topic> topics, PagingResult? paging)> GetListAcrossBoardsAsync(
        TopicsQuery query, CancellationToken ct = default);

    /// <summary>
    /// Get a user's best (most-liked) topic across every board visible to
    /// the current viewer, for the profile "Topics" widget. Returns null when
    /// the user has no visible topics.
    /// </summary>
    Task<Topic?> GetBestUserTopicAsync(string username, CancellationToken ct = default);

    /// <summary>
    /// Get the period markers of already-fetched topics in one lookup. Topics
    /// without a marker are absent from the result.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, PeriodDigest>> GetPeriodDigestsAsync(
        IReadOnlyCollection<Guid> topicIds, CancellationToken ct = default);

    Task<Topic> UpdateAsync(UpdateTopic updateTopic, CancellationToken ct = default);

    Task DeleteAsync(Guid topicId, CancellationToken ct = default);

    /// <summary>
    /// Replace the order of a board's pinned topics; <paramref name="topicIds"/>
    /// in the desired order (first = top), the board by title or alias
    /// </summary>
    /// <remarks>
    /// The whole order, not a correction to it: <paramref name="topicIds"/> names
    /// every pinned topic of the board exactly once, and a list that does not is
    /// refused.
    /// </remarks>
    Task ReorderPinnedAsync(string boardTitle, IReadOnlyList<Guid> topicIds, CancellationToken ct = default);
}
