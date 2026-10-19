namespace DM.Domain.Blog.Features.Blogs;

/// <summary>
/// How long the text fields of a blog and of a rubric may be.
/// </summary>
/// <remarks>
/// One declaration, so that a title creation accepts can be saved again on edit.
/// </remarks>
internal static class BlogFieldLimits
{
    public const int BlogTitleMaxLength = 200;

    public const int RubricTitleMaxLength = 100;
}
