using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Moderation.Features.Tags;

/// <summary>
/// Service for tag management (moderator only)
/// </summary>
public interface ITagManagementService
{
    /// <summary>
    /// Get all tag groups ordered by SortOrder
    /// </summary>
    Task<IEnumerable<TagGroup>> GetGroups(CancellationToken ct = default);

    /// <summary>
    /// Get a tag group by ID
    /// </summary>
    Task<TagGroup> GetGroup(Guid groupId, CancellationToken ct = default);

    /// <summary>
    /// Create a tag group
    /// </summary>
    Task<TagGroup> CreateGroup(CreateTagGroup createGroup, CancellationToken ct = default);

    /// <summary>
    /// Update a tag group
    /// </summary>
    Task<TagGroup> UpdateGroup(UpdateTagGroup updateGroup, CancellationToken ct = default);

    /// <summary>
    /// Delete a tag group (only if empty)
    /// </summary>
    Task DeleteGroup(Guid groupId, CancellationToken ct = default);

    /// <summary>
    /// Get all tags ordered by group and SortOrder
    /// </summary>
    Task<IEnumerable<Tag>> GetTags(CancellationToken ct = default);

    /// <summary>
    /// Get tags by group ID
    /// </summary>
    Task<IEnumerable<Tag>> GetTagsByGroup(Guid groupId, CancellationToken ct = default);

    /// <summary>
    /// Get a tag by ID
    /// </summary>
    Task<Tag> GetTag(Guid tagId, CancellationToken ct = default);

    /// <summary>
    /// Create a tag
    /// </summary>
    Task<Tag> CreateTag(CreateTag createTag, CancellationToken ct = default);

    /// <summary>
    /// Update a tag
    /// </summary>
    Task<Tag> UpdateTag(UpdateTag updateTag, CancellationToken ct = default);

    /// <summary>
    /// Delete a tag (only if not used by any games)
    /// </summary>
    Task DeleteTag(Guid tagId, CancellationToken ct = default);
}
