using System;
using System.Threading.Tasks;
using DM.Domain.Community.Features.WebsiteTestimonials;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Community.WebsiteTestimonials;

/// <summary>
/// API service for website testimonial operations
/// </summary>
public interface IWebsiteTestimonialApiService
{
    /// <summary>
    /// Get website testimonials
    /// </summary>
    /// <param name="query">Search, sorting and paging</param>
    Task<ListEnvelope<WebsiteTestimonialDto>> GetList(WebsiteTestimonialsQuery query);

    /// <summary>
    /// Get a single testimonial
    /// </summary>
    /// <param name="id">Testimonial identifier</param>
    Task<Envelope<WebsiteTestimonialDto>> Get(Guid id);

    /// <summary>
    /// Create a testimonial about the website
    /// </summary>
    /// <param name="request">Testimonial data</param>
    Task<Envelope<WebsiteTestimonialDto>> Create(CreateWebsiteTestimonialRequest request);

    /// <summary>
    /// Update an existing testimonial
    /// </summary>
    /// <param name="id">Testimonial identifier</param>
    /// <param name="request">Update data</param>
    Task<Envelope<WebsiteTestimonialDto>> Update(Guid id, UpdateWebsiteTestimonialRequest request);

    /// <summary>
    /// Delete an existing testimonial
    /// </summary>
    /// <param name="id">Testimonial identifier</param>
    Task Delete(Guid id);
}
