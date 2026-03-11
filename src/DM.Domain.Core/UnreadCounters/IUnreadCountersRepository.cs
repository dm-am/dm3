using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.UnreadCounters;

/// <summary>
/// Unread counters storage
/// </summary>
public interface IUnreadCountersRepository
{
    /// <summary>
    /// Create a counter for the entity for a certain user
    /// </summary>
    /// <param name="entityId">Entity Id</param>
    /// <param name="entryType">Entry type</param>
    /// <param name="userIds">User Ids</param>
    /// <returns></returns>
    Task CreateAsync(Guid entityId, UnreadEntryType entryType, IEnumerable<Guid> userIds);

    /// <summary>
    /// Create a counter for the entity
    /// </summary>
    /// <param name="entityId">Entity Id</param>
    /// <param name="parentId">Parent entity Id</param>
    /// <param name="entryType">Entry type</param>
    /// <returns></returns>
    Task CreateAsync(Guid entityId, Guid parentId, UnreadEntryType entryType);

    /// <summary>
    /// Create a counter for the entity without parent
    /// </summary>
    /// <param name="entityId">Entity id</param>
    /// <param name="entryType">Entry type</param>
    /// <returns></returns>
    Task CreateAsync(Guid entityId, UnreadEntryType entryType);

    /// <summary>
    /// Increment counter of the entity for every user
    /// </summary>
    /// <param name="entityId">Entity Id</param>
    /// <param name="entryType">Entry type</param>
    /// <returns></returns>
    Task IncrementAsync(Guid entityId, UnreadEntryType entryType);

    /// <summary>
    /// Increment counter of the entity for every user except the specified one
    /// </summary>
    /// <param name="entityId">Entity Id</param>
    /// <param name="entryType">Entry type</param>
    /// <param name="excludeUserId">User Id to exclude from increment</param>
    /// <returns></returns>
    Task IncrementExcludingAsync(Guid entityId, UnreadEntryType entryType, Guid excludeUserId);

    /// <summary>
    /// Decrement counter for users who hasn't read the entry since given time
    /// </summary>
    /// <param name="entityId">Entity Id</param>
    /// <param name="entryType">Entry type</param>
    /// <param name="createDate">Given time</param>
    /// <returns></returns>
    Task DecrementAsync(Guid entityId, UnreadEntryType entryType, DateTimeOffset createDate);

    /// <summary>
    /// Remove counter for everyone
    /// </summary>
    /// <param name="entityId">Entity Id</param>
    /// <param name="entryType">Entry type</param>
    /// <returns></returns>
    Task DeleteAsync(Guid entityId, UnreadEntryType entryType);

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
    /// <returns></returns>
    Task FlushAsync(Guid userId, UnreadEntryType entryType, Guid entityId);

    /// <summary>
    /// Mark all according entities as read
    /// </summary>
    /// <param name="userId">User Id</param>
    /// <param name="entryType">Entry type</param>
    /// <param name="parentId">Parent entity Id</param>
    /// <returns></returns>
    Task FlushAllAsync(Guid userId, UnreadEntryType entryType, Guid parentId);

    /// <summary>
    /// Move unread counters to new parent
    /// </summary>
    /// <param name="parentId">Parent Id</param>
    /// <param name="entryType">Entry type</param>
    /// <param name="newParentId">New parent Id</param>
    /// <returns></returns>
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
