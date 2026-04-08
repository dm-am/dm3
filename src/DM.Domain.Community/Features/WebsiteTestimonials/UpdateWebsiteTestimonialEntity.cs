using System;

namespace DM.Domain.Community.Features.WebsiteTestimonials;

/// <summary>
/// DTO for updating a website testimonial entity in repository
/// </summary>
public class UpdateWebsiteTestimonialEntity
{
    /// <summary>
    /// Testimonial identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Last modification moment (UTC)
    /// </summary>
    public DateTime ModifiedUtc { get; set; }

    /// <summary>
    /// Last editor user identifier
    /// </summary>
    public Guid ModifiedByUserId { get; set; }

    /// <summary>
    /// Testimonial content (plain text, NO BBCode)
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
