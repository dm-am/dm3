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
using DM.Testing;
using DM.Domain.Account.Features.Authentication;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Blog.Tests.Features.Likes;

public class BlogLikeServiceShould : UnitTestBase
{
    private readonly IBlogService _blogService;
    private readonly IPublicationService _publicationService;
    private readonly IBlogCommentService _blogCommentService;
    private readonly IPublicationCommentService _publicationCommentService;
    private readonly IIntentionManager _intentionManager;
    private readonly IIdentityProvider _identityProvider;
    private readonly ILikeOperations _likeOperations;
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

        _identityProvider.Current.Returns(Identity.Guest());

        _service = new BlogLikeService(
            _blogService,
            _publicationService,
            _blogCommentService,
            _publicationCommentService,
            _intentionManager,
            _identityProvider,
            _likeOperations);
    }

    [Fact]
    public async Task AuthorizeLikeBlogCommentAction()
    {
        var commentId = Guid.NewGuid();
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var comment = new Comment { Id = commentId, EntityId = blogId };
        var blog = new BlogDto { Id = blogId, BlacklistedUserIds = new HashSet<Guid>() };
        var identity = AuthenticatedIdentities.Of(userId);

        _identityProvider.Current.Returns(identity);
        _blogCommentService.GetAsync(commentId).Returns(comment);
        _blogService.GetBlogAsync(blogId, default).Returns(blog);
        _likeOperations.LikeAsync(comment, EventType.LikedBlogComment).Returns(new GeneralUser { UserId = userId });

        await _service.LikeBlogCommentAsync(commentId);

        _intentionManager.Received(1).ThrowIfForbidden(CommentIntention.Like, comment);
    }

    [Fact]
    public async Task ThrowWhenBlacklistedUserLikesBlogComment()
    {
        var commentId = Guid.NewGuid();
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var comment = new Comment { Id = commentId, EntityId = blogId };
        var blog = new BlogDto { Id = blogId, BlacklistedUserIds = new HashSet<Guid> { userId } };
        var identity = AuthenticatedIdentities.Of(userId);

        _identityProvider.Current.Returns(identity);
        _blogCommentService.GetAsync(commentId).Returns(comment);
        _blogService.GetBlogAsync(blogId, default).Returns(blog);

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
        var identity = AuthenticatedIdentities.Of(userId);

        _identityProvider.Current.Returns(identity);
        _publicationService.GetPublication(publicationId, default).Returns(publication);
        _blogService.GetBlogAsync(blogId, default).Returns(blog);
        _likeOperations.LikeAsync(publication, EventType.LikedPublication).Returns(new GeneralUser { UserId = userId });

        await _service.LikePublicationAsync(publicationId);

        _intentionManager.Received(1).ThrowIfForbidden(PublicationIntention.Like, publication);
    }

    [Fact]
    public async Task ThrowWhenBlacklistedUserLikesPublication()
    {
        var publicationId = Guid.NewGuid();
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var publication = new Publication { Id = publicationId, BlogId = blogId };
        var blog = new BlogDto { Id = blogId, BlacklistedUserIds = new HashSet<Guid> { userId } };
        var identity = AuthenticatedIdentities.Of(userId);

        _identityProvider.Current.Returns(identity);
        _publicationService.GetPublication(publicationId, default).Returns(publication);
        _blogService.GetBlogAsync(blogId, default).Returns(blog);

        var act = async () => await _service.LikePublicationAsync(publicationId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden && e.Message.Contains("черном списке"));
    }

}
