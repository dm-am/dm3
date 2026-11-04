using System;
using System.Threading.Tasks;
using DM.Domain.Blog.Authorization;
using DM.Domain.Blog.Features.Comments;
using DM.Domain.Blog.Features.Likes;
using DM.Domain.Blog.Features.PublicationComments;
using DM.Domain.Blog.Features.Publications;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Likes;
using DM.Testing;
using NSubstitute;
using Xunit;

namespace DM.Domain.Blog.Tests.Features.Likes;

/// <summary>
/// A like is gated by the like intention of its subject and by nothing else. The
/// blog's blacklist closes comments only, so the service does not read the blog
/// at all; that a blacklisted reader likes like anybody is held end to end by
/// BlacklistRuleShould.
/// </summary>
public class BlogLikeServiceShould : UnitTestBase
{
    private readonly IPublicationService publicationService;
    private readonly IBlogCommentService blogCommentService;
    private readonly IPublicationCommentService publicationCommentService;
    private readonly IIntentionManager intentionManager;
    private readonly ILikeOperations likeOperations;
    private readonly BlogLikeService service;

    public BlogLikeServiceShould()
    {
        publicationService = Mock<IPublicationService>();
        blogCommentService = Mock<IBlogCommentService>();
        publicationCommentService = Mock<IPublicationCommentService>();
        intentionManager = Mock<IIntentionManager>();
        likeOperations = Mock<ILikeOperations>();

        service = new BlogLikeService(
            publicationService,
            blogCommentService,
            publicationCommentService,
            intentionManager,
            likeOperations);
    }

    [Fact]
    public async Task AuthorizeLikeBlogCommentAction()
    {
        var comment = new Comment { Id = Guid.NewGuid(), EntityId = Guid.NewGuid() };
        blogCommentService.GetAsync(comment.Id).Returns(comment);
        likeOperations.LikeAsync(comment, EventType.LikedBlogComment).Returns(new GeneralUser());

        await service.LikeBlogCommentAsync(comment.Id);

        intentionManager.Received(1).ThrowIfForbidden(CommentIntention.Like, comment);
        await likeOperations.Received(1).LikeAsync(comment, EventType.LikedBlogComment);
    }

    [Fact]
    public async Task AuthorizeLikePublicationCommentAction()
    {
        var comment = new Comment { Id = Guid.NewGuid(), EntityId = Guid.NewGuid() };
        publicationCommentService.GetAsync(comment.Id).Returns(comment);
        likeOperations.LikeAsync(comment, EventType.LikedPublicationComment).Returns(new GeneralUser());

        await service.LikePublicationCommentAsync(comment.Id);

        intentionManager.Received(1).ThrowIfForbidden(CommentIntention.Like, comment);
        await likeOperations.Received(1).LikeAsync(comment, EventType.LikedPublicationComment);
    }

    [Fact]
    public async Task AuthorizeLikePublicationAction()
    {
        var publication = new Publication { Id = Guid.NewGuid(), BlogId = Guid.NewGuid() };
        publicationService.GetPublication(publication.Id, default).Returns(publication);
        likeOperations.LikeAsync(publication, EventType.LikedPublication).Returns(new GeneralUser());

        await service.LikePublicationAsync(publication.Id);

        intentionManager.Received(1).ThrowIfForbidden(PublicationIntention.Like, publication);
        await likeOperations.Received(1).LikeAsync(publication, EventType.LikedPublication);
    }

    [Fact]
    public async Task AuthorizeTakingALikeBack()
    {
        var publication = new Publication { Id = Guid.NewGuid(), BlogId = Guid.NewGuid() };
        publicationService.GetPublication(publication.Id, default).Returns(publication);

        await service.UnlikePublicationAsync(publication.Id);

        intentionManager.Received(1).ThrowIfForbidden(PublicationIntention.Like, publication);
        await likeOperations.Received(1).UnlikeAsync(publication);
    }
}
