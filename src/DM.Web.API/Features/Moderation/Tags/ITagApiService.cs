using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Moderation.Tags;

/// <summary>
/// API service for tag management
/// </summary>
public interface ITagApiService
{
    /// <summary>
    /// Get all tag groups
    /// </summary>
    Task<ListEnvelope<TagGroup>> GetGroups(CancellationToken ct = default);

    /// <summary>
    /// Get a tag group by ID
    /// </summary>
    Task<TagGroup> GetGroup(Guid groupId, CancellationToken ct = default);

    /// <summary>
    /// Create a tag group
    /// </summary>
    Task<TagGroup> CreateGroup(CreateTagGroupRequest request, CancellationToken ct = default);

    /// <summary>
    /// Update a tag group
    /// </summary>
    Task<TagGroup> UpdateGroup(Guid groupId, UpdateTagGroupRequest request, CancellationToken ct = default);

    /// <summary>
    /// Delete a tag group
    /// </summary>
    Task DeleteGroup(Guid groupId, CancellationToken ct = default);

    /// <summary>
    /// Get all tags
    /// </summary>
    Task<ListEnvelope<Tag>> GetTags(CancellationToken ct = default);

    /// <summary>
    /// Get tags by group ID
    /// </summary>
    Task<ListEnvelope<Tag>> GetTagsByGroup(Guid groupId, CancellationToken ct = default);

    /// <summary>
    /// Get a tag by ID
    /// </summary>
    Task<Tag> GetTag(Guid tagId, CancellationToken ct = default);

    /// <summary>
    /// Create a tag
    /// </summary>
    Task<Tag> CreateTag(CreateTagRequest request, CancellationToken ct = default);

    /// <summary>
    /// Update a tag
    /// </summary>
    Task<Tag> UpdateTag(Guid tagId, UpdateTagRequest request, CancellationToken ct = default);

    /// <summary>
    /// Delete a tag
    /// </summary>
    Task DeleteTag(Guid tagId, CancellationToken ct = default);
}
