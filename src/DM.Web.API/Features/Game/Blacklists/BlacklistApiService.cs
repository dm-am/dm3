using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Blacklists;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Game.Blacklists;

/// <inheritdoc />
internal class BlacklistApiService : IBlacklistApiService
{
    private readonly IGameBlacklistService _blacklistService;
    private readonly UserMapper _mapper;

    /// <inheritdoc />
    public BlacklistApiService(
        IGameBlacklistService blacklistService,
        UserMapper mapper)
    {
        _blacklistService = blacklistService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<User>> Get(Guid gameId)
    {
        var users = await _blacklistService.Get(gameId);
        return new ListEnvelope<User>(users.Select(_mapper.ToUser));
    }

    /// <inheritdoc />
    public async Task<Envelope<User>> Create(Guid gameId, User user)
    {
        // The body names the user; the route names the game. Nothing else of
        // the User shape is read on this path. (The AutoMapper predecessor
        // called Map<OperateBlacklistLink> with no such map configured, so
        // this request could only throw at runtime.)
        var createBlacklistLink = new OperateBlacklistLink
        {
            GameId = gameId,
            Username = user.Username
        };
        var result = await _blacklistService.Add(createBlacklistLink);
        return new Envelope<User>(_mapper.ToUser(result));
    }

    /// <inheritdoc />
    public Task Delete(Guid gameId, string login) =>
        _blacklistService.Remove(new OperateBlacklistLink { GameId = gameId, Username = login });
}
