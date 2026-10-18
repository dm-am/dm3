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
        blog.Author?.UserId == userId ||
        blog.Assistants.Any(a => a.UserId == userId) ||
        blog.Mentor?.UserId == userId;

    /// <summary>
    /// Whether the blog's owner put this user on its blacklist.
    /// </summary>
    /// <remarks>
    /// The rule of the blog's blacklist, written once, the blog side of
    /// GameRoleExtensions.IsBlacklisted. It closes comments and nothing else: in
    /// the blog's discussion and under its publications, a new one and the
    /// rewrite of one's own. Reading, subscribing and likes stay open.
    ///
    /// BlogIntention.CreateComment asks this predicate so an IsAllowed answer does
    /// not lie; the services ask <see cref="ThrowIfBlacklisted" /> first, so the
    /// refusal says why. BlacklistRuleShould holds the one spelling and the short
    /// list of places that ask it.
    /// </remarks>
    public static bool IsBlacklisted(this Blog blog, Guid userId) =>
        blog.BlacklistedUserIds.Contains(userId);

    /// <summary>
    /// Refuse the user an action the blog's blacklist closes to them, in the
    /// words that say so.
    /// </summary>
    /// <param name="blog">Blog the action writes into</param>
    /// <param name="userId">The user acting on their own behalf</param>
    public static void ThrowIfBlacklisted(this Blog blog, Guid userId)
    {
        if (blog.IsBlacklisted(userId))
        {
            throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.BlacklistedFromBlog);
        }
    }
}
