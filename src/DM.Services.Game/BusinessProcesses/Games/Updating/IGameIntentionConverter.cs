using DM.Services.Core.Dto.Enums;
using DM.Services.Game.Authorization;

namespace DM.Services.Game.BusinessProcesses.Games.Updating;

/// <summary>
/// Converter for game status into associated intention
/// </summary>
internal interface IGameIntentionConverter
{
    /// <summary>
    /// Converts module status to associated intention
    /// </summary>
    /// <param name="moduleStatus">Module status</param>
    /// <returns>Authorized intention and invoked event type</returns>
    (GameIntention intention, EventType eventType) Convert(ModuleStatus moduleStatus);
}