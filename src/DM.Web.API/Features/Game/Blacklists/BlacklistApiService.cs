using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Game.Features.Blacklists;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Game.Blacklists;

/// <inheritdoc />
internal class BlacklistApiService : IBlacklistApiService
{
    private readonly IGameBlacklistService _blacklistService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public BlacklistApiService(
        IGameBlacklistService blacklistService,
        IMapper mapper)
    {
        _blacklistService = blacklistService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<User>> Get(Guid gameId)
    {
        var users = await _blacklistService.Get(gameId);
        return new ListEnvelope<User>(users.Select(_mapper.Map<User>));
    }

    /// <inheritdoc />
    public async Task<Envelope<User>> Create(Guid gameId, User user)
    {
        var createBlacklistLink = _mapper.Map<OperateBlacklistLink>(user);
        createBlacklistLink.GameId = gameId;
        var result = await _blacklistService.Add(createBlacklistLink);
        return new Envelope<User>(_mapper.Map<User>(result));
    }

    /// <inheritdoc />
    public Task Delete(Guid gameId, string login) =>
        _blacklistService.Remove(new OperateBlacklistLink { GameId = gameId, Username = login });
}
