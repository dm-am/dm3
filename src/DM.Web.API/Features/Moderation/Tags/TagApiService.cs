using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Moderation.Features.Tags;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Moderation.Tags;

/// <inheritdoc />
internal class TagApiService : ITagApiService
{
    private readonly ITagManagementService _tagService;
    private readonly IMapper _mapper;

    public TagApiService(ITagManagementService tagService, IMapper mapper)
    {
        _tagService = tagService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<TagGroup>> GetGroups(CancellationToken ct = default)
    {
        var groups = await _tagService.GetGroups(ct);
        return new ListEnvelope<TagGroup>(groups.Select(_mapper.Map<TagGroup>));
    }

    /// <inheritdoc />
    public async Task<TagGroup> GetGroup(Guid groupId, CancellationToken ct = default)
    {
        var group = await _tagService.GetGroup(groupId, ct);
        return _mapper.Map<TagGroup>(group);
    }

    /// <inheritdoc />
    public async Task<TagGroup> CreateGroup(CreateTagGroupRequest request, CancellationToken ct = default)
    {
        var createGroup = _mapper.Map<CreateTagGroup>(request);
        var group = await _tagService.CreateGroup(createGroup, ct);
        return _mapper.Map<TagGroup>(group);
    }

    /// <inheritdoc />
    public async Task<TagGroup> UpdateGroup(Guid groupId, UpdateTagGroupRequest request, CancellationToken ct = default)
    {
        var updateGroup = _mapper.Map<UpdateTagGroup>(request);
        updateGroup.Id = groupId;
        var group = await _tagService.UpdateGroup(updateGroup, ct);
        return _mapper.Map<TagGroup>(group);
    }

    /// <inheritdoc />
    public Task DeleteGroup(Guid groupId, CancellationToken ct = default)
    {
        return _tagService.DeleteGroup(groupId, ct);
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Tag>> GetTags(CancellationToken ct = default)
    {
        var tags = await _tagService.GetTags(ct);
        return new ListEnvelope<Tag>(tags.Select(_mapper.Map<Tag>));
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Tag>> GetTagsByGroup(Guid groupId, CancellationToken ct = default)
    {
        var tags = await _tagService.GetTagsByGroup(groupId, ct);
        return new ListEnvelope<Tag>(tags.Select(_mapper.Map<Tag>));
    }

    /// <inheritdoc />
    public async Task<Tag> GetTag(Guid tagId, CancellationToken ct = default)
    {
        var tag = await _tagService.GetTag(tagId, ct);
        return _mapper.Map<Tag>(tag);
    }

    /// <inheritdoc />
    public async Task<Tag> CreateTag(CreateTagRequest request, CancellationToken ct = default)
    {
        var createTag = _mapper.Map<CreateTag>(request);
        var tag = await _tagService.CreateTag(createTag, ct);
        return _mapper.Map<Tag>(tag);
    }

    /// <inheritdoc />
    public async Task<Tag> UpdateTag(Guid tagId, UpdateTagRequest request, CancellationToken ct = default)
    {
        var updateTag = _mapper.Map<UpdateTag>(request);
        updateTag.Id = tagId;
        var tag = await _tagService.UpdateTag(updateTag, ct);
        return _mapper.Map<Tag>(tag);
    }

    /// <inheritdoc />
    public Task DeleteTag(Guid tagId, CancellationToken ct = default)
    {
        return _tagService.DeleteTag(tagId, ct);
    }
}
