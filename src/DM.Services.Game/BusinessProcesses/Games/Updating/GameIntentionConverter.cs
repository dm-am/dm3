using DM.Services.Core.Dto.Enums;
using DM.Services.Game.Authorization;

namespace DM.Services.Game.BusinessProcesses.Games.Updating;

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