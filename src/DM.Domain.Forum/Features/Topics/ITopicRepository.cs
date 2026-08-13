using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Forum.Features.Topics;

/// <summary>
/// Repository for forum topics (unified CRUD operations)
/// </summary>
public interface ITopicRepository
{
    // --- READ ---

    /// <summary>
    /// Get number of topics matching query.
    /// </summary>
    /// <param name="boardId">Board identifier (null = cross-board, scoped by access policy)</param>
    /// <param name="accessPolicy">Board access policy of the viewer — applied only when boardId is null</param>
    /// <param name="query">Filter parameters</param>
    /// <param name="ct">Cancellation token</param>
    Task<int> Count(Guid? boardId, BoardAccessPolicy accessPolicy, TopicsQuery query, CancellationToken ct = default);

    /// <summary>
    /// Get list of topics with filtering and sorting. The same method
    /// drives the per-board forum listings and the cross-board user-profile
    /// "Topics" tab — boardId=null switches to cross-board mode, in which
    /// the access policy mask filters out topics the viewer can't see.
    /// </summary>
    /// <param name="boardId">Board identifier (null = cross-board, scoped by access policy)</param>
    /// <param name="accessPolicy">Board access policy of the viewer — applied only when boardId is null</param>
    /// <param name="pagingData">Paging data</param>
    /// <param name="query">Filter and sort parameters</param>
    /// <param name="ct">Cancellation token</param>
    Task<IEnumerable<Topic>> Get(Guid? boardId, BoardAccessPolicy accessPolicy, PagingData? pagingData, TopicsQuery query, CancellationToken ct = default);

    /// <summary>
    /// Get single topic by identifier
    /// </summary>
    /// <param name="topicId">Topic identifier</param>
    /// <param name="accessPolicy">Board access policy</param>
    /// <param name="ct">Cancellation token</param>
    Task<Topic?> Get(Guid topicId, BoardAccessPolicy accessPolicy, CancellationToken ct = default);

    /// <summary>
    /// Get single topic by board ID and topic number
    /// </summary>
    /// <param name="boardId">Board identifier</param>
    /// <param name="topicNumber">Topic number within board</param>
    /// <param name="accessPolicy">Board access policy</param>
    /// <param name="ct">Cancellation token</param>
    Task<Topic?> GetByBoardAndNumber(Guid boardId, int topicNumber, BoardAccessPolicy accessPolicy, CancellationToken ct = default);

    /// <summary>
    /// Get the period markers of the given topics in one lookup. Topics
    /// without a marker are absent from the result.
    /// </summary>
    /// <param name="topicIds">Topic identifiers to look up</param>
    /// <param name="ct">Cancellation token</param>
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
    /// Get the user's most-liked topic across every board visible to the
    /// viewer. Soft-deleted topics are excluded, and the access-policy mask
    /// filters out topics on boards the viewer cannot see — so the profile
    /// widget never surfaces content behind a board the viewer lacks access
    /// to. Returns null when the user has no visible topics at all.
    /// </summary>
    /// <param name="authorId">Author user ID</param>
    /// <param name="accessPolicy">Board access policy of the viewer</param>
    /// <param name="ct">Cancellation token</param>
    Task<Topic?> GetBestUserTopic(Guid authorId, BoardAccessPolicy accessPolicy, CancellationToken ct = default);

    // --- WRITE ---

    /// <summary>
    /// Create new topic
    /// </summary>
    /// <param name="createTopic">Topic creation data</param>
    /// <param name="authorId">Author user ID</param>
    /// <param name="boardId">Board ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created topic DTO</returns>
    Task<Topic> Create(CreateTopicEntity createTopic, Guid authorId, Guid boardId, CancellationToken ct = default);

    /// <summary>
    /// Update existing topic
    /// </summary>
    /// <param name="updateTopic">Topic update data</param>
    /// <param name="boardId">New board ID (if changed)</param>
    /// <returns>Updated topic DTO, and whether the row actually changed</returns>
    Task<TopicUpdateResult> Update(UpdateTopicEntity updateTopic, Guid? boardId = null);

    /// <summary>
    /// Soft delete a topic
    /// </summary>
    /// <param name="topicId">Topic identifier</param>
    /// <param name="deletedByUserId">User who removed the topic</param>
    Task Delete(Guid topicId, Guid deletedByUserId);

    /// <summary>
    /// Ids of the board's pinned topics, in the order they are shown now
    /// </summary>
    /// <param name="boardId">Board identifier</param>
    /// <param name="ct">Cancellation token</param>
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
    /// <param name="boardId">Board identifier</param>
    /// <param name="orderedTopicIds">Pinned topic ids in the desired order</param>
    /// <param name="ct">Cancellation token</param>
    Task ReplaceAttachOrder(Guid boardId, IReadOnlyList<Guid> orderedTopicIds, CancellationToken ct = default);
}

/// <summary>
/// What an update did: the topic as it now stands, and whether it moved at all.
/// </summary>
/// <remarks>
/// A PATCH that round-trips the values the topic already holds is a legitimate
/// request and answers 200, but it is not an edit: it leaves no row in the edit
/// history, and it must announce nothing either. The two have to be decided by the
/// same fact, or the ChangedTopic notification goes out naming whoever edited the
/// topic last — a person who did nothing this time, and whose blacklist is the one
/// the delivery is then filtered against.
/// </remarks>
/// <param name="Topic">The topic after the update.</param>
/// <param name="Changed">True when the row was actually modified.</param>
public sealed record TopicUpdateResult(Topic Topic, bool Changed);

/// <summary>
/// Internal DTO for topic creation (repository layer)
/// </summary>
public class CreateTopicEntity
{
    /// <summary>
    /// Identifier of the topic to be created
    /// </summary>
    /// <remarks>
    /// Minted by the caller, because the unread markers of the topic are written
    /// before the row is — see UnreadCountersReservation for why that way round.
    /// </remarks>
    public required Guid TopicId { get; init; }

    /// <summary>
    /// Topic title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Topic text
    /// </summary>
    public string Text { get; set; } = null!;
}

/// <summary>
/// Internal DTO for topic update (repository layer)
/// </summary>
public class UpdateTopicEntity
{
    /// <summary>
    /// Topic identifier
    /// </summary>
    public Guid TopicId { get; set; }

    /// <summary>
    /// Updated title (optional)
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Updated text (optional)
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Updated closed status (optional)
    /// </summary>
    public bool? IsClosed { get; set; }

    /// <summary>
    /// Updated attached status (optional)
    /// </summary>
    public bool? IsAttached { get; set; }

    /// <summary>
    /// User performing the edit. Recorded in the topic edit history, which is
    /// the only place the schema keeps "who changed this" for a topic — the row
    /// itself carries the author and nothing else.
    /// </summary>
    public Guid EditorUserId { get; set; }
}
