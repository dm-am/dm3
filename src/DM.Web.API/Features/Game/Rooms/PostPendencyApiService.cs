using System;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Game.Features.PostPendencies;
using DM.Web.API.Shared.Dto;
using CreatePostPendency = DM.Domain.Game.Features.PostPendencies.CreatePostPendency;

namespace DM.Web.API.Features.Game.Rooms;

/// <inheritdoc />
internal class PostPendencyApiService : IPostPendencyApiService
{
    private readonly IPostPendencyService _pendencyService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public PostPendencyApiService(
        IPostPendencyService pendencyService,
        IMapper mapper)
    {
        _pendencyService = pendencyService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Envelope<PostPendency>> Create(Guid roomId, PostPendency postPendency)
    {
        var createPostPendency = _mapper.Map<CreatePostPendency>(postPendency);
        createPostPendency.RoomId = roomId;
        var createdPostPendency = await _pendencyService.CreateAsync(createPostPendency);
        return new Envelope<PostPendency>(_mapper.Map<PostPendency>(createdPostPendency));
    }

    /// <inheritdoc />
    public Task Delete(Guid postPendencyId) => _pendencyService.DeleteAsync(postPendencyId);
}
