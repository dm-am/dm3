using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Exceptions;

namespace DM.Domain.Community.Features.WebsiteTestimonials;

public interface IWebsiteTestimonialRepository
{
    Task<int> Count(WebsiteTestimonialsQuery query);

    Task<IEnumerable<WebsiteTestimonial>> Get(WebsiteTestimonialsQuery query, PagingData pagingData);

    Task<WebsiteTestimonial?> Get(Guid id);

    Task<WebsiteTestimonial?> GetByAuthor(Guid authorId);

    /// <summary>
    /// Create new website testimonial
    /// </summary>
    /// <remarks>
    /// Throws <see cref="DuplicateEntityException" /> when the author already has
    /// a live testimonial: the caller checks first, and this is the race where two
    /// requests both pass that check. The partial unique index over the author is
    /// what actually holds "one testimonial apiece", and a second row would leave
    /// the author with two entries and the form still offering to write one.
    /// </remarks>
    Task<WebsiteTestimonial> Create(CreateWebsiteTestimonialEntity testimonial);

    Task<WebsiteTestimonial> Update(UpdateWebsiteTestimonialEntity testimonial);

    /// <summary>
    /// Mark website testimonial as removed
    /// </summary>
    Task Delete(Guid id, Guid deletedByUserId);
}
