using System;
using System.Linq;
using DM.Domain.Core.Enums;

namespace DM.Domain.Blog.Features.Blogs;

/// <summary>
/// Blog role rendering, and the predicates that read roles off a blog
/// </summary>
public static class BlogRoleExtensions
{
    /// <summary>
    /// Convert BlogRole to API string representation
    /// </summary>
    public static string ToApiString(this BlogRole role) => role switch
    {
        BlogRole.Author => "author",
        BlogRole.Mentor => "mentor",
        BlogRole.Assistant => "assistant",
        BlogRole.Reader => "reader",
        _ => "unknown"
    };

    /// <summary>
    /// Whether the blog is the user's own — the predicate behind the ordinary
    /// ban's exemption, the blog side of what IsOwnGame answers for games.
    /// </summary>
    /// <remarks>
    /// The author, the assistants and the curating mentor. A subscriber is not
    /// here: subscribing is self-service and takes one request, so counting it
    /// would let a banned user undo the ban by pressing a button.
    ///
    /// One definition on purpose. The blog resolver and the publication comment
    /// service both ask this question, and two copies of it answer "you may
    /// comment in the blog but not under its publication" the moment one of them
    /// is edited.
    /// </remarks>
    public static bool IsOwnBlog(this Blog blog, Guid userId) =>
        blog.Author.UserId == userId ||
        blog.Assistants.Any(a => a.UserId == userId) ||
        blog.Mentor?.UserId == userId;
}
