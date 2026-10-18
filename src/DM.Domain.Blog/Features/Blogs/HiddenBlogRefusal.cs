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
    /// Refuse by the intention, answering as a blog that is not there where the
    /// caller may not see the blog at all.
    /// </summary>
    /// <param name="intentionManager">Intention manager</param>
    /// <param name="blogService">Blog service, for the visibility of the blog</param>
    /// <param name="intention">Action asked for</param>
    /// <param name="blog">The blog, read unfiltered</param>
    /// <param name="ct">Cancellation token</param>
    public static async Task ThrowIfForbiddenOrHiddenAsync(this IIntentionManager intentionManager,
        IBlogService blogService, BlogIntention intention, Blog blog, CancellationToken ct = default)
    {
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
    }
}
