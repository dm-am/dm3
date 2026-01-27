using DM.Services.Core.Dto.Enums;
using DM.Services.Gaming.Authorization;

namespace DM.Services.Gaming.BusinessProcesses.Games.Updating;

/// <inheritdoc />
internal class GameIntentionConverter : IGameIntentionConverter
{
    /// <inheritdoc />
    public (GameIntention intention, EventType eventType) Convert(GameStatus gameStatus) => gameStatus switch
    {
        GameStatus.Draft => (GameIntention.SetStatusDraft, EventType.StatusGameDraft),
        GameStatus.Active => (GameIntention.SetStatusActive, EventType.StatusGameActive),
        GameStatus.Closed => (GameIntention.SetStatusClosed, EventType.StatusGameClosed),
        _ => throw new GameIntentionConverterException(gameStatus)
    };
}