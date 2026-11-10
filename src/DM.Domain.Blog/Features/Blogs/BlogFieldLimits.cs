namespace DM.Domain.Blog.Features.Blogs;

// One declaration, so that a title creation accepts can be saved again on edit.
internal static class BlogFieldLimits
{
    public const int BlogTitleMaxLength = 200;

    public const int RubricTitleMaxLength = 100;
}
