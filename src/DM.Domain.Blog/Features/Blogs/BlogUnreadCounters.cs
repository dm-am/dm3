using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Domain.Core.UnreadCounters;

namespace DM.Domain.Blog.Features.Blogs;

/// <summary>
/// The unread counters a read of blogs carries, the blog's own and those of
/// its rubrics
/// </summary>
/// <remarks>
/// A guest cannot mark anything read, so a guest is shown the totals in the
/// same fields; a reader is shown what is unread for them. The blog service
/// reads the rows and this fills the counters in on them.
/// </remarks>
internal sealed class BlogUnreadCounters
{
    private readonly IBlogRepository _repository;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IIdentityProvider _identityProvider;

    public BlogUnreadCounters(
        IBlogRepository repository,
        IUnreadCountersRepository unreadCountersRepository,
        IIdentityProvider identityProvider)
    {
        _repository = repository;
        _unreadCountersRepository = unreadCountersRepository;
        _identityProvider = identityProvider;
    }

    /// <summary>
    /// Fill the blog-level counters: publications with unread comments and the
    /// unread comments across all of them.
    /// </summary>
    public async Task FillBlogs(Blog[] blogs)
    {
        if (blogs.Length == 0) return;

        var identity = _identityProvider.Current;

        if (!identity.User.IsAuthenticated)
        {
            foreach (var blog in blogs)
            {
                blog.UnreadPublicationsCount = blog.PublicationCount;
                blog.UnreadCommentsCount = blog.CommentsCount;
            }
            return;
        }

        var userId = identity.User.UserId;

        // Sequential on purpose: both fills query the one DmDbContext of this
        // scope, and a context refuses parallel operations.
        await _unreadCountersRepository.FillParentCounters(blogs, userId,
            b => b.Id, b => b.UnreadPublicationsCount);
        await _unreadCountersRepository.FillTotalUnreadCounters(blogs, userId,
            b => b.Id, b => b.UnreadCommentsCount);
    }

    /// <summary>
    /// Fill the per-rubric "(N/A)" counter from the per-publication counters: a
    /// rubric is not itself an unread-counter entity, its publications are.
    /// </summary>
    public async Task FillRubrics(Guid blogId, ICollection<Rubric> rubrics)
    {
        if (rubrics.Count == 0) return;

        var publicationIdsByRubric = await _repository.GetRubricPublicationIds(blogId);
        var allPublicationIds = publicationIdsByRubric.Values
            .SelectMany(ids => ids)
            .Distinct()
            .ToArray();
        if (allPublicationIds.Length == 0) return;

        // A guest's id is Guid.Empty, the template counter that holds the totals.
        var user = _identityProvider.Current.User;
        var unreadByPublication = await _unreadCountersRepository.SelectByEntitiesAsync(
            user.UserId, UnreadEntryType.Message, allPublicationIds);

        foreach (var rubric in rubrics)
        {
            if (!publicationIdsByRubric.TryGetValue(rubric.Id, out var ids)) continue;

            rubric.UnreadCommentsCount = ids.Sum(
                id => unreadByPublication.TryGetValue(id, out var count) ? count : 0);

            rubric.UnreadPublicationsCount = user.IsAuthenticated
                ? ids.Count(id => unreadByPublication.TryGetValue(id, out var count) && count > 0)
                : rubric.PublicationCount;
        }
    }

    /// <summary>
    /// Fill the unread counters on a single blog's embedded rubrics. The
    /// rubric list must be materialized so mutations survive serialization.
    /// </summary>
    public async Task FillBlogRubrics(Blog blog)
    {
        var rubrics = blog.Rubrics as ICollection<Rubric> ?? blog.Rubrics.ToArray();
        if (rubrics.Count == 0) return;

        await FillRubrics(blog.Id, rubrics);
        blog.Rubrics = rubrics;
    }
}
