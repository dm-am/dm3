using System;
using System.Linq;
using System.Linq.Expressions;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Blog.Features.Comments;
using DM.Domain.Blog.Features.PublicationComments;
using DM.Domain.Blog.Features.Publications;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Shared.Comments;
using DM.Infrastructure.Persistence.Shared.Queries;
using DM.Infrastructure.Persistence.Shared.Users;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using DbPublication = DM.Infrastructure.Persistence.Entities.Blog.Publication;
using DbRubric = DM.Infrastructure.Persistence.Entities.Blog.Rubric;
using BlogDto = DM.Domain.Blog.Features.Blogs.Blog;

namespace DM.Infrastructure.Persistence.Repositories.Blog;

/// <summary>
/// Projection formulas for the blog module. The rubric shape is one spliced
/// expression: it is asked for on its own, under a blog, under a publication,
/// and once over a row already in memory. It used to be four copies "that must
/// stay identical", with a note saying a fourth copy would be the moment to
/// merge them; the fourth copy was written and the note was not read.
/// </summary>
internal static class BlogMappers
{
    /// <summary>
    /// The one rubric formula. The publication count is published-only,
    /// mirroring how a game room's TotalPostsCount is a projection subquery
    /// over its posts; unread counts need the current viewer and are filled in
    /// the service.
    /// </summary>
    private static readonly Expression<Func<DbRubric, Rubric>> RubricProjection =
        r => new Rubric
        {
            Id = r.RubricId,
            Title = r.Title,
            SortOrder = r.SortOrder,
            PublicationCount = r.Publications.Count(p => !p.IsRemoved && p.IsPublished)
        };

    private static readonly Func<DbRubric, Rubric> CompiledRubric = RubricProjection.Compile();

    /// <summary>
    /// EF projection to the domain blog. Subscriber facts, blacklist and the
    /// per-viewer unread counters are populated by the repository/service
    /// after the query.
    /// </summary>
    public static IQueryable<BlogDto> ProjectToBlog(this IQueryable<DbBlog> query) =>
        query.Select(ExpressionSplicer.Expand<Func<DbBlog, BlogDto>>(b => new BlogDto
        {
            Id = b.BlogId,
            PublicId = b.PublicId,
            Title = b.Title,
            Description = b.Description,
            CreatedUtc = b.CreatedUtc,
            Status = b.Status,
            PremoderationStatus = b.PremoderationStatus,
            ActivatedUtc = b.ActivatedUtc,
            ClosedUtc = b.ClosedUtc,
            ClosedReason = b.ClosedReason,
            DraftVisibility = b.DraftVisibility,
            CommentsEnabled = b.CommentsEnabled,
            PublicationCount = b.PublicationCount,
            CommentCount = b.CommentCount,
            // The blog discussion plus every published publication's thread.
            CommentsCount = b.CommentCount + b.Publications
                .Where(p => !p.IsRemoved && p.IsPublished)
                .Sum(p => p.CommentCount),
            LastCommentId = b.LastCommentId,
            Author = GeneralUserProjections.Projection.Splice(b.Author),
            Mentor = GeneralUserProjections.Projection.Splice(b.Mentor),
            Rubrics = b.Rubrics.Select(r => RubricProjection.Splice(r)).ToList(),
            Assistants = b.Assistants
                .Select(a => new BlogAssistantInfo
                {
                    UserId = a.UserId,
                    Username = a.User.Username,
                    JoinedUtc = a.JoinedUtc,
                    LastActivityUtc = a.User.LastActivityUtc,
                    Role = a.User.Role,
                    IsNewbie = a.User.IsNewbie
                })
                .ToList(),
            PendingInvitedUserIds = b.Tokens
                .Where(t => t.Type == TokenType.BlogAssistantInvitation
                    || t.Type == TokenType.BlogReaderInvitation)
                .Select(t => t.UserId)
                .ToList()
        }));

    /// <summary>EF projection to the domain rubric</summary>
    public static IQueryable<Rubric> ProjectToRubric(this IQueryable<DbRubric> query) =>
        query.Select(ExpressionSplicer.Expand(RubricProjection));

    /// <summary>
    /// Loaded entity (publications included) to the domain rubric, for the
    /// write path that already holds the row
    /// </summary>
    public static Rubric ToRubric(this DbRubric rubric) => CompiledRubric(rubric);

    /// <summary>
    /// EF projection to the domain publication. The blog title travels via
    /// the navigation - a SQL join - so the profile "best publication" can
    /// name the blog without a second query.
    /// </summary>
    public static IQueryable<Publication> ProjectToPublication(this IQueryable<DbPublication> query) =>
        query.Select(ExpressionSplicer.Expand<Func<DbPublication, Publication>>(p => new Publication
        {
            Id = p.PublicationId,
            BlogId = p.BlogId,
            BlogTitle = p.Blog.Title,
            Title = p.Title,
            Content = p.Content,
            Preview = p.Preview,
            CreatedUtc = p.CreatedUtc,
            ModifiedUtc = p.ModifiedUtc,
            IsPublished = p.IsPublished,
            PublishedUtc = p.PublishedUtc,
            CommentsEnabled = p.CommentsEnabled,
            ViewCount = p.ViewCount,
            CommentCount = p.CommentCount,
            LastCommentId = p.LastCommentId,
            Author = GeneralUserProjections.Projection.Splice(p.Author),
            Rubric = p.Rubric == null
                ? null!
                : RubricProjection.Splice(p.Rubric)
        }));

    /// <summary>
    /// EF projection for the blog-comment delete path; the counters are set
    /// by the repository afterwards.
    /// </summary>
    public static IQueryable<BlogCommentToDelete> ProjectToBlogCommentToDelete(
        this IQueryable<DbComment> query) =>
        query.Select(ExpressionSplicer.Expand(
            CommentProjections.WithCommentFields<BlogCommentToDelete>(c => new BlogCommentToDelete())));

    /// <summary>
    /// EF projection for the publication-comment delete path; the counters
    /// are set by the repository afterwards.
    /// </summary>
    public static IQueryable<PublicationCommentToDelete> ProjectToPublicationCommentToDelete(
        this IQueryable<DbComment> query) =>
        query.Select(ExpressionSplicer.Expand(
            CommentProjections.WithCommentFields<PublicationCommentToDelete>(c => new PublicationCommentToDelete())));
}
