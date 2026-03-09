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
