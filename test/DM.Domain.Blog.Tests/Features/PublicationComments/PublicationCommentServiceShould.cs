using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Blog.Authorization;
using DM.Domain.Blog.Features.Blogs;
using BlogDto = DM.Domain.Blog.Features.Blogs.Blog;
using DM.Domain.Blog.Features.PublicationComments;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;
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

namespace DM.Domain.Blog.Tests.Features.PublicationComments;

public class PublicationCommentServiceShould : UnitTestBase
{
    private readonly Mock<IValidator<CreateComment>> _createValidator;
    private readonly Mock<IValidator<UpdateComment>> _updateValidator;
    private readonly Mock<IBlogService> _blogService;
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly Mock<IPublicationCommentRepository> _repository;
    private readonly Mock<IUnreadCountersRepository> _countersRepository;
    private readonly Mock<IEventProducer> _eventProducer;
    private readonly PublicationCommentService _service;

    public PublicationCommentServiceShould()
    {
        _createValidator = Mock<IValidator<CreateComment>>();
        _updateValidator = Mock<IValidator<UpdateComment>>();
        _blogService = Mock<IBlogService>();
        _intentionManager = Mock<IIntentionManager>();
        _identityProvider = Mock<IIdentityProvider>();
        _dateTimeProvider = Mock<IDateTimeProvider>();
        _repository = Mock<IPublicationCommentRepository>();
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

        _service = new PublicationCommentService(
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
        var publicationId = Guid.NewGuid();
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var publication = new Publication { Id = publicationId, BlogId = blogId };
        var blog = new BlogDto { Id = blogId, Author = new GeneralUser { UserId = Guid.NewGuid() }, Assistants = Array.Empty<BlogAssistantInfo>(), BlacklistedUserIds = new HashSet<Guid>() };
        var createComment = new CreateComment { EntityId = publicationId, Text = "Test comment" };
        var identity = CreateAuthenticatedIdentity(userId);

        _identityProvider.Setup(p => p.Current).Returns(identity);
        _blogService.Setup(s => s.GetPublication(publicationId, default)).ReturnsAsync(publication);
        _blogService.Setup(s => s.GetBlogAsync(blogId, default)).ReturnsAsync(blog);
        _repository.Setup(r => r.Create(It.IsAny<CreateComment>(), userId, publicationId, It.IsAny<int>(), default))
            .ReturnsAsync((new Comment { Id = commentId }, commentId));

        await _service.CreateAsync(createComment);

        _intentionManager.Verify(m => m.ThrowIfForbidden(PublicationIntention.CreateComment, publication), Times.Once);
    }

    [Fact]
    public async Task ThrowWhenBlacklistedUserCreatesComment()
    {
        var publicationId = Guid.NewGuid();
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var publication = new Publication { Id = publicationId, BlogId = blogId };
        var blog = new BlogDto { Id = blogId, Author = new GeneralUser { UserId = Guid.NewGuid() }, Assistants = Array.Empty<BlogAssistantInfo>(), BlacklistedUserIds = new HashSet<Guid> { userId } };
        var createComment = new CreateComment { EntityId = publicationId, Text = "Test comment" };
        var identity = CreateAuthenticatedIdentity(userId);

        _identityProvider.Setup(p => p.Current).Returns(identity);
        _blogService.Setup(s => s.GetPublication(publicationId, default)).ReturnsAsync(publication);
        _blogService.Setup(s => s.GetBlogAsync(blogId, default)).ReturnsAsync(blog);

        var act = async () => await _service.CreateAsync(createComment);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden && e.Message.Contains("черном списке"));
    }

    [Fact]
    public async Task PublishEventWhenCreatingComment()
    {
        var publicationId = Guid.NewGuid();
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var publication = new Publication { Id = publicationId, BlogId = blogId };
        var blog = new BlogDto { Id = blogId, Author = new GeneralUser { UserId = Guid.NewGuid() }, Assistants = Array.Empty<BlogAssistantInfo>(), BlacklistedUserIds = new HashSet<Guid>() };
        var createComment = new CreateComment { EntityId = publicationId, Text = "Test comment" };
        var identity = CreateAuthenticatedIdentity(userId);

        _identityProvider.Setup(p => p.Current).Returns(identity);
        _blogService.Setup(s => s.GetPublication(publicationId, default)).ReturnsAsync(publication);
        _blogService.Setup(s => s.GetBlogAsync(blogId, default)).ReturnsAsync(blog);
        _repository.Setup(r => r.Create(It.IsAny<CreateComment>(), userId, publicationId, It.IsAny<int>(), default))
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
        _repository.Setup(r => r.Update(It.IsAny<UpdatePublicationCommentEntity>(), default)).ReturnsAsync(comment);

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
        _repository.Verify(r => r.Update(It.IsAny<UpdatePublicationCommentEntity>(), default), Times.Never);
    }

    private static IIdentity CreateAuthenticatedIdentity(Guid userId)
    {
        var user = new AuthenticatedUser { UserId = userId, Username = "testuser" };
        var session = new Session();
        return Identity.Success(user, session, UserSettings.Default, "token");
    }
}
