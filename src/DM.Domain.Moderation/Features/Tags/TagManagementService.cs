using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Exceptions;
using DM.Domain.Moderation.Authorization;

namespace DM.Domain.Moderation.Features.Tags;

/// <inheritdoc />
internal class TagManagementService : ITagManagementService
{
    private readonly IIntentionManager _intentionManager;
    private readonly ITagManagementRepository _repository;

    public TagManagementService(
        IIntentionManager intentionManager,
        ITagManagementRepository repository)
    {
        _intentionManager = intentionManager;
        _repository = repository;
    }

    /// <inheritdoc />
    public Task<IEnumerable<TagGroup>> GetGroups(CancellationToken ct = default)
    {
        _intentionManager.ThrowIfForbidden(ModerationIntention.ManageTags);
        return _repository.GetGroups(ct);
    }

    /// <inheritdoc />
    public async Task<TagGroup> GetGroup(Guid groupId, CancellationToken ct = default)
    {
        _intentionManager.ThrowIfForbidden(ModerationIntention.ManageTags);

        var group = await _repository.GetGroup(groupId, ct)
            ?? throw new HttpException(HttpStatusCode.NotFound, $"Tag group {groupId} not found");

        return group;
    }

    /// <inheritdoc />
    public async Task<TagGroup> CreateGroup(CreateTagGroup createGroup, CancellationToken ct = default)
    {
        _intentionManager.ThrowIfForbidden(ModerationIntention.ManageTags);

        if (await _repository.GroupTitleExists(createGroup.Title, ct: ct))
        {
            throw new HttpException(HttpStatusCode.Conflict, $"Tag group '{createGroup.Title}' already exists");
        }

        return await _repository.CreateGroup(createGroup, ct);
    }

    /// <inheritdoc />
    public async Task<TagGroup> UpdateGroup(UpdateTagGroup updateGroup, CancellationToken ct = default)
    {
        _intentionManager.ThrowIfForbidden(ModerationIntention.ManageTags);

        var group = await _repository.GetGroup(updateGroup.Id, ct)
            ?? throw new HttpException(HttpStatusCode.NotFound, $"Tag group {updateGroup.Id} not found");

        if (await _repository.GroupTitleExists(updateGroup.Title, updateGroup.Id, ct))
        {
            throw new HttpException(HttpStatusCode.Conflict, $"Tag group '{updateGroup.Title}' already exists");
        }

        return await _repository.UpdateGroup(updateGroup, ct);
    }

    /// <inheritdoc />
    public async Task DeleteGroup(Guid groupId, CancellationToken ct = default)
    {
        _intentionManager.ThrowIfForbidden(ModerationIntention.ManageTags);

        var group = await _repository.GetGroup(groupId, ct)
            ?? throw new HttpException(HttpStatusCode.NotFound, $"Tag group {groupId} not found");

        if (group.TagsCount > 0)
        {
            throw new HttpException(HttpStatusCode.Conflict, "Cannot delete a group that contains tags");
        }

        await _repository.DeleteGroup(groupId, ct);
    }

    /// <inheritdoc />
    public Task<IEnumerable<Tag>> GetTags(CancellationToken ct = default)
    {
        _intentionManager.ThrowIfForbidden(ModerationIntention.ManageTags);
        return _repository.GetTags(ct);
    }

    /// <inheritdoc />
    public Task<IEnumerable<Tag>> GetTagsByGroup(Guid groupId, CancellationToken ct = default)
    {
        _intentionManager.ThrowIfForbidden(ModerationIntention.ManageTags);
        return _repository.GetTagsByGroup(groupId, ct);
    }

    /// <inheritdoc />
    public async Task<Tag> GetTag(Guid tagId, CancellationToken ct = default)
    {
        _intentionManager.ThrowIfForbidden(ModerationIntention.ManageTags);

        var tag = await _repository.GetTag(tagId, ct)
            ?? throw new HttpException(HttpStatusCode.NotFound, $"Tag {tagId} not found");

        return tag;
    }

    /// <inheritdoc />
    public async Task<Tag> CreateTag(CreateTag createTag, CancellationToken ct = default)
    {
        _intentionManager.ThrowIfForbidden(ModerationIntention.ManageTags);

        var group = await _repository.GetGroup(createTag.GroupId, ct)
            ?? throw new HttpException(HttpStatusCode.NotFound, $"Tag group {createTag.GroupId} not found");

        if (await _repository.TagTitleExists(createTag.Title, createTag.GroupId, ct: ct))
        {
            throw new HttpException(HttpStatusCode.Conflict, $"Tag '{createTag.Title}' already exists in group '{group.Title}'");
        }

        return await _repository.CreateTag(createTag, ct);
    }

    /// <inheritdoc />
    public async Task<Tag> UpdateTag(UpdateTag updateTag, CancellationToken ct = default)
    {
        _intentionManager.ThrowIfForbidden(ModerationIntention.ManageTags);

        var tag = await _repository.GetTag(updateTag.Id, ct)
            ?? throw new HttpException(HttpStatusCode.NotFound, $"Tag {updateTag.Id} not found");

        var group = await _repository.GetGroup(updateTag.GroupId, ct)
            ?? throw new HttpException(HttpStatusCode.NotFound, $"Tag group {updateTag.GroupId} not found");

        if (await _repository.TagTitleExists(updateTag.Title, updateTag.GroupId, updateTag.Id, ct))
        {
            throw new HttpException(HttpStatusCode.Conflict, $"Tag '{updateTag.Title}' already exists in group '{group.Title}'");
        }

        return await _repository.UpdateTag(updateTag, ct);
    }

    /// <inheritdoc />
    public async Task DeleteTag(Guid tagId, CancellationToken ct = default)
    {
        _intentionManager.ThrowIfForbidden(ModerationIntention.ManageTags);

        var tag = await _repository.GetTag(tagId, ct)
            ?? throw new HttpException(HttpStatusCode.NotFound, $"Tag {tagId} not found");

        var usageCount = await _repository.GetTagUsageCount(tagId, ct);
        if (usageCount > 0)
        {
            throw new HttpException(HttpStatusCode.Conflict, $"Cannot delete tag '{tag.Title}' - it is used by {usageCount} game(s)");
        }

        await _repository.DeleteTag(tagId, ct);
    }
}
