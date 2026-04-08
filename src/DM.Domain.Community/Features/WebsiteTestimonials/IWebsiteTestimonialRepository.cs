using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Community.Features.WebsiteTestimonials;

/// <summary>
/// Storage for website testimonials
/// </summary>
public interface IWebsiteTestimonialRepository
{
    // READ

    /// <summary>
    /// Count website testimonials
    /// </summary>
    /// <param name="query">Query parameters for filtering</param>
    /// <returns>Number of testimonials</returns>
    Task<long> Count(WebsiteTestimonialsQuery query);

    /// <summary>
    /// Get list of website testimonials
    /// </summary>
    /// <param name="query">Query parameters for filtering and sorting</param>
    /// <param name="pagingData">Paging data</param>
    /// <returns>List of testimonials</returns>
    Task<IEnumerable<WebsiteTestimonial>> Get(WebsiteTestimonialsQuery query, PagingData pagingData);

    /// <summary>
    /// Get single website testimonial by id
    /// </summary>
    /// <param name="id">Testimonial identifier</param>
    /// <returns>Testimonial or null if not found</returns>
    Task<WebsiteTestimonial?> Get(Guid id);

    /// <summary>
    /// Get single website testimonial by author id
    /// </summary>
    /// <param name="authorId">Author identifier</param>
    /// <returns>Testimonial or null if not found</returns>
    Task<WebsiteTestimonial?> GetByAuthor(Guid authorId);

    // WRITE

    /// <summary>
    /// Create new website testimonial
    /// </summary>
    /// <param name="testimonial">Testimonial data</param>
    /// <returns>Created testimonial</returns>
    Task<WebsiteTestimonial> Create(CreateWebsiteTestimonialEntity testimonial);

    /// <summary>
    /// Update website testimonial
    /// </summary>
    /// <param name="testimonial">Update data</param>
    /// <returns>Updated testimonial</returns>
    Task<WebsiteTestimonial> Update(UpdateWebsiteTestimonialEntity testimonial);

    /// <summary>
    /// Mark website testimonial as removed
    /// </summary>
    /// <param name="id">Testimonial identifier</param>
    Task Delete(Guid id);
}
