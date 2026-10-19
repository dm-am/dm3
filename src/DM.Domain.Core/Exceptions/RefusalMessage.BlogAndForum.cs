namespace DM.Domain.Core.Exceptions;

/// <summary>
/// Refusals of blogs, the forum and the comments under both
/// </summary>
public static partial class RefusalMessage
{
    // Blogs and forum

    /// <summary>
    /// The blog is absent, or hidden from this caller
    /// </summary>
    public const string BlogNotFound = "Блог не найден";

    /// <summary>
    /// The rubric is absent, or belongs to another blog
    /// </summary>
    public const string RubricNotFound = "Рубрика не найдена";

    /// <summary>
    /// The blog's owner put this caller on its blacklist
    /// </summary>
    /// <remarks>
    /// Said by BlogRoleExtensions.ThrowIfBlacklisted and by nothing else, the
    /// blog side of BlacklistedFromGame.
    /// </remarks>
    public const string BlacklistedFromBlog = "Вы в черном списке этого блога";

    /// <summary>
    /// The comment is absent, on any of the four commented surfaces
    /// </summary>
    public static string CommentNotFound(Guid commentId) =>
        $"Комментарий {commentId} не найден";

    // Forum

    /// <summary>
    /// The board is absent. Addressed by alias or by title, and both are echoed
    /// back because the caller sent them
    /// </summary>
    public static string BoardNotFound(string aliasOrTitle) =>
        $"Раздел {aliasOrTitle} не найден";
}
