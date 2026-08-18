using System.Threading.Tasks;

namespace DM.Domain.Game.Features.Games;

/// <summary>
/// Facade for game creation validation and authorization
/// </summary>
public interface IGameCreationValidator
{
    /// <summary>
    /// Validates the game creation request and checks authorization
    /// </summary>
    /// <remarks>
    /// The premoderation status a new game starts in is deliberately not decided
    /// here. It used to be — by a method on this interface that nothing ever
    /// called, so every game was created Approved while the rule sat unused. It
    /// is one rule over a game and a blog and lives in ModulePremoderationPolicy.
    /// </remarks>
    Task ValidateAndAuthorize(CreateGame createGame);
}
