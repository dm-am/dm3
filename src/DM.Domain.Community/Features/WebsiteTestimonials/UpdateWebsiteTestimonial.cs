using System;

namespace DM.Domain.Community.Features.WebsiteTestimonials;

/// <summary>
/// DTO model for website testimonial update
/// </summary>
public class UpdateWebsiteTestimonial
{
    /// <summary>
    /// Testimonial identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Testimonial content (plain text, NO BBCode)
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
