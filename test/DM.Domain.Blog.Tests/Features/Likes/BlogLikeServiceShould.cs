using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Blog.Authorization;
using DM.Domain.Blog.Features.Blogs;
using BlogDto = DM.Domain.Blog.Features.Blogs.Blog;
using DM.Domain.Blog.Features.Comments;
using DM.Domain.Blog.Features.Likes;
using DM.Domain.Blog.Features.PublicationComments;
using DM.Domain.Blog.Features.Publications;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Likes;
using DM.Domain.Core.Users;
using DM.Domain.Account.Features.Authentication;
using DM.Testing;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Domain.Blog.Tests.Features.Likes;

public class BlogLikeServiceShould : UnitTestBase
{
    private readonly Mock<IBlogService> _blogService;
    private readonly Mock<IPublicationService> _publicationService;
    private readonly Mock<IBlogCommentService> _blogCommentService;
    private readonly Mock<IPublicationCommentService> _publicationCommentService;
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<ILikeOperations> _likeOperations;
    private readonly BlogLikeService _service;

    public BlogLikeServiceShould()
    {
        _blogService = Mock<IBlogService>();
        _publicationService = Mock<IPublicationService>();
        _blogCommentService = Mock<IBlogCommentService>();
        _publicationCommentService = Mock<IPublicationCommentService>();
        _intentionManager = Mock<IIntentionManager>();
        _identityProvider = Mock<IIdentityProvider>();
        _likeOperations = Mock<ILikeOperations>();

        _identityProvider.Setup(p => p.Current).Returns(Identity.Guest());

        _service = new BlogLikeService(
            _blogService.Object,
            _publicationService.Object,
            _blogCommentService.Object,
            _publicationCommentService.Object,
            _intentionManager.Object,
            _identityProvider.Object,
            _likeOperations.Object);
    }

    [Fact]
    public async Task AuthorizeLikeBlogCommentAction()
    {
        var commentId = Guid.NewGuid();
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var comment = new Comment { Id = commentId, EntityId = blogId };
        var blog = new BlogDto { Id = blogId, BlacklistedUserIds = new HashSet<Guid>() };
        var identity = CreateAuthenticatedIdentity(userId);

        _identityProvider.Setup(p => p.Current).Returns(identity);
        _blogCommentService.Setup(s => s.GetAsync(commentId)).ReturnsAsync(comment);
        _blogService.Setup(s => s.GetBlogAsync(blogId, default)).ReturnsAsync(blog);
        _likeOperations.Setup(o => o.LikeAsync(comment, EventType.LikedBlogComment))
            .ReturnsAsync(new GeneralUser { UserId = userId });

        await _service.LikeBlogCommentAsync(commentId);

        _intentionManager.Verify(m => m.ThrowIfForbidden(CommentIntention.Like, comment), Times.Once);
    }

    [Fact]
    public async Task ThrowWhenBlacklistedUserLikesBlogComment()
    {
        var commentId = Guid.NewGuid();
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var comment = new Comment { Id = commentId, EntityId = blogId };
        var blog = new BlogDto { Id = blogId, BlacklistedUserIds = new HashSet<Guid> { userId } };
        var identity = CreateAuthenticatedIdentity(userId);

        _identityProvider.Setup(p => p.Current).Returns(identity);
        _blogCommentService.Setup(s => s.GetAsync(commentId)).ReturnsAsync(comment);
        _blogService.Setup(s => s.GetBlogAsync(blogId, default)).ReturnsAsync(blog);

        var act = async () => await _service.LikeBlogCommentAsync(commentId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden && e.Message.Contains("черном списке"));
    }

    [Fact]
    public async Task AuthorizeLikePublicationAction()
    {
        var publicationId = Guid.NewGuid();
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var publication = new Publication { Id = publicationId, BlogId = blogId };
        var blog = new BlogDto { Id = blogId, BlacklistedUserIds = new HashSet<Guid>() };
        var identity = CreateAuthenticatedIdentity(userId);

        _identityProvider.Setup(p => p.Current).Returns(identity);
        _publicationService.Setup(s => s.GetPublication(publicationId, default)).ReturnsAsync(publication);
        _blogService.Setup(s => s.GetBlogAsync(blogId, default)).ReturnsAsync(blog);
        _likeOperations.Setup(o => o.LikeAsync(publication, EventType.LikedPublication))
            .ReturnsAsync(new GeneralUser { UserId = userId });

        await _service.LikePublicationAsync(publicationId);

        _intentionManager.Verify(m => m.ThrowIfForbidden(PublicationIntention.Like, publication), Times.Once);
    }

    [Fact]
    public async Task ThrowWhenBlacklistedUserLikesPublication()
    {
        var publicationId = Guid.NewGuid();
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var publication = new Publication { Id = publicationId, BlogId = blogId };
        var blog = new BlogDto { Id = blogId, BlacklistedUserIds = new HashSet<Guid> { userId } };
        var identity = CreateAuthenticatedIdentity(userId);

        _identityProvider.Setup(p => p.Current).Returns(identity);
        _publicationService.Setup(s => s.GetPublication(publicationId, default)).ReturnsAsync(publication);
        _blogService.Setup(s => s.GetBlogAsync(blogId, default)).ReturnsAsync(blog);

        var act = async () => await _service.LikePublicationAsync(publicationId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden && e.Message.Contains("черном списке"));
    }

    private static IIdentity CreateAuthenticatedIdentity(Guid userId)
    {
        var user = new AuthenticatedUser { UserId = userId, Username = "testuser" };
        var session = new Session();
        return Identity.Success(user, session, UserSettings.Default, "token");
    }
}
