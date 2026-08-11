using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.UnreadCounters;

/// <summary>
/// Unread counters storage
/// </summary>
/// <remarks>
/// <para>
/// What ParentId holds, which is the one thing about this contract that cannot
/// be read off a signature. It is whatever answers "all of mine" for the entity
/// in question, and that is two different kinds of thing depending on which
/// overload created the marker.
/// </para>
/// <para>
/// The two-argument <see cref="CreateAsync(Guid,Guid,UnreadEntryType)" /> takes
/// it explicitly and gets a container: a topic is parented by its board, a room
/// by its game, a publication by its blog. Those markers are anonymous — one per
/// entity, shared by every reader who has not opened it yet — and a
/// parent-scoped read means "how much is unread in this board".
/// </para>
/// <para>
/// The three-argument <see cref="CreateAsync(Guid,UnreadEntryType,IEnumerable{Guid})" />
/// takes a list of readers instead and parents each marker by the reader
/// themselves. A conversation has no container, so without this "all my
/// conversations" would not be a parent-scoped read at all. The
/// <see cref="CreateAsync(Guid,UnreadEntryType)" /> overload is the degenerate
/// case: the entity is its own parent.
/// </para>
/// <para>
/// Both meanings live in one field on purpose — a parent-scoped aggregate is one
/// query either way — but nothing in the storage tells them apart, so a write
/// that stamps the wrong kind does not fail, it silently drops the entity out of
/// every total it belonged to. That is what makes the borrowing branch of
/// FlushAsync delicate, and why it is commented where it is.
/// </para>
/// </remarks>
public interface IUnreadCountersRepository
{
    /// <summary>
    /// Create a counter for the entity for a certain user
    /// </summary>
    /// <param name="entityId">Entity Id</param>
    /// <param name="entryType">Entry type</param>
    /// <param name="userIds">User Ids</param>
    Task CreateAsync(Guid entityId, UnreadEntryType entryType, IEnumerable<Guid> userIds);

    /// <summary>
    /// Create a counter for the entity
    /// </summary>
    /// <param name="entityId">Entity Id</param>
    /// <param name="parentId">Parent entity Id</param>
    /// <param name="entryType">Entry type</param>
    Task CreateAsync(Guid entityId, Guid parentId, UnreadEntryType entryType);

    /// <summary>
    /// Create a counter for the entity without parent
    /// </summary>
    /// <param name="entityId">Entity id</param>
    /// <param name="entryType">Entry type</param>
    Task CreateAsync(Guid entityId, UnreadEntryType entryType);

    /// <summary>
    /// Increment counter of the entity for every user
    /// </summary>
    /// <param name="entityId">Entity Id</param>
    /// <param name="entryType">Entry type</param>
    Task IncrementAsync(Guid entityId, UnreadEntryType entryType);

    /// <summary>
    /// Increment counter of the entity for every user except the specified one
    /// </summary>
    /// <param name="entityId">Entity Id</param>
    /// <param name="entryType">Entry type</param>
    /// <param name="excludeUserId">User Id to exclude from increment</param>
    Task IncrementExcludingAsync(Guid entityId, UnreadEntryType entryType, Guid excludeUserId);

    /// <summary>
    /// Decrement counter for users who hasn't read the entry since given time
    /// </summary>
    /// <param name="entityId">Entity Id</param>
    /// <param name="entryType">Entry type</param>
    /// <param name="createDate">Given time</param>
    Task DecrementAsync(Guid entityId, UnreadEntryType entryType, DateTimeOffset createDate);

    /// <summary>
    /// Remove counter for everyone
    /// </summary>
    /// <param name="entityId">Entity Id</param>
    /// <param name="entryType">Entry type</param>
    Task DeleteAsync(Guid entityId, UnreadEntryType entryType);

    /// <summary>
    /// Remove the counters of one entity for certain users
    /// </summary>
    /// <remarks>
    /// The mirror of the three-argument <see cref="CreateAsync(Guid,UnreadEntryType,IEnumerable{Guid})" />,
    /// and the half of the lifecycle that was missing. A person can be counted
    /// into an entity two ways and counted out of it none: removed from a group
    /// chat, their marker went on being incremented by every message in a
    /// conversation they can no longer open, and nothing collected it — the
    /// expiry index reads the removal stamp, which an untouched marker does not
    /// carry.
    /// </remarks>
    /// <param name="entityId">Entity Id</param>
    /// <param name="entryType">Entry type</param>
    /// <param name="userIds">Users whose counters go away</param>
    Task DeleteAsync(Guid entityId, UnreadEntryType entryType, IEnumerable<Guid> userIds);

    /// <summary>
    /// Get count of entities that have unread entries by parent entity ids
    /// </summary>
    /// <param name="userId">User Id</param>
    /// <param name="entryType">Entry type</param>
    /// <param name="parentIds">Parent entity Ids</param>
    /// <returns>List of pairs of parent entity Id and number of according entities that have unread entries</returns>
    Task<IDictionary<Guid, int>> SelectByParentsAsync(
        Guid userId, UnreadEntryType entryType, params Guid[] parentIds);

    /// <summary>
    /// Get total count of unread entries by parent entity ids (sum of all counters)
    /// </summary>
    /// <param name="userId">User Id</param>
    /// <param name="entryType">Entry type</param>
    /// <param name="parentIds">Parent entity Ids</param>
    /// <returns>List of pairs of parent entity Id and total number of unread entries</returns>
    Task<IDictionary<Guid, int>> SelectTotalUnreadByParentsAsync(
        Guid userId, UnreadEntryType entryType, params Guid[] parentIds);

    /// <summary>
    /// Get count of unread entries by entry ids
    /// </summary>
    /// <param name="userId">User Id</param>
    /// <param name="entryType">Entry type</param>
    /// <param name="entityIds">Entity Ids</param>
    /// <returns>List of pairs of entity Id and number of entries unread</returns>
    Task<IDictionary<Guid, int>> SelectByEntitiesAsync(
        Guid userId, UnreadEntryType entryType, params Guid[] entityIds);

    /// <summary>
    /// Mark entity as read
    /// </summary>
    /// <param name="userId">User Id</param>
    /// <param name="entryType">Entry type</param>
    /// <param name="entityId">Entity Id</param>
    Task FlushAsync(Guid userId, UnreadEntryType entryType, Guid entityId);

    /// <summary>
    /// Mark all according entities as read
    /// </summary>
    /// <param name="userId">User Id</param>
    /// <param name="entryType">Entry type</param>
    /// <param name="parentId">Parent entity Id</param>
    Task FlushAllAsync(Guid userId, UnreadEntryType entryType, Guid parentId);

    /// <summary>
    /// Move unread counters to new parent
    /// </summary>
    /// <param name="parentId">Parent Id</param>
    /// <param name="entryType">Entry type</param>
    /// <param name="newParentId">New parent Id</param>
    Task ChangeParentAsync(Guid parentId, UnreadEntryType entryType, Guid newParentId);

    /// <summary>
    /// Get the last read time for a specific entity
    /// </summary>
    /// <param name="userId">User Id</param>
    /// <param name="entityId">Entity Id</param>
    /// <param name="entryType">Entry type</param>
    /// <returns>Last read time or null if never read</returns>
    Task<DateTime?> GetLastReadTimeAsync(Guid userId, Guid entityId, UnreadEntryType entryType);

    /// <summary>
    /// Get the last read times for multiple entities
    /// </summary>
    /// <param name="userId">User Id</param>
    /// <param name="entryType">Entry type</param>
    /// <param name="entityIds">Entity Ids</param>
    /// <returns>Dictionary of entity Id to last read time</returns>
    Task<IDictionary<Guid, DateTime>> GetLastReadTimesAsync(Guid userId, UnreadEntryType entryType, params Guid[] entityIds);
}
