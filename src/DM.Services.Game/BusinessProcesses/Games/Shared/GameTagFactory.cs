using System;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Games.Links;

namespace DM.Services.Game.BusinessProcesses.Games.Shared;

/// <inheritdoc />
internal class GameTagFactory : IGameTagFactory
{
    private readonly IGuidFactory _guidFactory;

    /// <inheritdoc />
    public GameTagFactory(
        IGuidFactory guidFactory)
    {
        _guidFactory = guidFactory;
    }

    /// <inheritdoc />
    public GameTag Create(Guid gameId, Guid tagId)
    {
        return new GameTag
        {
            GameTagId = _guidFactory.Create(),
            GameId = gameId,
            TagId = tagId
        };
    }
}