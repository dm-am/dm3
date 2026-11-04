using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Game.Features.GameAttributeSchemas;

/// <summary>
/// Service owning the replacement of a game's character sheet
/// </summary>
/// <remarks>
/// Reading is not here: the sheet travels with the details of its game.
/// </remarks>
public interface IGameAttributeSchemaService
{
    Task<GameAttributeSchema> ReplaceAsync(
        ReplaceGameAttributeSchema replace, CancellationToken ct = default);
}
