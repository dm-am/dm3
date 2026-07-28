using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Game.AttributeSchemas;

/// <summary>
/// API service for attribute schemas
/// </summary>
public interface IAttributeSchemaApiService
{
    /// <summary>
    /// Get all available attribute schemas
    /// </summary>
    /// <returns>List envelope containing all attribute schemas</returns>
    Task<ListEnvelope<AttributeSchema>> Get();

    /// <summary>
    /// Get single attribute schema
    /// </summary>
    /// <param name="schemaId">Schema identifier</param>
    /// <returns>Envelope containing the attribute schema</returns>
    Task<Envelope<AttributeSchema>> Get(Guid schemaId);

    /// <summary>
    /// Create new attribute schema
    /// </summary>
    /// <param name="schema">Schema DTO</param>
    /// <returns>Envelope containing the created attribute schema</returns>
    Task<Envelope<AttributeSchema>> Create(AttributeSchema schema);

    /// <summary>
    /// Update existing attribute schema
    /// </summary>
    /// <param name="schemaId">Schema identifier</param>
    /// <param name="request">Fields to change</param>
    /// <returns>Envelope containing the updated attribute schema</returns>
    Task<Envelope<AttributeSchema>> Update(Guid schemaId, UpdateAttributeSchemaRequest request);

    /// <summary>
    /// Delete existing attribute schema
    /// </summary>
    /// <param name="schemaId"></param>
    Task Delete(Guid schemaId);
}
