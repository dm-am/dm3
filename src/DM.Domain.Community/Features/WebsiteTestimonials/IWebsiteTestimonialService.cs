using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Community.Features.WebsiteTestimonials;

/// <summary>
/// Service for website testimonial operations
/// </summary>
public interface IWebsiteTestimonialService
{
    /// <summary>
    /// Create new website testimonial
    /// </summary>
    /// <param name="createTestimonial">Testimonial data</param>
    /// <returns>Created testimonial</returns>
    Task<WebsiteTestimonial> CreateAsync(CreateWebsiteTestimonial createTestimonial);

    /// <summary>
    /// Get single website testimonial by ID
    /// </summary>
    /// <param name="id">Testimonial ID</param>
    /// <returns>Testimonial or throws if not found</returns>
    Task<WebsiteTestimonial> GetAsync(Guid id);

    /// <summary>
    /// Get website testimonials with paging
    /// </summary>
    /// <param name="query">Paging query</param>
    /// <returns>Testimonials and paging info</returns>
    Task<(IEnumerable<WebsiteTestimonial> Testimonials, PagingResult Paging)> GetListAsync(WebsiteTestimonialsQuery query);

    /// <summary>
    /// Update website testimonial (text)
    /// </summary>
    /// <param name="updateTestimonial">Update data</param>
    /// <returns>Updated testimonial</returns>
    Task<WebsiteTestimonial> UpdateAsync(UpdateWebsiteTestimonial updateTestimonial);

    /// <summary>
    /// Delete website testimonial
    /// </summary>
    /// <param name="id">Testimonial ID</param>
    Task DeleteAsync(Guid id);
}
