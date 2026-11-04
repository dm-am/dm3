using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Blog.Authorization;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Exceptions;

namespace DM.Domain.Blog.Features.Blogs;

/// <summary>
/// A refusal on a blog the caller may not even see is a 404 and not a 403.
/// </summary>
/// <remarks>
/// The services that act on a blog read it unfiltered and let the intention
/// decide, because the people the intention admits always see the blog. For
/// everybody else the 403 says "it exists": the alias is five letters and the
/// reads of the blog already answer a hidden one as one that is not there
/// (BlogService.IsVisibleToViewer). So only the refusal asks whether the blog is
/// visible, and a hidden one answers as the reads do.
/// </remarks>
internal static class HiddenBlogRefusal
{
    /// <summary>
    /// The blog read unfiltered and refused by the intention, answering as a blog
    /// that is not there where the caller may not see the blog at all.
    /// </summary>
    public static async Task<Blog> GetPermittedBlogAsync(this IIntentionManager intentionManager,
        IBlogService blogService, BlogIntention intention, Guid blogId, CancellationToken ct = default)
    {
        var blog = await blogService.GetUnfiltered(blogId, ct);
        try
        {
            intentionManager.ThrowIfForbidden(intention, blog);
        }
        catch (HttpException refusal) when (refusal.StatusCode == HttpStatusCode.Forbidden)
        {
            if (!await blogService.IsVisibleToViewerAsync(blog.Id, ct))
            {
                throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.BlogNotFound);
            }

            throw;
        }

        return blog;
    }
}
