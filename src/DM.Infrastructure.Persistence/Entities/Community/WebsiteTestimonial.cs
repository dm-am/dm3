using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Contracts;
using DM.Infrastructure.Persistence.Entities.Account;

namespace DM.Infrastructure.Persistence.Entities.Community;

/// <summary>
/// DAL model for website testimonial (positive review about the website)
/// </summary>
/// <remarks>
/// Plain text only - NO BBCode support.
/// Only positive testimonials are allowed.
/// One testimonial per user.
/// No likes support.
/// </remarks>
[Table("WebsiteTestimonials")]
public class WebsiteTestimonial : ISoftDeletable
{
    /// <summary>
    /// Testimonial identifier
    /// </summary>
    [Key]
    public Guid WebsiteTestimonialId { get; set; }

    /// <summary>
    /// Author identifier
    /// </summary>
    public Guid AuthorId { get; set; }

    /// <summary>
    /// Creation moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Last modification moment (UTC)
    /// </summary>
    public DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>
    /// Last editor user identifier
    /// </summary>
    public Guid? ModifiedByUserId { get; set; }

    /// <summary>
    /// Testimonial content (plain text, NO BBCode)
    /// </summary>
    [Required]
    public string Text { get; set; } = string.Empty;

    /// <inheritdoc />
    public bool IsRemoved { get; set; }

    /// <inheritdoc />
    public Guid? DeletedByUserId { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? DeletedUtc { get; set; }

    /// <summary>
    /// Author
    /// </summary>
    [ForeignKey(nameof(AuthorId))]
    public virtual User Author { get; set; } = null!;

    /// <summary>
    /// Last editor
    /// </summary>
    [ForeignKey(nameof(ModifiedByUserId))]
    public virtual User? ModifiedBy { get; set; }

    /// <summary>
    /// User who deleted the testimonial
    /// </summary>
    [ForeignKey(nameof(DeletedByUserId))]
    public virtual User? DeletedBy { get; set; }
}
