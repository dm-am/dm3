using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Blog.Features.Comments;
using DM.Domain.Blog.Features.PublicationComments;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Enums;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using DbPublication = DM.Infrastructure.Persistence.Entities.Blog.Publication;
using DbRubric = DM.Infrastructure.Persistence.Entities.Blog.Rubric;
using BlogDto = DM.Domain.Blog.Features.Blogs.Blog;

namespace DM.Infrastructure.Persistence.Repositories.Blog;

/// <summary>
/// AutoMapper profile for blog repository mappings
/// </summary>
internal class BlogMappingProfile : Profile
{
    /// <inheritdoc />
    public BlogMappingProfile()
    {
        // Comment mappings. The comment itself is mapped by
        // CommentMappingProfile — every module's comments share one entity and
        // one DTO, so the pair belongs there and not once per module. The two
        // deletion shapes below are blog-specific.
        CreateMap<DbComment, BlogCommentToDelete>()
            .ForMember(d => d.Id, s => s.MapFrom(c => c.CommentId))
            .ForMember(d => d.EntityId, s => s.MapFrom(c => c.EntityId))
            .ForMember(d => d.Author, s => s.MapFrom(c => c.Author))
            .ForMember(d => d.Text, s => s.MapFrom(c => c.Text))
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(c => c.CreatedUtc))
            .ForMember(d => d.ModifiedUtc, s => s.MapFrom(c => c.Edits
                .OrderByDescending(e => e.EditedUtc)
                .Select(e => (DateTimeOffset?)e.EditedUtc)
                .FirstOrDefault()))
            .ForMember(d => d.Likes, s => s.Ignore())
            .ForMember(d => d.BlogCommentCount, s => s.Ignore())
            .ForMember(d => d.IsLastComment, s => s.Ignore());

        CreateMap<DbComment, PublicationCommentToDelete>()
            .ForMember(d => d.Id, s => s.MapFrom(c => c.CommentId))
            .ForMember(d => d.EntityId, s => s.MapFrom(c => c.EntityId))
            .ForMember(d => d.Author, s => s.MapFrom(c => c.Author))
            .ForMember(d => d.Text, s => s.MapFrom(c => c.Text))
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(c => c.CreatedUtc))
            .ForMember(d => d.ModifiedUtc, s => s.MapFrom(c => c.Edits
                .OrderByDescending(e => e.EditedUtc)
                .Select(e => (DateTimeOffset?)e.EditedUtc)
                .FirstOrDefault()))
            .ForMember(d => d.Likes, s => s.Ignore())
            .ForMember(d => d.PublicationCommentCount, s => s.Ignore())
            .ForMember(d => d.IsLastComment, s => s.Ignore());

        // Blog mappings
        // BlogAssistant -> BlogAssistantInfo mapping (required for EF Core projection)
        CreateMap<DM.Infrastructure.Persistence.Entities.Blog.BlogAssistant, BlogAssistantInfo>()
            .ForMember(d => d.UserId, s => s.MapFrom(a => a.UserId))
            .ForMember(d => d.Username, s => s.MapFrom(a => a.User.Username))
            .ForMember(d => d.JoinedUtc, s => s.MapFrom(a => a.JoinedUtc))
            .ForMember(d => d.LastActivityUtc, s => s.MapFrom(a => a.User.LastActivityUtc))
            .ForMember(d => d.Role, s => s.MapFrom(a => a.User.Role))
            .ForMember(d => d.IsNewbie, s => s.MapFrom(a => a.User.QuantityRating < 100));

        CreateMap<DbBlog, BlogDto>()
            .ForMember(d => d.Id, s => s.MapFrom(b => b.BlogId))
            .ForMember(d => d.Status, s => s.MapFrom(b => b.Status))
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(b => b.CreatedUtc))
            .ForMember(d => d.ActivatedUtc, s => s.MapFrom(b => b.ActivatedUtc))
            .ForMember(d => d.ClosedUtc, s => s.MapFrom(b => b.ClosedUtc))
            .ForMember(d => d.CommentsCount, s => s.MapFrom(b =>
                b.CommentCount + b.Publications
                    .Where(p => !p.IsRemoved && p.IsPublished)
                    .Sum(p => p.CommentCount)))
            .ForMember(d => d.Assistants, s => s.MapFrom(b => b.Assistants))
            .ForMember(d => d.SubscribersCount, s => s.Ignore()) // Populated in repository
            .ForMember(d => d.IsViewerSubscriber, s => s.Ignore()) // Populated in repository
            .ForMember(d => d.SubscriberUsernames, s => s.Ignore()) // Populated in repository
            .ForMember(d => d.ActiveSubscribersCount, s => s.Ignore()) // Populated in repository
            .ForMember(d => d.BlacklistedUserIds, s => s.Ignore()) // Populated separately
            .ForMember(d => d.UnreadPublicationsCount, s => s.Ignore()) // Populated in service layer
            .ForMember(d => d.UnreadCommentsCount, s => s.Ignore()) // Populated in service layer
            .ForMember(d => d.PendingInvitedUserIds, s => s.MapFrom(b => b.Tokens
                .Where(t => t.Type == TokenType.BlogAssistantInvitation || t.Type == TokenType.BlogReaderInvitation)
                .Select(t => t.UserId)));

        CreateMap<DbRubric, Rubric>()
            .ForMember(d => d.Id, s => s.MapFrom(r => r.RubricId))
            // Published-only publication count, mirroring how a game room's
            // TotalPostsCount is a projection subquery over its posts.
            .ForMember(d => d.PublicationCount, s => s.MapFrom(r => r.Publications
                .Count(p => !p.IsRemoved && p.IsPublished)))
            // Unread counts need the current viewer; filled in the service.
            .ForMember(d => d.UnreadPublicationsCount, s => s.Ignore())
            .ForMember(d => d.UnreadCommentsCount, s => s.Ignore());

        CreateMap<DbPublication, Publication>()
            .ForMember(d => d.Id, s => s.MapFrom(p => p.PublicationId))
            // Projected via the Blog navigation — ProjectTo turns this into a
            // SQL join, so the publication carries its blog's name (used by the
            // profile "best publication" instead of a bare blog id).
            .ForMember(d => d.BlogTitle, s => s.MapFrom(p => p.Blog.Title))
            .ForMember(d => d.Author, s => s.MapFrom(p => p.Author))
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(p => p.CreatedUtc))
            .ForMember(d => d.ModifiedUtc, s => s.MapFrom(p => p.ModifiedUtc))
            .ForMember(d => d.PublishedUtc, s => s.MapFrom(p => p.PublishedUtc))
            .ForMember(d => d.Likes, s => s.Ignore()) // Likes fetched via EntityType+EntityId pattern
            .ForMember(d => d.UnreadCommentsCount, s => s.Ignore()); // Populated in service layer
    }
}
