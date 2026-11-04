using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Blacklists;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Game.Blacklists;

public class BlacklistApiService
{
    private readonly IGameBlacklistService _blacklistService;
    private readonly UserMapper _mapper;

    public BlacklistApiService(
        IGameBlacklistService blacklistService,
        UserMapper mapper)
    {
        _blacklistService = blacklistService;
        _mapper = mapper;
    }

    public async Task<ListEnvelope<User>> Get(Guid gameId)
    {
        var users = await _blacklistService.Get(gameId);
        return new ListEnvelope<User>(users.Select(_mapper.ToUser));
    }

    public async Task<Envelope<User>> Create(Guid gameId, BlockUserRequest request)
    {
        var result = await _blacklistService.Add(new OperateBlacklistLink { GameId = gameId, Username = request.Username });
        return new Envelope<User>(_mapper.ToUser(result));
    }

    public Task Delete(Guid gameId, string username) =>
        _blacklistService.Remove(new OperateBlacklistLink { GameId = gameId, Username = username });
}
