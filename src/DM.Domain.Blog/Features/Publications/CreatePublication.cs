using System;

namespace DM.Domain.Blog.Features.Publications;

public class CreatePublication
{
    public Guid BlogId { get; set; }

    public Guid? RubricId { get; set; }

    public string Title { get; set; } = null!;

    public string Content { get; set; } = null!;

    /// <summary>
    /// Short preview/excerpt
    /// </summary>
    public string? Preview { get; set; }
}
