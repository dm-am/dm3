using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Blog.Features.Blogs;

/// <summary>
/// A null field is left as it is.
/// </summary>
public class UpdateBlog
{
    public Guid BlogId { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public DraftVisibility? DraftVisibility { get; set; }
}
