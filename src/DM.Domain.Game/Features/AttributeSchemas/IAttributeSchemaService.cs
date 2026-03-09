using DM.Domain.Game.Features.Games;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace DM.Domain.Game.Features.AttributeSchemas;

/// <summary>
/// Unified service for attribute schema CRUD operations
/// </summary>
public interface IAttributeSchemaService
{
    #region Create

    /// <summary>
    /// Create new attribute schema
    /// </summary>
    /// <param name="createSchema">DTO for creating</param>
    Task<AttributeSchema> CreateAsync(CreateAttributeSchema createSchema);

    #endregion

    #region Read

    /// <summary>
    /// Get list of available schemas
    /// </summary>
    Task<IEnumerable<AttributeSchema>> GetAllAsync();

    /// <summary>
    /// Get certain attribute schema
    /// </summary>
    /// <param name="schemaId">Schema identifier</param>
    Task<AttributeSchema> GetAsync(Guid schemaId);

    #endregion

    #region Update

    /// <summary>
    /// Update attribute schema
    /// </summary>
    /// <param name="updateSchema">DTO for updating</param>
    Task<AttributeSchema> UpdateAsync(UpdateAttributeSchema updateSchema);

    #endregion

    #region Delete

    /// <summary>
    /// Delete existing attribute schema
    /// </summary>
    /// <param name="schemaId">Attribute schema identifier</param>
    Task DeleteAsync(Guid schemaId);

    #endregion
}
