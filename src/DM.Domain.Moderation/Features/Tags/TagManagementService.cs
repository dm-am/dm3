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

        return await GetExistingGroup(groupId, ct);
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

        await GetExistingGroup(updateGroup.Id, ct);

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

        var group = await GetExistingGroup(groupId, ct);
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

        return await GetExistingTag(tagId, ct);
    }

    /// <inheritdoc />
    public async Task<Tag> CreateTag(CreateTag createTag, CancellationToken ct = default)
    {
        await _createTagValidator.ValidateAndThrowAsync(createTag, ct);
        _intentionManager.ThrowIfForbidden(ModerationIntention.ManageTags);

        var group = await GetExistingGroup(createTag.GroupId, ct);
        if (await _repository.TagTitleExists(createTag.Title, createTag.GroupId, ct: ct))
        {
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.TagTitleTaken(createTag.Title, group.Title));
        }

        return await _repository.CreateTag(new CreateTag
        {
            GroupId = createTag.GroupId,
            Title = createTag.Title,
            Description = createTag.Description,
            SortOrder = createTag.SortOrder
        }, ct);
    }

    /// <inheritdoc />
    public async Task<Tag> UpdateTag(UpdateTag updateTag, CancellationToken ct = default)
    {
        await _updateTagValidator.ValidateAndThrowAsync(updateTag, ct);
        _intentionManager.ThrowIfForbidden(ModerationIntention.ManageTags);

        await GetExistingTag(updateTag.Id, ct);
        var group = await GetExistingGroup(updateTag.GroupId, ct);
        if (await _repository.TagTitleExists(updateTag.Title, updateTag.GroupId, updateTag.Id, ct))
        {
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.TagTitleTaken(updateTag.Title, group.Title));
        }

        return await _repository.UpdateTag(new UpdateTag
        {
            Id = updateTag.Id,
            GroupId = updateTag.GroupId,
            Title = updateTag.Title,
            Description = updateTag.Description,
            SortOrder = updateTag.SortOrder
        }, ct);
    }

    /// <inheritdoc />
    public async Task DeleteTag(Guid tagId, CancellationToken ct = default)
    {
        _intentionManager.ThrowIfForbidden(ModerationIntention.ManageTags);

        var tag = await GetExistingTag(tagId, ct);
        if (tag.GamesCount > 0)
        {
            throw new HttpException(HttpStatusCode.Conflict, $"Нельзя удалить тег \"{tag.Title}\": он используется в играх ({tag.GamesCount})");
        }

        await _repository.DeleteTag(tagId, ct);
    }

    private async Task<TagGroup> GetExistingGroup(Guid groupId, CancellationToken ct) =>
        await _repository.GetGroup(groupId, ct)
        ?? throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.TagGroupNotFound(groupId));

    private async Task<Tag> GetExistingTag(Guid tagId, CancellationToken ct) =>
        await _repository.GetTag(tagId, ct)
        ?? throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.TagNotFound(tagId));
}
