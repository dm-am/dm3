using System;
using System.Threading.Tasks;
using DM.Domain.Game.Features.PostPendencies;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Game.Rooms;

public class PostPendencyApiService
{
    private readonly IPostPendencyService _pendencyService;
    private readonly RoomMapper _mapper;

    public PostPendencyApiService(
        IPostPendencyService pendencyService,
        RoomMapper mapper)
    {
        _pendencyService = pendencyService;
        _mapper = mapper;
    }

    public async Task<Envelope<PostPendency>> Create(Guid roomId, CreatePostPendencyRequest request)
    {
        var createPostPendency = RoomMapper.ToCreatePostPendency(roomId, request);
        var createdPostPendency = await _pendencyService.CreateAsync(createPostPendency);
        return new Envelope<PostPendency>(_mapper.ToPostPendency(createdPostPendency));
    }

    public Task Delete(Guid postPendencyId) => _pendencyService.DeleteAsync(postPendencyId);
}
