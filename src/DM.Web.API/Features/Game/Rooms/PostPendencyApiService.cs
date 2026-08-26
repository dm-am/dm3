using System;
using System.Threading.Tasks;
using DM.Domain.Game.Features.PostPendencies;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Game.Rooms;

/// <inheritdoc />
internal class PostPendencyApiService : IPostPendencyApiService
{
    private readonly IPostPendencyService _pendencyService;
    private readonly RoomMapper _mapper;

    /// <inheritdoc />
    public PostPendencyApiService(
        IPostPendencyService pendencyService,
        RoomMapper mapper)
    {
        _pendencyService = pendencyService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Envelope<PostPendency>> Create(Guid roomId, PostPendency postPendency)
    {
        var createPostPendency = _mapper.ToCreatePostPendency(postPendency);
        createPostPendency.RoomId = roomId;
        var createdPostPendency = await _pendencyService.CreateAsync(createPostPendency);
        return new Envelope<PostPendency>(_mapper.ToPostPendency(createdPostPendency));
    }

    /// <inheritdoc />
    public Task Delete(Guid postPendencyId) => _pendencyService.DeleteAsync(postPendencyId);
}
