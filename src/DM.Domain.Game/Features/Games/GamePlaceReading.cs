using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;

namespace DM.Domain.Game.Features.Games;

/// <summary>
/// Whether the viewer reads a game: by the read of the game itself, the one its
/// discussion and its reviews are read through.
/// </summary>
internal class GamePlaceReading : IPlaceReading
{
    private readonly IGameService _games;

    public GamePlaceReading(IGameService games)
    {
        _games = games;
    }

    /// <inheritdoc />
    public ReadablePlaceKind Kind => ReadablePlaceKind.Game;

    /// <inheritdoc />
    public Task<IReadOnlySet<Guid>> ReadableAsync(IReadOnlyCollection<Guid> placeIds, CancellationToken ct = default) =>
        PlaceReading.ThroughOwnRead(placeIds, (gameId, token) => _games.GetAsync(gameId, token), ct);
}
