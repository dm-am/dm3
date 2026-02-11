using System.Threading.Tasks;
using DM.Services.Core.Dto.Enums;
using DM.Services.Game.Dto.Input;

namespace DM.Services.Game.BusinessProcesses.Games.Creating.Facades;

/// <summary>
/// Facade for game creation validation and authorization
/// </summary>
public interface IGameCreationValidator
{
    /// <summary>
    /// Validates the game creation request and checks authorization
    /// </summary>
    Task ValidateAndAuthorize(CreateGame createGame);

    /// <summary>
    /// Determines the initial game state based on the request and user permissions
    /// </summary>
    /// <returns>Tuple of (initialStatus, premoderationStatus, isRecruitmentOpen)</returns>
    (ModuleStatus initialStatus, PremoderationStatus premoderationStatus, bool isRecruitmentOpen) GetInitialGameState(
        CreateGame createGame);
}
