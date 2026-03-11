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

namespace DM.Infrastructure.Persistence.Repositories.Blog;

/// <summary>
/// AutoMapper profile for blog repository mappings
/// </summary>
internal class BlogMappingProfile : Profile
{
    /// <inheritdoc />
    public BlogMappingProfile()
    {
        // Comment mappings
        CreateMap<DbComment, Comment>()
            .ForMember(d => d.Id, s => s.MapFrom(c => c.CommentId))
            .ForMember(d => d.EntityId, s => s.MapFrom(c => c.EntityId))
            .ForMember(d => d.Author, s => s.MapFrom(c => c.Author))
            .ForMember(d => d.Text, s => s.MapFrom(c => c.Text))
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(c => c.CreatedUtc))
            .ForMember(d => d.ModifiedUtc, s => s.MapFrom(c => c.ModifiedUtc))
            .ForMember(d => d.Likes, s => s.Ignore());

        CreateMap<DbComment, BlogCommentToDelete>()
            .ForMember(d => d.Id, s => s.MapFrom(c => c.CommentId))
            .ForMember(d => d.EntityId, s => s.MapFrom(c => c.EntityId))
            .ForMember(d => d.Author, s => s.MapFrom(c => c.Author))
            .ForMember(d => d.Text, s => s.MapFrom(c => c.Text))
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(c => c.CreatedUtc))
            .ForMember(d => d.ModifiedUtc, s => s.MapFrom(c => c.ModifiedUtc))
            .ForMember(d => d.Likes, s => s.Ignore())
            .ForMember(d => d.BlogCommentCount, s => s.Ignore())
            .ForMember(d => d.IsLastComment, s => s.Ignore());

        CreateMap<DbComment, PublicationCommentToDelete>()
            .ForMember(d => d.Id, s => s.MapFrom(c => c.CommentId))
            .ForMember(d => d.EntityId, s => s.MapFrom(c => c.EntityId))
            .ForMember(d => d.Author, s => s.MapFrom(c => c.Author))
            .ForMember(d => d.Text, s => s.MapFrom(c => c.Text))
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(c => c.CreatedUtc))
            .ForMember(d => d.ModifiedUtc, s => s.MapFrom(c => c.ModifiedUtc))
            .ForMember(d => d.Likes, s => s.Ignore())
            .ForMember(d => d.PublicationCommentCount, s => s.Ignore())
            .ForMember(d => d.IsLastComment, s => s.Ignore());

        // Blog mappings
        CreateMap<DbBlog, BlogModel>()
            .ForMember(d => d.Id, s => s.MapFrom(b => b.BlogId))
            .ForMember(d => d.CreatedAt, s => s.MapFrom(b => b.CreatedUtc))
            .ForMember(d => d.UpdatedAt, s => s.MapFrom(b => b.UpdatedUtc))
            .ForMember(d => d.CommentsCount, s => s.MapFrom(b =>
                b.CommentCount + (b.Publications != null ? b.Publications.Sum(p => p.CommentCount) : 0)))
            .ForMember(d => d.Assistants, s => s.MapFrom(b => b.Assistants != null
                ? b.Assistants.Select(a => new BlogAssistantInfo { UserId = a.UserId, JoinedUtc = a.JoinedUtc })
                : new List<BlogAssistantInfo>()))
            .ForMember(d => d.SubscriberIds, s => s.Ignore()) // Populated separately via SubscriptionService
            .ForMember(d => d.PendingInvitedUserIds, s => s.MapFrom(b => b.Tokens
                .Where(t => t.Type == TokenType.BlogAssistantInvitation || t.Type == TokenType.BlogReaderInvitation)
                .Select(t => t.UserId)));

        CreateMap<DbRubric, Rubric>()
            .ForMember(d => d.Id, s => s.MapFrom(r => r.RubricId));

        CreateMap<DbPublication, Publication>()
            .ForMember(d => d.Id, s => s.MapFrom(p => p.PublicationId))
            .ForMember(d => d.Author, s => s.MapFrom(p => p.Author))
            .ForMember(d => d.CreatedAt, s => s.MapFrom(p => p.CreatedUtc))
            .ForMember(d => d.ModifiedAt, s => s.MapFrom(p => p.ModifiedUtc))
            .ForMember(d => d.PublishedAt, s => s.MapFrom(p => p.PublishedUtc))
            .ForMember(d => d.Likes, s => s.Ignore()); // Likes fetched via EntityType+EntityId pattern
    }
}
