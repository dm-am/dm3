using System;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Games.Links;

namespace DM.Services.Game.BusinessProcesses.Blacklist.Creating;

/// <inheritdoc />
internal class GameBlacklistFactory : IGameBlacklistFactory
{
    private readonly IGuidFactory _guidFactory;

    /// <inheritdoc />
    public GameBlacklistFactory(
        IGuidFactory guidFactory)
    {
        _guidFactory = guidFactory;
    }

    /// <inheritdoc />
    public GameBlacklist Create(Guid gameId, Guid userId)
    {
        return new GameBlacklist
        {
            EntryId = _guidFactory.Create(),
            GameId = gameId,
            UserId = userId
        };
    }
}