using System.Threading.Tasks;
using DM.Domain.Core.Enums;


namespace DM.Domain.Game.Features.Games;
/// <summary>
/// Facade for game creation validation and authorization
/// </summary>
public interface IGameCreationValidator
{
    /// <summary>
    /// Validates the game creation request and checks authorization
    /// </summary>
    Task ValidateAndAuthorize(CreateGame createGame);
    /// Determines the initial game state based on the request and user permissions
    /// <returns>Tuple of (initialStatus, premoderationStatus, isRecruitmentOpen)</returns>
    (ModuleStatus initialStatus, PremoderationStatus premoderationStatus, bool isRecruitmentOpen) GetInitialGameState(
        CreateGame createGame);
}
