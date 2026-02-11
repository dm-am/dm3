using System;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Games.Links;

namespace DM.Services.Game.BusinessProcesses.Readers.Subscribing;

/// <inheritdoc />
internal class ReaderFactory : IReaderFactory
{
    private readonly IGuidFactory _guidFactory;

    /// <inheritdoc />
    public ReaderFactory(
        IGuidFactory guidFactory)
    {
        _guidFactory = guidFactory;
    }
        
    /// <inheritdoc />
    public Reader Create(Guid userId, Guid gameId)
    {
        return new Reader
        {
            ReaderId = _guidFactory.Create(),
            GameId = gameId,
            UserId = userId
        };
    }
}