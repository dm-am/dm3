using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Exceptions;
using DM.Domain.Moderation.Authorization;
using FluentValidation;

namespace DM.Domain.Moderation.Features.Tags;

/// <inheritdoc />
internal class TagManagementService : ITagManagementService
{
    private readonly IIntentionManager _intentionManager;
    private readonly ITagManagementRepository _repository;
    private readonly IValidator<CreateTagGroup> _createGroupValidator;
    private readonly IValidator<UpdateTagGroup> _updateGroupValidator;
    private readonly IValidator<CreateTag> _createTagValidator;
    private readonly IValidator<UpdateTag> _updateTagValidator;

    public TagManagementService(
        IIntentionManager intentionManager,
        ITagManagementRepository repository,
        IValidator<CreateTagGroup> createGroupValidator,
        IValidator<UpdateTagGroup> updateGroupValidator,
        IValidator<CreateTag> createTagValidator,
        IValidator<UpdateTag> updateTagValidator)
    {
        _intentionManager = intentionManager;
        _repository = repository;
        _createGroupValidator = createGroupValidator;
        _updateGroupValidator = updateGroupValidator;
        _createTagValidator = createTagValidator;
        _updateTagValidator = updateTagValidator;
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
            ?? throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.TagGroupNotFound(groupId));

        return group;
    }

    /// <inheritdoc />
    public async Task<TagGroup> CreateGroup(CreateTagGroup createGroup, CancellationToken ct = default)
    {
        await _createGroupValidator.ValidateAndThrowAsync(createGroup, ct);
        _intentionManager.ThrowIfForbidden(ModerationIntention.ManageTags);

        if (await _repository.GroupTitleExists(createGroup.Title, ct: ct))
        {
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.TagGroupTitleTaken(createGroup.Title));
        }

        return await _repository.CreateGroup(createGroup, ct);
    }

    /// <inheritdoc />
    public async Task<TagGroup> UpdateGroup(UpdateTagGroup updateGroup, CancellationToken ct = default)
    {
        await _updateGroupValidator.ValidateAndThrowAsync(updateGroup, ct);
        _intentionManager.ThrowIfForbidden(ModerationIntention.ManageTags);

        var group = await _repository.GetGroup(updateGroup.Id, ct)
            ?? throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.TagGroupNotFound(updateGroup.Id));

        if (await _repository.GroupTitleExists(updateGroup.Title, updateGroup.Id, ct))
        {
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.TagGroupTitleTaken(updateGroup.Title));
        }

        return await _repository.UpdateGroup(updateGroup, ct);
    }

    /// <inheritdoc />
    public async Task DeleteGroup(Guid groupId, CancellationToken ct = default)
    {
        _intentionManager.ThrowIfForbidden(ModerationIntention.ManageTags);

        var group = await _repository.GetGroup(groupId, ct)
            ?? throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.TagGroupNotFound(groupId));

        if (group.TagsCount > 0)
        {
            throw new HttpException(HttpStatusCode.Conflict, "Нельзя удалить группу, в которой есть теги");
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
            ?? throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.TagNotFound(tagId));

        return tag;
    }

    /// <inheritdoc />
    public async Task<Tag> CreateTag(CreateTag createTag, CancellationToken ct = default)
    {
        await _createTagValidator.ValidateAndThrowAsync(createTag, ct);
        _intentionManager.ThrowIfForbidden(ModerationIntention.ManageTags);

        var group = await _repository.GetGroup(createTag.GroupId, ct)
            ?? throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.TagGroupNotFound(createTag.GroupId));

        if (await _repository.TagTitleExists(createTag.Title, createTag.GroupId, ct: ct))
        {
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.TagTitleTaken(createTag.Title, group.Title));
        }

        return await _repository.CreateTag(createTag, ct);
    }

    /// <inheritdoc />
    public async Task<Tag> UpdateTag(UpdateTag updateTag, CancellationToken ct = default)
    {
        await _updateTagValidator.ValidateAndThrowAsync(updateTag, ct);
        _intentionManager.ThrowIfForbidden(ModerationIntention.ManageTags);

        var tag = await _repository.GetTag(updateTag.Id, ct)
            ?? throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.TagNotFound(updateTag.Id));

        var group = await _repository.GetGroup(updateTag.GroupId, ct)
            ?? throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.TagGroupNotFound(updateTag.GroupId));

        if (await _repository.TagTitleExists(updateTag.Title, updateTag.GroupId, updateTag.Id, ct))
        {
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.TagTitleTaken(updateTag.Title, group.Title));
        }

        return await _repository.UpdateTag(updateTag, ct);
    }

    /// <inheritdoc />
    public async Task DeleteTag(Guid tagId, CancellationToken ct = default)
    {
        _intentionManager.ThrowIfForbidden(ModerationIntention.ManageTags);

        var tag = await _repository.GetTag(tagId, ct)
            ?? throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.TagNotFound(tagId));

        var usageCount = await _repository.GetTagUsageCount(tagId, ct);
        if (usageCount > 0)
        {
            throw new HttpException(HttpStatusCode.Conflict, $"Нельзя удалить тег \"{tag.Title}\": он используется в играх ({usageCount})");
        }

        await _repository.DeleteTag(tagId, ct);
    }
}
