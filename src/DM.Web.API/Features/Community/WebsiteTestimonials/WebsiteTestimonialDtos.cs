using System;
using System.ComponentModel.DataAnnotations;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Community.WebsiteTestimonials;

/// <summary>
/// Website testimonial information (positive review about the website)
/// </summary>
/// <remarks>
/// Plain text only - NO BBCode support.
/// Only positive testimonials are allowed.
/// One testimonial per user.
/// NO likes support.
/// </remarks>
public class WebsiteTestimonialDto
{
    /// <summary>
    /// Testimonial unique identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Testimonial author details (user writing the testimonial)
    /// </summary>
    public User? Author { get; set; }

    /// <summary>
    /// Testimonial content (plain text, NO BBCode)
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Creation timestamp (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Last modification timestamp (UTC)
    /// </summary>
    public DateTimeOffset? ModifiedUtc { get; set; }
}

/// <summary>
/// Request to create a new website testimonial
/// </summary>
public class CreateWebsiteTestimonialRequest
{
    /// <summary>
    /// Testimonial text content (10-1000 characters, plain text, positive only)
    /// </summary>
    [Required(ErrorMessage = "Testimonial text is required")]
    [StringLength(1000, MinimumLength = 10, ErrorMessage = "Testimonial text must be between 10 and 1000 characters")]
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// Request to update an existing website testimonial
/// </summary>
public class UpdateWebsiteTestimonialRequest
{
    /// <summary>
    /// Updated testimonial text (10-1000 characters, plain text, positive only)
    /// </summary>
    [StringLength(1000, MinimumLength = 10, ErrorMessage = "Testimonial text must be between 10 and 1000 characters")]
    public string? Text { get; set; }
}
