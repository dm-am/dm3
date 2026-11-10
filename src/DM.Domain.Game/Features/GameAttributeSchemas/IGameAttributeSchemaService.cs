using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Game.Features.GameAttributeSchemas;

/// <remarks>
/// Reading is not here: the sheet travels with the details of its game.
/// </remarks>
public interface IGameAttributeSchemaService
{
    Task<GameAttributeSchema> ReplaceAsync(
        ReplaceGameAttributeSchema replace, CancellationToken ct = default);
}
