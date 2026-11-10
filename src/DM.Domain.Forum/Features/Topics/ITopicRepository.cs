using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Forum.Features.Topics;

public interface ITopicRepository
{
    /// <summary>
    /// Get number of topics matching query; a null <paramref name="boardId" />
    /// counts across boards.
    /// </summary>
    Task<int> Count(Guid? boardId, TopicsQuery query, CancellationToken ct = default);

    /// <summary>
    /// Get list of topics with filtering and sorting. The same method
    /// drives the per-board forum listings and the cross-board user-profile
    /// "Topics" tab: boardId=null switches to cross-board mode.
    /// </summary>
    Task<IEnumerable<Topic>> Get(Guid? boardId, PagingData? pagingData, TopicsQuery query, CancellationToken ct = default);

    Task<Topic?> Get(Guid topicId, CancellationToken ct = default);

    Task<Topic?> GetByBoardAndNumber(Guid boardId, int topicNumber, CancellationToken ct = default);

    /// <summary>
    /// Get the period markers of the given topics in one lookup. Topics
    /// without a marker are absent from the result.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, PeriodDigest>> GetPeriodDigests(
        IReadOnlyCollection<Guid> topicIds, CancellationToken ct = default);

    /// <summary>
    /// Whether a topic row exists at all, INCLUDING removed ones — lets the
    /// service tell a deleted topic (410 Gone) from one that never existed
    /// (404 Not Found).
    /// </summary>
    Task<bool> Exists(Guid topicId, CancellationToken ct = default);

    /// <summary>
    /// Whether a topic row with this board number exists at all, INCLUDING
    /// removed ones — same 410-vs-404 distinction for number lookups.
    /// </summary>
    Task<bool> ExistsByBoardAndNumber(Guid boardId, int topicNumber, CancellationToken ct = default);

    /// <summary>
    /// Get the user's most-liked topic across every board. Soft-deleted topics
    /// are excluded. Returns null when the user has no topics at all.
    /// </summary>
    Task<Topic?> GetBestUserTopic(Guid authorId, CancellationToken ct = default);

    Task<Topic> Create(CreateTopicEntity createTopic, Guid authorId, Guid boardId, CancellationToken ct = default);

    /// <summary>
    /// Update existing topic, moving it to <paramref name="boardId" /> when given
    /// </summary>
    /// <returns>Updated topic DTO, and whether the row actually changed</returns>
    Task<TopicUpdateResult> Update(UpdateTopicEntity updateTopic, Guid? boardId = null);

    /// <summary>
    /// Soft delete a topic and drop the subscriptions to it
    /// </summary>
    Task Delete(Guid topicId, Guid deletedByUserId);

    /// <summary>
    /// Recompute a board's stored topic count and its "last topic" block from the
    /// topics the board actually holds.
    /// </summary>
    /// <remarks>
    /// For a caller that changed a topic behind this repository's back - the
    /// period digest backdates the topic it has just created. Creating, deleting
    /// and moving a topic recompute it themselves. One definition of "the last
    /// topic" and no other.
    /// </remarks>
    Task RefreshBoardSummary(Guid boardId, CancellationToken ct = default);

    /// <summary>
    /// Ids of the board's pinned topics, in the order they are shown now
    /// </summary>
    Task<IReadOnlyList<Guid>> GetAttachedTopicIds(Guid boardId, CancellationToken ct = default);

    /// <summary>
    /// Replace the attach order of the board's pinned topics: a topic's order
    /// becomes its position in <paramref name="orderedTopicIds"/>
    /// </summary>
    /// <remarks>
    /// The board bounds the write. An id belonging to another board is not
    /// written at all, so whose topics move is decided by the board in the
    /// caller's address and not by the ids in its body.
    /// </remarks>
    Task ReplaceAttachOrder(Guid boardId, IReadOnlyList<Guid> orderedTopicIds, CancellationToken ct = default);
}

/// <remarks>
/// A PATCH that round-trips the values the topic already holds answers 200 but is
/// not an edit: it leaves no row in the edit history and announces nothing, both
/// decided by this one fact.
/// </remarks>
/// <param name="Changed">True when the row was actually modified.</param>
public sealed record TopicUpdateResult(Topic Topic, bool Changed);

public class CreateTopicEntity
{
    /// <summary>
    /// Minted by the caller, because the unread markers of the topic are written
    /// before the row is — see UnreadCountersReservation for why that way round.
    /// </summary>
    public required Guid TopicId { get; init; }

    public string Title { get; set; } = null!;

    public string Text { get; set; } = null!;
}

/// <summary>
/// A null field is left as it is.
/// </summary>
public class UpdateTopicEntity
{
    public Guid TopicId { get; set; }

    public string? Title { get; set; }

    public string? Text { get; set; }

    public bool? IsClosed { get; set; }

    /// <summary>
    /// Whether the author is the one closing the topic (written together with
    /// <see cref="IsClosed"/> when the closed state changes, false on reopening)
    /// </summary>
    public bool? ClosedByAuthor { get; set; }

    public bool? IsAttached { get; set; }

    /// <summary>
    /// User performing the edit. Recorded in the topic edit history, which is
    /// the only place the schema keeps "who changed this" for a topic — the row
    /// itself carries the author and nothing else.
    /// </summary>
    public Guid EditorUserId { get; set; }
}
