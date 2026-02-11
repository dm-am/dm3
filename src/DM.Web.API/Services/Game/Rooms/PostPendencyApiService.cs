using System;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Game.BusinessProcesses.PostPendencies.Creating;
using DM.Services.Game.BusinessProcesses.PostPendencies.Deleting;
using DM.Services.Game.Dto.Input;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Games;

namespace DM.Web.API.Services.Game.Rooms;

/// <inheritdoc />
internal class PostPendencyApiService : IPostPendencyApiService
{
    private readonly IPostPendencyCreatingService creatingService;
    private readonly IPostPendencyDeletingService deletingService;
    private readonly IMapper mapper;

    /// <inheritdoc />
    public PostPendencyApiService(
        IPostPendencyCreatingService creatingService,
        IPostPendencyDeletingService deletingService,
        IMapper mapper)
    {
        this.creatingService = creatingService;
        this.deletingService = deletingService;
        this.mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Envelope<PostPendency>> Create(Guid roomId, PostPendency postPendency)
    {
        var createPostPendency = mapper.Map<CreatePostPendency>(postPendency);
        createPostPendency.RoomId = roomId;
        var createdPostPendency = await creatingService.Create(createPostPendency);
        return new Envelope<PostPendency>(mapper.Map<PostPendency>(createdPostPendency));
    }

    /// <inheritdoc />
    public Task Delete(Guid postPendencyId) => deletingService.Delete(postPendencyId);
}