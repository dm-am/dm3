using System;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Game.Features.GameAttributeSchemas;

public interface IGameAttributeSchemaRepository
{
    /// <summary>
    /// The sheet of a game, with the version its next replacement is checked
    /// against
    /// </summary>
    /// <remarks>
    /// Throws when the game holds no sheet row: every writer of a game writes one, so absence is a defect.
    /// </remarks>
    Task<GameAttributeSchema> Get(Guid gameId, CancellationToken ct = default);

    /// <summary>
    /// Replace the whole document, and drop the values of every character of
    /// the game whose specification the new set no longer holds
    /// </summary>
    /// <returns>
    /// The stored sheet with its new version, or null when the stored version
    /// was not the one the caller read - in which case nothing was written.
    /// </returns>
    /// <remarks>
    /// Throws on a game holding no sheet row, so null means a lost version and nothing else.
    /// </remarks>
    Task<GameAttributeSchema?> Replace(
        ReplaceGameAttributeSchema replace, CancellationToken ct = default);
}
