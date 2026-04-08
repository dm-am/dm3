using System;

namespace DM.Domain.Community.Features.WebsiteTestimonials;

/// <summary>
/// DTO for creating a website testimonial entity in repository
/// </summary>
public class CreateWebsiteTestimonialEntity
{
    /// <summary>
    /// Testimonial identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Author identifier
    /// </summary>
    public Guid AuthorId { get; set; }

    /// <summary>
    /// Creation moment (UTC)
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Testimonial content (plain text, NO BBCode)
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
