using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Moderation.Features.Tags;

public interface ITagManagementRepository
{
    /// <summary>
    /// Get all tag groups ordered by SortOrder
    /// </summary>
    Task<IEnumerable<TagGroup>> GetGroups(CancellationToken ct = default);

    Task<TagGroup?> GetGroup(Guid groupId, CancellationToken ct = default);

    Task<bool> GroupTitleExists(string title, Guid? excludeId = null, CancellationToken ct = default);

    Task<TagGroup> CreateGroup(CreateTagGroup createGroup, CancellationToken ct = default);

    Task<TagGroup> UpdateGroup(UpdateTagGroup updateGroup, CancellationToken ct = default);

    /// <summary>
    /// Delete a tag group (only if empty)
    /// </summary>
    Task DeleteGroup(Guid groupId, CancellationToken ct = default);

    /// <summary>
    /// Get all tags ordered by group and SortOrder
    /// </summary>
    Task<IEnumerable<Tag>> GetTags(CancellationToken ct = default);

    Task<IEnumerable<Tag>> GetTagsByGroup(Guid groupId, CancellationToken ct = default);

    Task<Tag?> GetTag(Guid tagId, CancellationToken ct = default);

    /// <summary>
    /// Check if a tag with the given title exists in the group
    /// </summary>
    Task<bool> TagTitleExists(string title, Guid groupId, Guid? excludeId = null, CancellationToken ct = default);

    Task<Tag> CreateTag(CreateTag createTag, CancellationToken ct = default);

    Task<Tag> UpdateTag(UpdateTag updateTag, CancellationToken ct = default);

    /// <summary>
    /// Delete a tag (only if not used by any games)
    /// </summary>
    Task DeleteTag(Guid tagId, CancellationToken ct = default);
}
