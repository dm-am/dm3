using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Games;

namespace DM.Domain.Game.Features.GameAttributeSchemas;

/// <summary>
/// Repository for the character sheet a game is described by
/// </summary>
public interface IGameAttributeSchemaRepository
{
    /// <summary>
    /// The sheet of a game, with the version its next replacement is checked
    /// against
    /// </summary>
    /// <remarks>
    /// Throws when the game holds no sheet row. Every writer of a game writes
    /// one in the same save, so the absence is a defect of the write rather
    /// than a state a reader has to render, and answering with an empty sheet
    /// would hide a character's values behind a schema nobody chose.
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
    /// Throws on a game holding no sheet row, for the same reason the read does:
    /// null is the answer to a lost version and to nothing else, so a caller
    /// told to look again always has a row to look at.
    /// </remarks>
    Task<GameAttributeSchema?> Replace(
        ReplaceGameAttributeSchema replace, CancellationToken ct = default);
}
