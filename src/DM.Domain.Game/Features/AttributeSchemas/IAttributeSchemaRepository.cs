using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Games;

namespace DM.Domain.Game.Features.AttributeSchemas;

/// <summary>
/// Repository for attribute schema operations
/// </summary>
public interface IAttributeSchemaRepository
{
    #region Read

    /// <summary>
    /// Get all schemas available to user
    /// </summary>
    Task<IEnumerable<AttributeSchema>> GetSchemata(Guid userId);

    /// <summary>
    /// Get single schema
    /// </summary>
    Task<AttributeSchema?> GetSchema(Guid schemaId);

    /// <summary>
    /// Get the schema a game is built on, resolved through the game's own
    /// reference to it
    /// </summary>
    /// <remarks>
    /// Two things differ from <see cref="GetSchema"/>, and both on purpose. The
    /// removal flag is not filtered: it hides a schema from the lists a user
    /// picks one from, and it does not detach the schema from a game already
    /// built on it. The author is not read either — the character screens this
    /// feeds never show it, and reading it costs a second query per call.
    /// Throws when the game is unknown or its document is gone, because a game
    /// that requires attributes cannot be read without a schema.
    /// </remarks>
    Task<AttributeSchema> GetGameSchema(Guid gameId);

    /// <summary>
    /// Whether the user leads or participates in a game that references this schema
    /// </summary>
    Task<bool> IsUsedByUserGame(Guid schemaId, Guid userId);

    /// <summary>
    /// Whether any live game references this schema, whoever leads it
    /// </summary>
    Task<bool> IsUsedByAnyGame(Guid schemaId);

    #endregion

    #region Write

    /// <summary>
    /// Create schema
    /// </summary>
    Task<AttributeSchema> Create(CreateAttributeSchema createSchema, Guid authorId);

    /// <summary>
    /// Update schema
    /// </summary>
    Task<AttributeSchema> Update(UpdateAttributeSchema updateSchema);

    /// <summary>
    /// Delete schema
    /// </summary>
    Task Delete(Guid schemaId);

    #endregion
}
