using System;
using System.Linq;
using System.Net;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;

namespace DM.Domain.Blog.Features.Blogs;

/// <summary>
/// Blog role rendering, and the predicates that read roles off a blog
/// </summary>
public static class BlogRoleExtensions
{
    public static string ToApiString(this BlogRole role) => role switch
    {
        BlogRole.Author => "author",
        BlogRole.Mentor => "mentor",
        BlogRole.Assistant => "assistant",
        BlogRole.Reader => "reader",
        _ => "unknown"
    };

    /// <summary>
    /// Whether the blog is the user's own (author, assistant, mentor), the
    /// ordinary ban's exemption, as IsOwnGame is for games.
    /// </summary>
    /// <remarks>
    /// A subscriber is not here: subscribing takes one request, and would let a
    /// banned user undo the ban.
    /// </remarks>
    public static bool IsOwnBlog(this Blog blog, Guid userId) =>
        blog.Author?.UserId == userId ||
        blog.Assistants.Any(a => a.UserId == userId) ||
        blog.Mentor?.UserId == userId;

    /// <summary>
    /// Whether the blog's owner put this user on its blacklist.
    /// </summary>
    /// <remarks>
    /// It closes comments and nothing else: reading, subscribing and likes stay
    /// open. BlacklistRuleShould holds the one spelling and the places that ask it.
    /// </remarks>
    public static bool IsBlacklisted(this Blog blog, Guid userId) =>
        blog.BlacklistedUserIds.Contains(userId);

    /// <summary>
    /// Refuse the user an action the blog's blacklist closes to them, in the
    /// words that say so.
    /// </summary>
    public static void ThrowIfBlacklisted(this Blog blog, Guid userId)
    {
        if (blog.IsBlacklisted(userId))
        {
            throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.BlacklistedFromBlog);
        }
    }
}
