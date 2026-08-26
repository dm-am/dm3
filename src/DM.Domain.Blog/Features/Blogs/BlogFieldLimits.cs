namespace DM.Domain.Blog.Features.Blogs;

/// <summary>
/// How long the text fields of a blog and of a rubric may be.
/// </summary>
/// <remarks>
/// One declaration because creation and editing must agree. They already
/// disagreed once, for games: the create validator read a shared constant while
/// the edit validator kept its own numbers, so a title creation accepted could
/// not be saved again after any change to the row.
/// </remarks>
internal static class BlogFieldLimits
{
    /// <summary>Longest blog title.</summary>
    public const int BlogTitleMaxLength = 200;

    /// <summary>Longest rubric title.</summary>
    public const int RubricTitleMaxLength = 100;
}
