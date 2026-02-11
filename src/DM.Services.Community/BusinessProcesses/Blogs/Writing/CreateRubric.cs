using System;

namespace DM.Services.Community.BusinessProcesses.Blogs.Writing;

/// <summary>
/// DTO for creating a rubric
/// </summary>
public class CreateRubric
{
    /// <summary>
    /// Parent blog identifier
    /// </summary>
    public Guid BlogId { get; set; }

    /// <summary>
    /// Rubric title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Sort order
    /// </summary>
    public int SortOrder { get; set; }
}
