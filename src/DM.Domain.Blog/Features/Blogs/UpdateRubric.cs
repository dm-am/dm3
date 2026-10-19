using System;

namespace DM.Domain.Blog.Features.Blogs;

/// <summary>
/// A null field is left as it is.
/// </summary>
public class UpdateRubric
{
    public Guid RubricId { get; set; }

    public string? Title { get; set; }

    public int? SortOrder { get; set; }
}
