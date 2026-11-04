using System;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Unread;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Game.Unread;

public class UnreadApiService
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

    public async Task<Envelope<FirstUnreadPostResult>> GetFirstUnreadPost(Guid gameId)
    {
        var result = await _firstUnreadService.GetFirstUnreadPost(gameId);
        return new Envelope<FirstUnreadPostResult>(_mapper.ToResponse(result));
    }

    /// <summary>Without a page size the page is counted at the reader's own setting.</summary>
    public async Task<Envelope<FirstUnreadRoomPostResult>> GetFirstUnreadPostInRoom(Guid roomId, int? pageSize)
    {
        var result = await _firstUnreadService.GetFirstUnreadPostInRoom(roomId, pageSize);
        return new Envelope<FirstUnreadRoomPostResult>(_mapper.ToResponse(result));
    }

    public async Task<Envelope<FirstUnreadCommentResult>> GetFirstUnreadComment(Guid gameId)
    {
        var result = await _firstUnreadService.GetFirstUnreadComment(gameId);
        return new Envelope<FirstUnreadCommentResult>(_mapper.ToResponse(result));
    }
}
