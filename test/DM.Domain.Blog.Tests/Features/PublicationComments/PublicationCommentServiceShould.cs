using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Blog.Authorization;
using DM.Domain.Blog.Features.Blogs;
using BlogDto = DM.Domain.Blog.Features.Blogs.Blog;
using DM.Domain.Blog.Features.PublicationComments;
using DM.Domain.Blog.Features.Publications;
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
using Xunit;

namespace DM.Domain.Blog.Tests.Features.PublicationComments;

public class PublicationCommentServiceShould : UnitTestBase
{
    private readonly IValidator<CreateComment> createValidator;
    private readonly IValidator<UpdateComment> updateValidator;
    private readonly IBlogService blogService;
    private readonly IPublicationService publicationService;
    private readonly IIntentionManager intentionManager;
    private readonly IIdentityProvider identityProvider;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IPublicationCommentRepository repository;
    private readonly IUnreadCountersRepository countersRepository;
    private readonly IEventProducer eventProducer;
    private readonly PublicationCommentService service;

    public PublicationCommentServiceShould()
    {
        createValidator = Mock<IValidator<CreateComment>>();
        updateValidator = Mock<IValidator<UpdateComment>>();
        blogService = Mock<IBlogService>();
        publicationService = Mock<IPublicationService>();
        intentionManager = Mock<IIntentionManager>();
        identityProvider = Mock<IIdentityProvider>();
        dateTimeProvider = Mock<IDateTimeProvider>();
        repository = Mock<IPublicationCommentRepository>();
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

        service = new PublicationCommentService(
            createValidator,
            updateValidator,
            blogService,
            publicationService,
            intentionManager,
            identityProvider,
            dateTimeProvider,
            repository,
            countersRepository,
            eventProducer);
    }

    [Fact]
    public async Task AuthorizeCreateCommentAction()
    {
        var publicationId = Guid.NewGuid();
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var publication = new Publication { Id = publicationId, BlogId = blogId };
        var blog = OpenBlog(blogId);
        var createComment = new CreateComment { EntityId = publicationId, Text = "Test comment" };
        var identity = Identities.User(userId, "testuser");

        identityProvider.Current.Returns(identity);
        publicationService.GetPublication(publicationId, default).Returns(publication);
        blogService.GetUnfiltered(blogId, default).Returns(blog);
        repository.Create(Arg.Any<CreateComment>(), userId, publicationId, default)
            .Returns((new Comment { Id = commentId }, commentId));

        await service.CreateAsync(createComment);

        intentionManager.Received(1).ThrowIfForbidden(PublicationIntention.CreateComment, publication);
    }

    [Fact]
    public async Task ThrowWhenBlacklistedUserCreatesComment()
    {
        var publicationId = Guid.NewGuid();
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var publication = new Publication { Id = publicationId, BlogId = blogId };
        var blog = OpenBlog(blogId, userId);
        var createComment = new CreateComment { EntityId = publicationId, Text = "Test comment" };
        var identity = Identities.User(userId, "testuser");

        identityProvider.Current.Returns(identity);
        publicationService.GetPublication(publicationId, default).Returns(publication);
        blogService.GetUnfiltered(blogId, default).Returns(blog);

        var act = async () => await service.CreateAsync(createComment);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden && e.Message == RefusalMessage.BlacklistedFromBlog);
    }

    // Rewriting one's comment under a publication is writing in the blog again.
    [Fact]
    public async Task RefuseABlacklistedAuthorTheEditOfHisComment()
    {
        var publicationId = Guid.NewGuid();
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var comment = new Comment
        {
            Id = Guid.NewGuid(),
            EntityId = publicationId,
            Text = "Original text",
            Author = new GeneralUser { UserId = userId }
        };
        identityProvider.Current.Returns(Identities.User(userId, "testuser"));
        repository.Get(comment.Id, default).Returns(comment);
        publicationService.GetPublication(publicationId, default)
            .Returns(new Publication { Id = publicationId, BlogId = blogId });
        blogService.GetUnfiltered(blogId, default).Returns(new BlogDto
        {
            Id = blogId,
            Author = new GeneralUser { UserId = Guid.NewGuid() },
            Assistants = Array.Empty<BlogAssistantInfo>(),
            BlacklistedUserIds = new HashSet<Guid> { userId }
        });

        var act = async () => await service.UpdateAsync(new UpdateComment { CommentId = comment.Id, Text = "Updated" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden && e.Message == RefusalMessage.BlacklistedFromBlog);
        await repository.DidNotReceive().Update(Arg.Any<UpdatePublicationCommentEntity>(), Arg.Any<CancellationToken>());
    }

    // The text is stored as typed, the spaces around it included (BBCODE_RENDERING.md, "Регистр
    // имен").
    [Fact]
    public async Task StoreTheTextAsTyped()
    {
        var publicationId = Guid.NewGuid();
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        identityProvider.Current.Returns(Identities.User(userId, "testuser"));
        publicationService.GetPublication(publicationId, default)
            .Returns(new Publication { Id = publicationId, BlogId = blogId });
        blogService.GetUnfiltered(blogId, default).Returns(OpenBlog(blogId));
        repository.Create(Arg.Any<CreateComment>(), userId, publicationId, default)
            .Returns((new Comment { Id = commentId }, commentId));

        await service.CreateAsync(new CreateComment { EntityId = publicationId, Text = "  \n Test comment \n " });

        await repository.Received(1).Create(
            Arg.Is<CreateComment>(c => c.Text == "  \n Test comment \n "), userId, publicationId, default);
    }

    [Fact]
    public async Task PublishEventWhenCreatingComment()
    {
        var publicationId = Guid.NewGuid();
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var publication = new Publication { Id = publicationId, BlogId = blogId };
        var blog = OpenBlog(blogId);
        var createComment = new CreateComment { EntityId = publicationId, Text = "Test comment" };
        var identity = Identities.User(userId, "testuser");

        identityProvider.Current.Returns(identity);
        publicationService.GetPublication(publicationId, default).Returns(publication);
        blogService.GetUnfiltered(blogId, default).Returns(blog);
        repository.Create(Arg.Any<CreateComment>(), userId, publicationId, default)
            .Returns((new Comment { Id = commentId }, commentId));

        await service.CreateAsync(createComment);

        // The publication event, not the blog one: a comment carries the id of
        // whatever it hangs on, so the blog generator joined Comments to Blogs on
        // a publication id and produced nothing at all.
        await eventProducer.Received(1).SendAsync(EventType.NewPublicationComment, commentId);
        await eventProducer.DidNotReceive().SendAsync(EventType.NewBlogComment, Arg.Any<Guid>());
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
        repository.Update(Arg.Any<UpdatePublicationCommentEntity>(), default).Returns(comment);

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
            Arg.Any<UpdatePublicationCommentEntity>(), Arg.Any<CancellationToken>());
    }

    // The count and the last-comment pointer are the database's, see DiscussionCountersShould and
    // LastCommentPointerShould.
    [Fact]
    public async Task PayTheCountersForTheRemovalThatWentThrough()
    {
        var commentId = Guid.NewGuid();
        var publicationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var createdUtc = DateTimeOffset.UtcNow.AddHours(-2);

        var comment = new PublicationCommentToDelete
        {
            Id = commentId,
            EntityId = publicationId,
            CreatedUtc = createdUtc
        };

        identityProvider.Current.Returns(Identities.User(userId, "testuser"));
        repository.GetForDelete(commentId, default).Returns(comment);

        DeletePublicationCommentEntity? deleted = null;
        repository
            .Delete(Arg.Do<DeletePublicationCommentEntity>(entity => deleted = entity), default)
            .Returns(true);

        await service.DeleteAsync(commentId);

        intentionManager.Received(1).ThrowIfForbidden(CommentIntention.Delete, Arg.Any<Comment>());

        deleted.Should().NotBeNull("the deletion is what the repository is handed");
        deleted!.DeletedByUserId.Should().Be(userId,
            "who removed it is the one thing the row cannot recover afterwards");
        await countersRepository.Received(1).DecrementAsync(
            publicationId, UnreadEntryType.Message, createdUtc);
        await eventProducer.Received(1).SendAsync(EventType.DeletedPublicationComment, commentId);
    }

    /// <summary>
    /// A removal that found the comment already gone pays nothing.
    /// </summary>
    [Fact]
    public async Task LeaveTheCountersToTheRemovalThatTookTheComment()
    {
        var commentId = Guid.NewGuid();
        var publicationId = Guid.NewGuid();

        var comment = new PublicationCommentToDelete
        {
            Id = commentId,
            EntityId = publicationId,
            CreatedUtc = DateTimeOffset.UtcNow
        };

        identityProvider.Current.Returns(Identities.User(Guid.NewGuid(), "testuser"));
        repository.GetForDelete(commentId, default).Returns(comment);
        repository.Delete(Arg.Any<DeletePublicationCommentEntity>(), default).Returns(false);

        await service.DeleteAsync(commentId);

        await countersRepository.DidNotReceive().DecrementAsync(
            Arg.Any<Guid>(), Arg.Any<UnreadEntryType>(), Arg.Any<DateTimeOffset>());
        await eventProducer.DidNotReceive().SendAsync(
            Arg.Any<EventType>(), Arg.Any<Guid>());
    }

    private static BlogDto OpenBlog(Guid blogId, params Guid[] blacklisted) => new()
    {
        Id = blogId,
        Author = new GeneralUser { UserId = Guid.NewGuid() },
        Assistants = Array.Empty<BlogAssistantInfo>(),
        BlacklistedUserIds = blacklisted.ToHashSet()
    };
}
