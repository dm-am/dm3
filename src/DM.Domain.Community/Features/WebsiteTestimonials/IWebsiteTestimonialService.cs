using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Community.Features.WebsiteTestimonials;

public interface IWebsiteTestimonialService
{
    Task<WebsiteTestimonial> CreateAsync(CreateWebsiteTestimonial createTestimonial);

    /// <returns>Testimonial or throws if not found</returns>
    Task<WebsiteTestimonial> GetAsync(Guid id);

    Task<(IEnumerable<WebsiteTestimonial> Testimonials, PagingResult Paging)> GetListAsync(WebsiteTestimonialsQuery query);

    /// <summary>
    /// Update website testimonial (text)
    /// </summary>
    Task<WebsiteTestimonial> UpdateAsync(UpdateWebsiteTestimonial updateTestimonial);

    Task DeleteAsync(Guid id);
}
