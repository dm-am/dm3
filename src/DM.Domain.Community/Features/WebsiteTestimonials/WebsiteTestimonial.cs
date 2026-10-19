using System;
using DM.Domain.Core.Dto;

namespace DM.Domain.Community.Features.WebsiteTestimonials;

/// <summary>
/// Positive review about the website, one per user
/// </summary>
/// <remarks>
/// Plain text, no BBCode rendering, and no likes.
/// </remarks>
public class WebsiteTestimonial
{
    public Guid Id { get; set; }

    /// <summary>
    /// Author (user writing the testimonial)
    /// </summary>
    public GeneralUser Author { get; set; } = null!;

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset? ModifiedUtc { get; set; }

    public string Text { get; set; } = string.Empty;
}
