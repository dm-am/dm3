using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Game.AttributeSchemaTemplates;

/// <summary>
/// Starting character sheets the site offers when a game is created
/// </summary>
/// <remarks>
/// Reading is open: the catalogue is what the creation form fills itself from.
/// Writing is the senior moderation's, declared on each action here and asked
/// again by the service, so the catalogue has one answer whichever door a caller
/// comes through.
/// </remarks>
[ApiController]
[Route("v1/attribute-schema-templates")]
[ApiExplorerSettings(GroupName = "Game")]
[Tags("Attribute Schema Templates")]
public class AttributeSchemaTemplateController : ControllerBase
{
    private readonly IAttributeSchemaTemplateApiService _apiService;

    /// <summary>
    /// Creates a new instance of AttributeSchemaTemplateController
    /// </summary>
    public AttributeSchemaTemplateController(
        IAttributeSchemaTemplateApiService apiService)
    {
        _apiService = apiService;
    }

    /// <summary>
    /// Get the catalogue of attribute schema templates
    /// </summary>
    /// <remarks>
    /// The default template comes first, the rest by title. Every entry carries
    /// its specifications, so the creation form opens on one request.
    /// </remarks>
    /// <param name="ct">Cancellation token</param>
    /// <response code="200">Returns the template catalogue</response>
    [HttpGet(Name = nameof(GetAttributeSchemaTemplates))]
    [ProducesResponseType(typeof(ListEnvelope<AttributeSchemaTemplate>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAttributeSchemaTemplates(CancellationToken ct) =>
        Ok(await _apiService.Get(ct));

    /// <summary>
    /// Create new attribute schema template
    /// </summary>
    /// <param name="request">Template title and specifications</param>
    /// <response code="201">Resource created successfully</response>
    /// <response code="400">Some of the template parameters were invalid</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to manage attribute schema templates</response>
    /// <response code="409">A template with this title already exists</response>
    [HttpPost(Name = nameof(PostAttributeSchemaTemplate))]
    [RequireRole(UserRole.SeniorModerator)]
    [ProducesResponseType(typeof(Envelope<AttributeSchemaTemplate>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PostAttributeSchemaTemplate(
        [FromBody] AttributeSchemaTemplateRequest request)
    {
        var result = await _apiService.Create(request);
        return CreatedAtRoute(nameof(GetAttributeSchemaTemplate), new { id = result.Resource.Id }, result);
    }

    /// <summary>
    /// Get attribute schema template
    /// </summary>
    /// <param name="id">Template identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <response code="200">Returns the template</response>
    /// <response code="404">Template not found</response>
    [HttpGet("{id}", Name = nameof(GetAttributeSchemaTemplate))]
    [ProducesResponseType(typeof(Envelope<AttributeSchemaTemplate>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAttributeSchemaTemplate(Guid id, CancellationToken ct) =>
        Ok(await _apiService.Get(id, ct));

    /// <summary>
    /// Replace attribute schema template
    /// </summary>
    /// <param name="id">Template identifier</param>
    /// <param name="request">Template title and specifications</param>
    /// <response code="200">Returns the replaced template</response>
    /// <response code="400">Some of the template parameters were invalid</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to manage attribute schema templates</response>
    /// <response code="404">Template not found</response>
    /// <response code="409">A template with this title already exists</response>
    [HttpPut("{id}", Name = nameof(PutAttributeSchemaTemplate))]
    [RequireRole(UserRole.SeniorModerator)]
    [ProducesResponseType(typeof(Envelope<AttributeSchemaTemplate>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PutAttributeSchemaTemplate(
        Guid id, [FromBody] AttributeSchemaTemplateRequest request) =>
        Ok(await _apiService.Replace(id, request));

    /// <summary>
    /// Delete attribute schema template
    /// </summary>
    /// <param name="id">Template identifier</param>
    /// <response code="204">Operation completed successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to manage attribute schema templates</response>
    /// <response code="404">Template not found</response>
    /// <response code="409">Template is the default one and cannot be deleted</response>
    [HttpDelete("{id}", Name = nameof(DeleteAttributeSchemaTemplate))]
    [RequireRole(UserRole.SeniorModerator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteAttributeSchemaTemplate(Guid id)
    {
        await _apiService.Delete(id);
        return NoContent();
    }

    /// <summary>
    /// Make attribute schema template the default one
    /// </summary>
    /// <param name="id">Template identifier</param>
    /// <response code="204">Operation completed successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to manage attribute schema templates</response>
    /// <response code="404">Template not found</response>
    /// <response code="409">Another template has just been made the default one</response>
    [HttpPost("{id}/default", Name = nameof(PostAttributeSchemaTemplateDefault))]
    [RequireRole(UserRole.SeniorModerator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PostAttributeSchemaTemplateDefault(Guid id)
    {
        await _apiService.MakeDefault(id);
        return NoContent();
    }
}
