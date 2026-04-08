using DM.Domain.Core.Dto;

namespace DM.Domain.Community.Features.WebsiteTestimonials;

/// <summary>
/// Query parameters for website testimonials list
/// </summary>
public class WebsiteTestimonialsQuery : PagingQuery
{
    /// <summary>
    /// Text search in testimonial content or author username (case-insensitive contains)
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Sort field: "created" (default) or "author"
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// Sort order: "asc" or "desc" (default: desc)
    /// </summary>
    public string? SortOrder { get; set; }
}
