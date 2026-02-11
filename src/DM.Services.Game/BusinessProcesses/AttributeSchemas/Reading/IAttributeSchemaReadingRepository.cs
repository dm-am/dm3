using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.Game.Dto.Shared;

namespace DM.Services.Game.BusinessProcesses.AttributeSchemas.Reading;

/// <summary>
/// Storage for attribute schemas
/// </summary>
internal interface IAttributeSchemaReadingRepository
{
    /// <summary>
    /// Get list of available schemas
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns></returns>
    Task<IEnumerable<AttributeSchema>> GetSchemata(Guid userId);

    /// <summary>
    /// Get certain attribute schema
    /// </summary>
    /// <param name="schemaId">Schema identifier</param>
    /// <returns></returns>
    Task<AttributeSchema?> GetSchema(Guid schemaId);
}