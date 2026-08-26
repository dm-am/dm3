using System;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Unread;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Game.Unread;

/// <inheritdoc />
internal class UnreadApiService : IUnreadApiService
{
    private readonly IFirstUnreadService _firstUnreadService;
    private readonly UnreadMapper _mapper;

    public UnreadApiService(
        IFirstUnreadService firstUnreadService,
        UnreadMapper mapper)
    {
        _firstUnreadService = firstUnreadService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Envelope<FirstUnreadPostResult>> GetFirstUnreadPost(Guid gameId)
    {
        var result = await _firstUnreadService.GetFirstUnreadPost(gameId);
        return new Envelope<FirstUnreadPostResult>(_mapper.ToResponse(result));
    }

    /// <inheritdoc />
    public async Task<Envelope<FirstUnreadCommentResult>> GetFirstUnreadComment(Guid gameId)
    {
        var result = await _firstUnreadService.GetFirstUnreadComment(gameId);
        return new Envelope<FirstUnreadCommentResult>(_mapper.ToResponse(result));
    }
}
