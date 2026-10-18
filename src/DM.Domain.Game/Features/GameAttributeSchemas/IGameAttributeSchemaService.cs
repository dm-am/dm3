using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Games;

namespace DM.Domain.Game.Features.GameAttributeSchemas;

/// <summary>
/// Service owning the replacement of a game's character sheet
/// </summary>
/// <remarks>
/// Reading is not here: the sheet travels with the details of its game, and a
/// second read path would be a second answer to the same question.
/// </remarks>
public interface IGameAttributeSchemaService
{
    /// <summary>
    /// Replace the sheet of a game
    /// </summary>
    /// <param name="replace">The new set and the version it was read at</param>
    /// <param name="ct">Cancellation token</param>
    Task<GameAttributeSchema> ReplaceAsync(
        ReplaceGameAttributeSchema replace, CancellationToken ct = default);
}
