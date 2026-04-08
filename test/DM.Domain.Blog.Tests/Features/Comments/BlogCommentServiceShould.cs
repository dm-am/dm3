using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Blog.Authorization;
using DM.Domain.Blog.Features.Blogs;
using BlogDto = DM.Domain.Blog.Features.Blogs.Blog;
using DM.Domain.Blog.Features.Comments;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Core.Users;
using DM.Domain.Account.Features.Authentication;
using DM.Testing;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Xunit;

namespace DM.Domain.Blog.Tests.Features.Comments;

public class BlogCommentServiceShould : UnitTestBase
{
    private readonly Mock<IValidator<CreateComment>> _createValidator;
    private readonly Mock<IValidator<UpdateComment>> _updateValidator;
    private readonly Mock<IBlogService> _blogService;
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly Mock<IBlogCommentRepository> _repository;
    private readonly Mock<IUnreadCountersRepository> _countersRepository;
    private readonly Mock<IEventProducer> _eventProducer;
    private readonly BlogCommentService _service;

    public BlogCommentServiceShould()
    {
        _createValidator = Mock<IValidator<CreateComment>>();
        _updateValidator = Mock<IValidator<UpdateComment>>();
        _blogService = Mock<IBlogService>();
        _intentionManager = Mock<IIntentionManager>();
        _identityProvider = Mock<IIdentityProvider>();
        _dateTimeProvider = Mock<IDateTimeProvider>();
        _repository = Mock<IBlogCommentRepository>();
        _countersRepository = Mock<IUnreadCountersRepository>();
        _eventProducer = Mock<IEventProducer>();

        _identityProvider.Setup(p => p.Current).Returns(Identity.Guest());
        _dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);

        _createValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreateComment>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _updateValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<UpdateComment>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _service = new BlogCommentService(
            _createValidator.Object,
            _updateValidator.Object,
            _blogService.Object,
            _intentionManager.Object,
            _identityProvider.Object,
            _dateTimeProvider.Object,
            _repository.Object,
            _countersRepository.Object,
            _eventProducer.Object);
    }

    [Fact]
    public async Task AuthorizeCreateCommentAction()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var blog = new BlogDto { Id = blogId, BlacklistedUserIds = new HashSet<Guid>() };
        var createComment = new CreateComment { EntityId = blogId, Text = "Test comment" };
        var identity = CreateAuthenticatedIdentity(userId);

        _identityProvider.Setup(p => p.Current).Returns(identity);
        _blogService.Setup(s => s.GetBlogAsync(blogId, default)).ReturnsAsync(blog);
        _repository.Setup(r => r.Create(It.IsAny<CreateComment>(), userId, blogId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new Comment { Id = commentId }, commentId));

        await _service.CreateAsync(createComment);

        _intentionManager.Verify(m => m.ThrowIfForbidden(BlogIntention.CreateComment, blog), Times.Once);
    }

    [Fact]
    public async Task ThrowWhenBlacklistedUserCreatesComment()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var blog = new BlogDto { Id = blogId, BlacklistedUserIds = new HashSet<Guid> { userId } };
        var createComment = new CreateComment { EntityId = blogId, Text = "Test comment" };
        var identity = CreateAuthenticatedIdentity(userId);

        _identityProvider.Setup(p => p.Current).Returns(identity);
        _blogService.Setup(s => s.GetBlogAsync(blogId, default)).ReturnsAsync(blog);

        var act = async () => await _service.CreateAsync(createComment);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden && e.Message.Contains("blacklisted"));
    }

    [Fact]
    public async Task PublishEventWhenCreatingComment()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var blog = new BlogDto { Id = blogId, BlacklistedUserIds = new HashSet<Guid>() };
        var createComment = new CreateComment { EntityId = blogId, Text = "Test comment" };
        var identity = CreateAuthenticatedIdentity(userId);

        _identityProvider.Setup(p => p.Current).Returns(identity);
        _blogService.Setup(s => s.GetBlogAsync(blogId, default)).ReturnsAsync(blog);
        _repository.Setup(r => r.Create(It.IsAny<CreateComment>(), userId, blogId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new Comment { Id = commentId }, commentId));

        await _service.CreateAsync(createComment);

        _eventProducer.Verify(p => p.SendAsync(EventType.NewBlogComment, commentId), Times.Once);
    }

    [Fact]
    public async Task ThrowWhenCommentNotFound()
    {
        var commentId = Guid.NewGuid();
        _repository.Setup(r => r.Get(commentId, default)).ReturnsAsync((Comment?)null);

        var act = async () => await _service.GetAsync(commentId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AuthorizeUpdateCommentAction()
    {
        var commentId = Guid.NewGuid();
        var comment = new Comment { Id = commentId, Text = "Original text" };
        var updateComment = new UpdateComment { CommentId = commentId, Text = "Updated text" };

        _repository.Setup(r => r.Get(commentId, default)).ReturnsAsync(comment);
        _repository.Setup(r => r.Update(It.IsAny<UpdateBlogCommentEntity>(), default)).ReturnsAsync(comment);

        await _service.UpdateAsync(updateComment);

        _intentionManager.Verify(m => m.ThrowIfForbidden(CommentIntention.Edit, comment), Times.Once);
    }

    [Fact]
    public async Task NotUpdateWhenTextUnchanged()
    {
        var commentId = Guid.NewGuid();
        var comment = new Comment { Id = commentId, Text = "Same text" };
        var updateComment = new UpdateComment { CommentId = commentId, Text = "Same text" };

        _repository.Setup(r => r.Get(commentId, default)).ReturnsAsync(comment);

        var result = await _service.UpdateAsync(updateComment);

        result.Should().Be(comment);
        _repository.Verify(r => r.Update(It.IsAny<UpdateBlogCommentEntity>(), default), Times.Never);
    }

    [Fact]
    public async Task AuthorizeDeleteCommentAction()
    {
        var commentId = Guid.NewGuid();
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var comment = new BlogCommentToDelete
        {
            Id = commentId,
            EntityId = blogId,
            IsLastComment = false,
            BlogCommentCount = 5,
            CreatedUtc = DateTimeOffset.UtcNow
        };
        var identity = CreateAuthenticatedIdentity(userId);

        _identityProvider.Setup(p => p.Current).Returns(identity);
        _repository.Setup(r => r.GetForDelete(commentId, default)).ReturnsAsync(comment);

        await _service.DeleteAsync(commentId);

        _intentionManager.Verify(m => m.ThrowIfForbidden(CommentIntention.Delete, It.IsAny<Comment>()), Times.Once);
    }

    [Fact]
    public async Task ThrowWhenDeletingNonExistentComment()
    {
        var commentId = Guid.NewGuid();
        _repository.Setup(r => r.GetForDelete(commentId, default)).ReturnsAsync((BlogCommentToDelete?)null);

        var act = async () => await _service.DeleteAsync(commentId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    private static IIdentity CreateAuthenticatedIdentity(Guid userId)
    {
        var user = new AuthenticatedUser { UserId = userId, Username = "testuser" };
        var session = new Session();
        return Identity.Success(user, session, UserSettings.Default, "token");
    }
}
