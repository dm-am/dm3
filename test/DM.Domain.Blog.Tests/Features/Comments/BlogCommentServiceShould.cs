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
using DM.Testing;
using DM.Domain.Account.Features.Authentication;
using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using Xunit;

namespace DM.Domain.Blog.Tests.Features.Comments;

public class BlogCommentServiceShould : UnitTestBase
{
    private readonly IValidator<CreateComment> _createValidator;
    private readonly IValidator<UpdateComment> _updateValidator;
    private readonly IBlogService _blogService;
    private readonly IIntentionManager _intentionManager;
    private readonly IIdentityProvider _identityProvider;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IBlogCommentRepository _repository;
    private readonly IUnreadCountersRepository _countersRepository;
    private readonly IEventProducer _eventProducer;
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

        _identityProvider.Current.Returns(Identity.Guest());
        _dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        _createValidator
            .ValidateAsync(Arg.Any<ValidationContext<CreateComment>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
        _updateValidator
            .ValidateAsync(Arg.Any<ValidationContext<UpdateComment>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        _service = new BlogCommentService(
            _createValidator,
            _updateValidator,
            _blogService,
            _intentionManager,
            _identityProvider,
            _dateTimeProvider,
            _repository,
            _countersRepository,
            _eventProducer);
    }

    [Fact]
    public async Task AuthorizeCreateCommentAction()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var blog = new BlogDto { Id = blogId, BlacklistedUserIds = new HashSet<Guid>() };
        var createComment = new CreateComment { EntityId = blogId, Text = "Test comment" };
        var identity = AuthenticatedIdentities.Of(userId);

        _identityProvider.Current.Returns(identity);
        _blogService.GetBlogAsync(blogId, default).Returns(blog);
        _repository.Create(Arg.Any<CreateComment>(), userId, blogId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((new Comment { Id = commentId }, commentId));

        await _service.CreateAsync(createComment);

        _intentionManager.Received(1).ThrowIfForbidden(BlogIntention.CreateComment, blog);
    }

    [Fact]
    public async Task PublishEventWhenCreatingComment()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var blog = new BlogDto { Id = blogId, BlacklistedUserIds = new HashSet<Guid>() };
        var createComment = new CreateComment { EntityId = blogId, Text = "Test comment" };
        var identity = AuthenticatedIdentities.Of(userId);

        _identityProvider.Current.Returns(identity);
        _blogService.GetBlogAsync(blogId, default).Returns(blog);
        _repository.Create(Arg.Any<CreateComment>(), userId, blogId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((new Comment { Id = commentId }, commentId));

        await _service.CreateAsync(createComment);

        await _eventProducer.Received(1).SendAsync(EventType.NewBlogComment, commentId);
    }

    [Fact]
    public async Task ThrowWhenCommentNotFound()
    {
        var commentId = Guid.NewGuid();
        _repository.Get(commentId, default).Returns((Comment?)null);

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

        _repository.Get(commentId, default).Returns(comment);
        _repository.Update(Arg.Any<UpdateBlogCommentEntity>(), default).Returns(comment);

        await _service.UpdateAsync(updateComment);

        _intentionManager.Received(1).ThrowIfForbidden(CommentIntention.Edit, comment);
    }

    [Fact]
    public async Task NotUpdateWhenTextUnchanged()
    {
        var commentId = Guid.NewGuid();
        var comment = new Comment { Id = commentId, Text = "Same text" };
        var updateComment = new UpdateComment { CommentId = commentId, Text = "Same text" };

        _repository.Get(commentId, default).Returns(comment);

        var result = await _service.UpdateAsync(updateComment);

        result.Should().Be(comment);
        await _repository.DidNotReceive().Update(
            Arg.Any<UpdateBlogCommentEntity>(), Arg.Any<CancellationToken>());
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
        var identity = AuthenticatedIdentities.Of(userId);

        _identityProvider.Current.Returns(identity);
        _repository.GetForDelete(commentId, default).Returns(comment);

        await _service.DeleteAsync(commentId);

        _intentionManager.Received(1).ThrowIfForbidden(CommentIntention.Delete, Arg.Any<Comment>());
    }

    [Fact]
    public async Task ThrowWhenDeletingNonExistentComment()
    {
        var commentId = Guid.NewGuid();
        _repository.GetForDelete(commentId, default).Returns((BlogCommentToDelete?)null);

        var act = async () => await _service.DeleteAsync(commentId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

}
