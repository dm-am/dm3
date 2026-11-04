using System;

namespace DM.Domain.Blog.Features.Publications;

/// <summary>
/// A null field is left as it is.
/// </summary>
public class UpdatePublication
{
    public Guid PublicationId { get; set; }

    public Guid? RubricId { get; set; }

    public bool ClearRubric { get; set; }

    public string? Title { get; set; }

    public string? Content { get; set; }

    /// <summary>
    /// Short preview/excerpt
    /// </summary>
    public string? Preview { get; set; }
}
