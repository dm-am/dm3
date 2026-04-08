using System;
using DM.Domain.Core.Dto;

namespace DM.Domain.Community.Features.WebsiteTestimonials;

/// <summary>
/// Domain DTO for website testimonial (positive review about the website)
/// </summary>
/// <remarks>
/// Plain text only - NO BBCode support.
/// Only positive testimonials are allowed.
/// One testimonial per user.
/// NO likes support.
/// </remarks>
public class WebsiteTestimonial
{
    /// <summary>
    /// Testimonial identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Author (user writing the testimonial)
    /// </summary>
    public GeneralUser Author { get; set; } = null!;

    /// <summary>
    /// Creation moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Last modification moment (UTC)
    /// </summary>
    public DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>
    /// Testimonial content (plain text, NO BBCode)
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
