using System;

namespace DM.Domain.Blog.Features.Blogs;

public class CreateRubric
{
    public Guid BlogId { get; set; }

    public string Title { get; set; } = null!;

    public int SortOrder { get; set; }
}
