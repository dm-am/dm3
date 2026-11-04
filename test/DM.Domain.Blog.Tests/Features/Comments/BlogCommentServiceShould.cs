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
using DM.Testing;
using DM.Testing.Dsl;
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
    private readonly IValidator<CreateComment> createValidator;
    private readonly IValidator<UpdateComment> updateValidator;
    private readonly IBlogService blogService;
    private readonly IIntentionManager intentionManager;
    private readonly IIdentityProvider identityProvider;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IBlogCommentRepository repository;
    private readonly IUnreadCountersRepository countersRepository;
    private readonly IEventProducer eventProducer;
    private readonly BlogCommentService service;

    public BlogCommentServiceShould()
    {
        createValidator = Mock<IValidator<CreateComment>>();
        updateValidator = Mock<IValidator<UpdateComment>>();
        blogService = Mock<IBlogService>();
        intentionManager = Mock<IIntentionManager>();
        identityProvider = Mock<IIdentityProvider>();
        dateTimeProvider = Mock<IDateTimeProvider>();
        repository = Mock<IBlogCommentRepository>();
        countersRepository = Mock<IUnreadCountersRepository>();
        eventProducer = Mock<IEventProducer>();

        identityProvider.Current.Returns(Identity.Guest());
        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        createValidator
            .ValidateAsync(Arg.Any<ValidationContext<CreateComment>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
        updateValidator
            .ValidateAsync(Arg.Any<ValidationContext<UpdateComment>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        service = new BlogCommentService(
            createValidator,
            updateValidator,
            blogService,
            intentionManager,
            identityProvider,
            dateTimeProvider,
            repository,
            countersRepository,
            eventProducer);
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
        identityProvider.Current.Returns(Identities.User(userId, "testuser"));
        blogService.GetAsync(blogId, default).Returns(blog);
        intentionManager
            .When(manager => manager.ThrowIfForbidden(BlogIntention.CreateComment, Arg.Any<BlogDto>()))
            .Do(_ => throw new HttpException(HttpStatusCode.Forbidden, "generic"));

        var act = async () => await service.CreateAsync(new CreateComment { EntityId = blogId, Text = "Test comment" });

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
        identityProvider.Current.Returns(Identities.User(userId, "testuser"));
        repository.Get(comment.Id, default).Returns(comment);
        blogService.GetUnfiltered(blogId, default)
            .Returns(new BlogDto { Id = blogId, BlacklistedUserIds = new HashSet<Guid> { userId } });

        var act = async () => await service.UpdateAsync(new UpdateComment { CommentId = comment.Id, Text = "Updated" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden && e.Message == RefusalMessage.BlacklistedFromBlog);
        await repository.DidNotReceive().Update(Arg.Any<UpdateBlogCommentEntity>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A blog the user may not open takes no comment, and answers as a blog that
    /// is not there: the visible read refuses before anything is asked of it.
    /// </summary>
    [Fact]
    public async Task RefuseACommentInABlogTheUserMayNotSeeAsNoBlogAtAll()
    {
        var blogId = Guid.NewGuid();
        identityProvider.Current.Returns(Identities.User(Guid.NewGuid(), "testuser"));
        blogService.GetAsync(blogId, default)
            .ThrowsAsync(new HttpException(HttpStatusCode.NotFound, RefusalMessage.BlogNotFound));
        blogService.GetUnfiltered(blogId, default)
            .Returns(new BlogDto { Id = blogId, BlacklistedUserIds = new HashSet<Guid>() });

        var act = async () => await service.CreateAsync(new CreateComment { EntityId = blogId, Text = "Test comment" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound && e.Message == RefusalMessage.BlogNotFound);
        await repository.DidNotReceive().Create(
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
        var identity = Identities.User(userId, "testuser");

        identityProvider.Current.Returns(identity);
        blogService.GetAsync(blogId, default).Returns(blog);
        repository.Create(Arg.Any<CreateComment>(), userId, blogId, Arg.Any<CancellationToken>())
            .Returns((new Comment { Id = commentId }, commentId));

        await service.CreateAsync(createComment);

        intentionManager.Received(1).ThrowIfForbidden(BlogIntention.CreateComment, blog);
    }

    /// <summary>The text is stored as typed, the spaces around it included (BBCODE_RENDERING.md, "Регистр имен").</summary>
    [Fact]
    public async Task StoreTheTextAsTyped()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        identityProvider.Current.Returns(Identities.User(userId, "testuser"));
        blogService.GetAsync(blogId, default)
            .Returns(new BlogDto { Id = blogId, BlacklistedUserIds = new HashSet<Guid>() });
        repository.Create(Arg.Any<CreateComment>(), userId, blogId, Arg.Any<CancellationToken>())
            .Returns((new Comment { Id = commentId }, commentId));

        await service.CreateAsync(new CreateComment { EntityId = blogId, Text = "  \n Test comment \n " });

        await repository.Received(1).Create(
            Arg.Is<CreateComment>(c => c.Text == "  \n Test comment \n "), userId, blogId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishEventWhenCreatingComment()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var blog = new BlogDto { Id = blogId, BlacklistedUserIds = new HashSet<Guid>() };
        var createComment = new CreateComment { EntityId = blogId, Text = "Test comment" };
        var identity = Identities.User(userId, "testuser");

        identityProvider.Current.Returns(identity);
        blogService.GetAsync(blogId, default).Returns(blog);
        repository.Create(Arg.Any<CreateComment>(), userId, blogId, Arg.Any<CancellationToken>())
            .Returns((new Comment { Id = commentId }, commentId));

        await service.CreateAsync(createComment);

        await eventProducer.Received(1).SendAsync(EventType.NewBlogComment, commentId);
    }

    [Fact]
    public async Task ThrowWhenCommentNotFound()
    {
        var commentId = Guid.NewGuid();
        repository.Get(commentId, default).Returns((Comment?)null);

        var act = async () => await service.GetAsync(commentId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AuthorizeUpdateCommentAction()
    {
        var commentId = Guid.NewGuid();
        var comment = new Comment { Id = commentId, Text = "Original text" };
        var updateComment = new UpdateComment { CommentId = commentId, Text = "Updated text" };

        repository.Get(commentId, default).Returns(comment);
        repository.Update(Arg.Any<UpdateBlogCommentEntity>(), default).Returns(comment);

        await service.UpdateAsync(updateComment);

        intentionManager.Received(1).ThrowIfForbidden(CommentIntention.Edit, comment);
    }

    [Fact]
    public async Task NotUpdateWhenTextUnchanged()
    {
        var commentId = Guid.NewGuid();
        var comment = new Comment { Id = commentId, Text = "Same text" };
        var updateComment = new UpdateComment { CommentId = commentId, Text = "Same text" };

        repository.Get(commentId, default).Returns(comment);

        var result = await service.UpdateAsync(updateComment);

        result.Should().Be(comment);
        await repository.DidNotReceive().Update(
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
        var identity = Identities.User(userId, "testuser");

        identityProvider.Current.Returns(identity);
        repository.GetForDelete(commentId, default).Returns(comment);

        await service.DeleteAsync(commentId);

        intentionManager.Received(1).ThrowIfForbidden(CommentIntention.Delete, Arg.Any<Comment>());
    }

    [Fact]
    public async Task ThrowWhenDeletingNonExistentComment()
    {
        var commentId = Guid.NewGuid();
        repository.GetForDelete(commentId, default).Returns((BlogCommentToDelete?)null);

        var act = async () => await service.DeleteAsync(commentId);

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

        identityProvider.Current.Returns(Identities.User(Guid.NewGuid(), "testuser"));
        repository.GetForDelete(commentId, default).Returns(comment);
        repository.Delete(Arg.Any<DeleteBlogCommentEntity>(), default).Returns(true);

        await service.DeleteAsync(commentId);

        await countersRepository.Received(1).DecrementAsync(
            blogId, UnreadEntryType.Message, createdUtc);
        await eventProducer.Received(1).SendAsync(EventType.DeletedBlogComment, commentId);
    }

    /// <summary>
    /// A removal that found the comment already gone pays nothing.
    /// </summary>
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

        identityProvider.Current.Returns(Identities.User(Guid.NewGuid(), "testuser"));
        repository.GetForDelete(commentId, default).Returns(comment);
        repository.Delete(Arg.Any<DeleteBlogCommentEntity>(), default).Returns(false);

        await service.DeleteAsync(commentId);

        await countersRepository.DidNotReceive().DecrementAsync(
            Arg.Any<Guid>(), Arg.Any<UnreadEntryType>(), Arg.Any<DateTimeOffset>());
        await eventProducer.DidNotReceive().SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>());
    }
}
