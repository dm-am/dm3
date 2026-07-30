using System;
using System.Threading.Tasks;
using DM.Domain.Community.Features.WebsiteTestimonials;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Community.WebsiteTestimonials;

/// <summary>
/// Website testimonials controller - list, create, update, delete testimonials
/// </summary>
[ApiController]
[Route("v1/testimonials")]
[ApiExplorerSettings(GroupName = "Community")]
[Tags("Website Testimonials")]
public class WebsiteTestimonialController : ControllerBase
{
    private readonly IWebsiteTestimonialApiService _testimonialApiService;

    /// <inheritdoc />
    public WebsiteTestimonialController(IWebsiteTestimonialApiService testimonialApiService)
    {
        _testimonialApiService = testimonialApiService;
    }

    /// <summary>
    /// Get all website testimonials
    /// </summary>
    /// <remarks>
    /// Returns positive testimonials written about the website.
    /// Users can leave one testimonial about the website.
    /// Plain text only - NO BBCode support.
    /// NO likes support.
    ///
    /// ## Query Parameters
    /// - **skip**: Number of items to skip (pagination)
    /// - **take**: Number of items to return (max 100, default 20)
    /// - **search**: Text search in testimonial content or author username (case-insensitive)
    /// - **sortBy**: Sort field - "created" (default) or "author"
    /// - **sortOrder**: Sort direction - "asc" or "desc" (default: desc for created, asc for author)
    /// </remarks>
    /// <param name="q">Query parameters with search, sorting and pagination</param>
    /// <response code="200">List of website testimonials</response>
    [HttpGet(Name = nameof(GetWebsiteTestimonials))]
    [ProducesResponseType(typeof(ListEnvelope<WebsiteTestimonialDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWebsiteTestimonials([FromQuery] WebsiteTestimonialsQuery q) =>
        Ok(await _testimonialApiService.GetList(q));

    /// <summary>
    /// Get single testimonial by ID
    /// </summary>
    /// <param name="id">Testimonial identifier</param>
    /// <response code="200">Testimonial details</response>
    /// <response code="404">Testimonial not found</response>
    [HttpGet("{id:guid}", Name = nameof(GetWebsiteTestimonial))]
    [ProducesResponseType(typeof(Envelope<WebsiteTestimonialDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWebsiteTestimonial(Guid id) =>
        Ok(await _testimonialApiService.Get(id));

    /// <summary>
    /// Create website testimonial
    /// </summary>
    /// <remarks>
    /// Creates a positive testimonial about the website.
    /// Only one testimonial per user is allowed.
    /// Plain text only - NO BBCode support.
    /// NO likes support.
    /// </remarks>
    /// <param name="request">Testimonial data</param>
    /// <response code="201">Testimonial created successfully</response>
    /// <response code="400">Invalid testimonial data</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="409">Testimonial already exists</response>
    [HttpPost(Name = nameof(CreateWebsiteTestimonial))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<WebsiteTestimonialDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateWebsiteTestimonial([FromBody] CreateWebsiteTestimonialRequest request)
    {
        var result = await _testimonialApiService.Create(request);
        return CreatedAtRoute(nameof(GetWebsiteTestimonial), new { id = result.Resource.Id }, result);
    }

    /// <summary>
    /// Update website testimonial
    /// </summary>
    /// <remarks>
    /// Updates an existing testimonial.
    /// Can be updated by the author or a moderator.
    /// </remarks>
    /// <param name="id">Testimonial identifier</param>
    /// <param name="request">Update data</param>
    /// <response code="200">Testimonial updated successfully</response>
    /// <response code="400">Invalid update data</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">Not allowed (not author or moderator)</response>
    /// <response code="404">Testimonial not found</response>
    [HttpPatch("{id:guid}", Name = nameof(UpdateWebsiteTestimonial))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<WebsiteTestimonialDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateWebsiteTestimonial(
        Guid id, [FromBody] UpdateWebsiteTestimonialRequest request) =>
        Ok(await _testimonialApiService.Update(id, request));

    /// <summary>
    /// Delete website testimonial
    /// </summary>
    /// <remarks>
    /// Deletes an existing testimonial.
    /// Can be deleted by the author or a moderator.
    /// </remarks>
    /// <param name="id">Testimonial identifier</param>
    /// <response code="204">Testimonial deleted successfully</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">Not allowed (not author or moderator)</response>
    /// <response code="404">Testimonial not found</response>
    [HttpDelete("{id:guid}", Name = nameof(DeleteWebsiteTestimonial))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteWebsiteTestimonial(Guid id)
    {
        await _testimonialApiService.Delete(id);
        return NoContent();
    }
}
