using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Game.AttributeSchemas;

/// <summary>
/// Attribute schema management endpoints
/// </summary>
/// <remarks>
/// Provides CRUD operations for character attribute schemas.
/// Schemas define the structure of character attributes (stats, skills) used in games.
/// </remarks>
[ApiController]
[Route("v1/schemas")]
[ApiExplorerSettings(GroupName = "Game")]
[Tags("Attribute Schemas")]
public class AttributeSchemaController : ControllerBase
{
    private readonly IAttributeSchemaApiService _schemaApiService;

    /// <summary>
    /// Creates a new instance of AttributeSchemaController
    /// </summary>
    public AttributeSchemaController(
        IAttributeSchemaApiService schemaApiService)
    {
        _schemaApiService = schemaApiService;
    }

    /// <summary>
    /// Get list of game attribute schemas (public and authored)
    /// </summary>
    /// <response code="200">Returns the attribute schema list</response>
    [HttpGet(Name = nameof(GetSchemas))]
    [ProducesResponseType(typeof(ListEnvelope<AttributeSchema>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSchemas() => Ok(await _schemaApiService.Get());

    /// <summary>
    /// Create new attribute schema
    /// </summary>
    /// <param name="schema">Attribute schema to create</param>
    /// <response code="201">Resource created successfully</response>
    /// <response code="400">Some of schema parameters were invalid</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to create attribute schemas</response>
    [HttpPost(Name = nameof(PostSchema))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<AttributeSchema>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> PostSchema([FromBody] AttributeSchema schema)
    {
        var result = await _schemaApiService.Create(schema);
        return CreatedAtRoute(nameof(GetSchema), new { id = result.Resource.Id }, result);
    }

    /// <summary>
    /// Get attribute schema
    /// </summary>
    /// <param name="id">Schema identifier</param>
    /// <response code="200">Returns the attribute schema details</response>
    /// <response code="404">Schema not found</response>
    [HttpGet("{id}", Name = nameof(GetSchema))]
    [ProducesResponseType(typeof(Envelope<AttributeSchema>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSchema(Guid id) => Ok(await _schemaApiService.Get(id));

    /// <summary>
    /// Update attribute schema
    /// </summary>
    /// <param name="id">Schema identifier</param>
    /// <param name="schema">Updated schema details</param>
    /// <response code="200">Returns the updated attribute schema</response>
    /// <response code="400">Some of schema parameters were invalid</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to update this attribute schema</response>
    /// <response code="404">Schema not found</response>
    [HttpPatch("{id}", Name = nameof(PatchSchema))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<AttributeSchema>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PatchSchema(Guid id, [FromBody] AttributeSchema schema) =>
        Ok(await _schemaApiService.Update(id, schema));

    /// <summary>
    /// Delete attribute schema
    /// </summary>
    /// <param name="id">Schema identifier</param>
    /// <response code="204">Operation completed successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to delete this attribute schema</response>
    /// <response code="404">Schema not found</response>
    [HttpDelete("{id}", Name = nameof(DeleteSchema))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSchema(Guid id)
    {
        await _schemaApiService.Delete(id);
        return NoContent();
    }
}
