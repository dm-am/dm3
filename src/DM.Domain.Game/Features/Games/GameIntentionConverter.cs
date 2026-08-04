using DM.Domain.Game.Features.Games;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Authorization;

namespace DM.Domain.Game.Features.Games;

/// <inheritdoc />
internal class GameIntentionConverter : IGameIntentionConverter
{
    /// <inheritdoc />
    public (GameIntention intention, EventType eventType) Convert(ModuleStatus moduleStatus) => moduleStatus switch
    {
        ModuleStatus.Draft => (GameIntention.SetStatusDraft, EventType.StatusGameDraft),
        ModuleStatus.Active => (GameIntention.SetStatusActive, EventType.StatusGameActive),
        ModuleStatus.Closed => (GameIntention.SetStatusClosed, EventType.StatusGameClosed),
        _ => throw new GameIntentionConverterException(moduleStatus)
    };
}
