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
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Core.Users;
using DM.Testing;
using DM.Testing.Markup;
using DM.Domain.Account.Features.Authentication;
using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
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
            _eventProducer,
            new TagNamesAsTyped());
    }

    /// <summary>
    /// The gate carries the same rule and would refuse in its generic words; the
    /// list is asked first, so the refusal says why.
    /// </summary>
    [Fact]
    public async Task RefuseABlacklistedUserInTheWordsOfTheList()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var blog = new BlogDto { Id = blogId, BlacklistedUserIds = new HashSet<Guid> { userId } };
        _identityProvider.Current.Returns(AuthenticatedIdentities.Of(userId));
        _blogService.GetAsync(blogId, default).Returns(blog);
        _intentionManager
            .When(manager => manager.ThrowIfForbidden(BlogIntention.CreateComment, Arg.Any<BlogDto>()))
            .Do(_ => throw new HttpException(HttpStatusCode.Forbidden, "generic"));

        var act = async () => await _service.CreateAsync(new CreateComment { EntityId = blogId, Text = "Test comment" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden && e.Message == RefusalMessage.BlacklistedFromBlog);
    }

    /// <summary>
    /// Rewriting one's comment publishes text exactly as writing it did.
    /// </summary>
    [Fact]
    public async Task RefuseABlacklistedAuthorTheEditOfHisComment()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var comment = new Comment
        {
            Id = Guid.NewGuid(),
            EntityId = blogId,
            Text = "Original text",
            Author = new GeneralUser { UserId = userId }
        };
        _identityProvider.Current.Returns(AuthenticatedIdentities.Of(userId));
        _repository.Get(comment.Id, default).Returns(comment);
        _blogService.GetBlogAsync(blogId, default)
            .Returns(new BlogDto { Id = blogId, BlacklistedUserIds = new HashSet<Guid> { userId } });

        var act = async () => await _service.UpdateAsync(new UpdateComment { CommentId = comment.Id, Text = "Updated" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden && e.Message == RefusalMessage.BlacklistedFromBlog);
        await _repository.DidNotReceive().Update(Arg.Any<UpdateBlogCommentEntity>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A blog the user may not open takes no comment, and answers as a blog that
    /// is not there: the visible read refuses before anything is asked of it.
    /// </summary>
    [Fact]
    public async Task RefuseACommentInABlogTheUserMayNotSeeAsNoBlogAtAll()
    {
        var blogId = Guid.NewGuid();
        _identityProvider.Current.Returns(AuthenticatedIdentities.Of(Guid.NewGuid()));
        _blogService.GetAsync(blogId, default)
            .ThrowsAsync(new HttpException(HttpStatusCode.NotFound, RefusalMessage.BlogNotFound));
        _blogService.GetBlogAsync(blogId, default)
            .Returns(new BlogDto { Id = blogId, BlacklistedUserIds = new HashSet<Guid>() });

        var act = async () => await _service.CreateAsync(new CreateComment { EntityId = blogId, Text = "Test comment" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound && e.Message == RefusalMessage.BlogNotFound);
        await _repository.DidNotReceive().Create(
            Arg.Any<CreateComment>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
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
        _blogService.GetAsync(blogId, default).Returns(blog);
        _repository.Create(Arg.Any<CreateComment>(), userId, blogId, Arg.Any<CancellationToken>())
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
        _blogService.GetAsync(blogId, default).Returns(blog);
        _repository.Create(Arg.Any<CreateComment>(), userId, blogId, Arg.Any<CancellationToken>())
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

    /// <summary>
    /// The removal that went through pays the unread counters and announces itself.
    /// </summary>
    [Fact]
    public async Task PayTheCountersForTheRemovalThatWentThrough()
    {
        var commentId = Guid.NewGuid();
        var blogId = Guid.NewGuid();
        var createdUtc = DateTimeOffset.UtcNow.AddHours(-2);
        var comment = new BlogCommentToDelete
        {
            Id = commentId,
            EntityId = blogId,
            CreatedUtc = createdUtc
        };

        _identityProvider.Current.Returns(AuthenticatedIdentities.Of(Guid.NewGuid()));
        _repository.GetForDelete(commentId, default).Returns(comment);
        _repository.Delete(Arg.Any<DeleteBlogCommentEntity>(), default).Returns(true);

        await _service.DeleteAsync(commentId);

        await _countersRepository.Received(1).DecrementAsync(
            blogId, UnreadEntryType.Message, createdUtc);
        await _eventProducer.Received(1).SendAsync(EventType.DeletedBlogComment, commentId);
    }

    /// <summary>
    /// A removal that found the comment already gone pays nothing.
    /// </summary>
    /// <remarks>
    /// The author and a moderator can both press delete, and the author can press
    /// it twice on a slow answer. The unread counter and the event belong to the
    /// removal that happened: repeating them here takes the counter down twice for
    /// one comment, and nothing recomputes it.
    /// </remarks>
    [Fact]
    public async Task LeaveTheCountersToTheRemovalThatTookTheComment()
    {
        var commentId = Guid.NewGuid();
        var comment = new BlogCommentToDelete
        {
            Id = commentId,
            EntityId = Guid.NewGuid(),
            CreatedUtc = DateTimeOffset.UtcNow
        };

        _identityProvider.Current.Returns(AuthenticatedIdentities.Of(Guid.NewGuid()));
        _repository.GetForDelete(commentId, default).Returns(comment);
        _repository.Delete(Arg.Any<DeleteBlogCommentEntity>(), default).Returns(false);

        await _service.DeleteAsync(commentId);

        await _countersRepository.DidNotReceive().DecrementAsync(
            Arg.Any<Guid>(), Arg.Any<UnreadEntryType>(), Arg.Any<DateTimeOffset>());
        await _eventProducer.DidNotReceive().SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>());
    }

}
