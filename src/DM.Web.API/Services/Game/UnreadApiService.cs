using System;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Game.BusinessProcesses.Unread;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Games;

namespace DM.Web.API.Services.Game;

/// <inheritdoc />
internal class UnreadApiService : IUnreadApiService
{
    private readonly IFirstUnreadService _firstUnreadService;
    private readonly IMapper _mapper;

    public UnreadApiService(
        IFirstUnreadService firstUnreadService,
        IMapper mapper)
    {
        _firstUnreadService = firstUnreadService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Envelope<FirstUnreadPostResult>> GetFirstUnreadPost(Guid gameId)
    {
        var result = await _firstUnreadService.GetFirstUnreadPost(gameId);
        return new Envelope<FirstUnreadPostResult>(_mapper.Map<FirstUnreadPostResult>(result));
    }

    /// <inheritdoc />
    public async Task<Envelope<FirstUnreadCommentResult>> GetFirstUnreadComment(Guid gameId)
    {
        var result = await _firstUnreadService.GetFirstUnreadComment(gameId);
        return new Envelope<FirstUnreadCommentResult>(_mapper.Map<FirstUnreadCommentResult>(result));
    }
}
