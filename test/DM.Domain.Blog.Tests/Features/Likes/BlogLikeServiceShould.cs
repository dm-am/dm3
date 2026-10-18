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
    private readonly IPublicationService _publicationService;
    private readonly IBlogCommentService _blogCommentService;
    private readonly IPublicationCommentService _publicationCommentService;
    private readonly IIntentionManager _intentionManager;
    private readonly ILikeOperations _likeOperations;
    private readonly BlogLikeService _service;

    public BlogLikeServiceShould()
    {
        _publicationService = Mock<IPublicationService>();
        _blogCommentService = Mock<IBlogCommentService>();
        _publicationCommentService = Mock<IPublicationCommentService>();
        _intentionManager = Mock<IIntentionManager>();
        _likeOperations = Mock<ILikeOperations>();

        _service = new BlogLikeService(
            _publicationService,
            _blogCommentService,
            _publicationCommentService,
            _intentionManager,
            _likeOperations);
    }

    [Fact]
    public async Task AuthorizeLikeBlogCommentAction()
    {
        var comment = new Comment { Id = Guid.NewGuid(), EntityId = Guid.NewGuid() };
        _blogCommentService.GetAsync(comment.Id).Returns(comment);
        _likeOperations.LikeAsync(comment, EventType.LikedBlogComment).Returns(new GeneralUser());

        await _service.LikeBlogCommentAsync(comment.Id);

        _intentionManager.Received(1).ThrowIfForbidden(CommentIntention.Like, comment);
        await _likeOperations.Received(1).LikeAsync(comment, EventType.LikedBlogComment);
    }

    [Fact]
    public async Task AuthorizeLikePublicationCommentAction()
    {
        var comment = new Comment { Id = Guid.NewGuid(), EntityId = Guid.NewGuid() };
        _publicationCommentService.GetAsync(comment.Id).Returns(comment);
        _likeOperations.LikeAsync(comment, EventType.LikedPublicationComment).Returns(new GeneralUser());

        await _service.LikePublicationCommentAsync(comment.Id);

        _intentionManager.Received(1).ThrowIfForbidden(CommentIntention.Like, comment);
        await _likeOperations.Received(1).LikeAsync(comment, EventType.LikedPublicationComment);
    }

    [Fact]
    public async Task AuthorizeLikePublicationAction()
    {
        var publication = new Publication { Id = Guid.NewGuid(), BlogId = Guid.NewGuid() };
        _publicationService.GetPublication(publication.Id, default).Returns(publication);
        _likeOperations.LikeAsync(publication, EventType.LikedPublication).Returns(new GeneralUser());

        await _service.LikePublicationAsync(publication.Id);

        _intentionManager.Received(1).ThrowIfForbidden(PublicationIntention.Like, publication);
        await _likeOperations.Received(1).LikeAsync(publication, EventType.LikedPublication);
    }

    [Fact]
    public async Task AuthorizeTakingALikeBack()
    {
        var publication = new Publication { Id = Guid.NewGuid(), BlogId = Guid.NewGuid() };
        _publicationService.GetPublication(publication.Id, default).Returns(publication);

        await _service.UnlikePublicationAsync(publication.Id);

        _intentionManager.Received(1).ThrowIfForbidden(PublicationIntention.Like, publication);
        await _likeOperations.Received(1).UnlikeAsync(publication);
    }
}
