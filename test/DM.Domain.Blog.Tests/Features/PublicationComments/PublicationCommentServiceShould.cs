using System;
using System.Collections.Generic;
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
using DM.Domain.Core.Users;
using DM.Testing;
using DM.Testing.Markup;
using DM.Domain.Account.Features.Authentication;
using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using Xunit;

namespace DM.Domain.Blog.Tests.Features.PublicationComments;

public class PublicationCommentServiceShould : UnitTestBase
{
    private readonly IValidator<CreateComment> _createValidator;
    private readonly IValidator<UpdateComment> _updateValidator;
    private readonly IBlogService _blogService;
    private readonly IPublicationService _publicationService;
    private readonly IIntentionManager _intentionManager;
    private readonly IIdentityProvider _identityProvider;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IPublicationCommentRepository _repository;
    private readonly IUnreadCountersRepository _countersRepository;
    private readonly IEventProducer _eventProducer;
    private readonly PublicationCommentService _service;

    public PublicationCommentServiceShould()
    {
        _createValidator = Mock<IValidator<CreateComment>>();
        _updateValidator = Mock<IValidator<UpdateComment>>();
        _blogService = Mock<IBlogService>();
        _publicationService = Mock<IPublicationService>();
        _intentionManager = Mock<IIntentionManager>();
        _identityProvider = Mock<IIdentityProvider>();
        _dateTimeProvider = Mock<IDateTimeProvider>();
        _repository = Mock<IPublicationCommentRepository>();
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

        _service = new PublicationCommentService(
            _createValidator,
            _updateValidator,
            _blogService,
            _publicationService,
            _intentionManager,
            _identityProvider,
            _dateTimeProvider,
            _repository,
            _countersRepository,
            _eventProducer,
            new TagNamesAsTyped());
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
        var identity = AuthenticatedIdentities.Of(userId);

        _identityProvider.Current.Returns(identity);
        _publicationService.GetPublication(publicationId, default).Returns(publication);
        _blogService.GetBlogAsync(blogId, default).Returns(blog);
        _repository.Create(Arg.Any<CreateComment>(), userId, publicationId, default)
            .Returns((new Comment { Id = commentId }, commentId));

        await _service.CreateAsync(createComment);

        _intentionManager.Received(1).ThrowIfForbidden(PublicationIntention.CreateComment, publication);
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
        var identity = AuthenticatedIdentities.Of(userId);

        _identityProvider.Current.Returns(identity);
        _publicationService.GetPublication(publicationId, default).Returns(publication);
        _blogService.GetBlogAsync(blogId, default).Returns(blog);

        var act = async () => await _service.CreateAsync(createComment);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden && e.Message == RefusalMessage.BlacklistedFromBlog);
    }

    /// <summary>
    /// Rewriting one's comment under a publication is writing in the blog again.
    /// </summary>
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
        _identityProvider.Current.Returns(AuthenticatedIdentities.Of(userId));
        _repository.Get(comment.Id, default).Returns(comment);
        _publicationService.GetPublication(publicationId, default)
            .Returns(new Publication { Id = publicationId, BlogId = blogId });
        _blogService.GetBlogAsync(blogId, default).Returns(new BlogDto
        {
            Id = blogId,
            Author = new GeneralUser { UserId = Guid.NewGuid() },
            Assistants = Array.Empty<BlogAssistantInfo>(),
            BlacklistedUserIds = new HashSet<Guid> { userId }
        });

        var act = async () => await _service.UpdateAsync(new UpdateComment { CommentId = comment.Id, Text = "Updated" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden && e.Message == RefusalMessage.BlacklistedFromBlog);
        await _repository.DidNotReceive().Update(Arg.Any<UpdatePublicationCommentEntity>(), Arg.Any<CancellationToken>());
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
        var identity = AuthenticatedIdentities.Of(userId);

        _identityProvider.Current.Returns(identity);
        _publicationService.GetPublication(publicationId, default).Returns(publication);
        _blogService.GetBlogAsync(blogId, default).Returns(blog);
        _repository.Create(Arg.Any<CreateComment>(), userId, publicationId, default)
            .Returns((new Comment { Id = commentId }, commentId));

        await _service.CreateAsync(createComment);

        // The publication event, not the blog one: a comment carries the id of
        // whatever it hangs on, so the blog generator joined Comments to Blogs on
        // a publication id and produced nothing at all.
        await _eventProducer.Received(1).SendAsync(EventType.NewPublicationComment, commentId);
        await _eventProducer.DidNotReceive().SendAsync(EventType.NewBlogComment, Arg.Any<Guid>());
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
        _repository.Update(Arg.Any<UpdatePublicationCommentEntity>(), default).Returns(comment);

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
            Arg.Any<UpdatePublicationCommentEntity>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The removal that went through pays the unread counters and announces itself.
    /// </summary>
    /// <remarks>
    /// Deletion in this service had no test at all, alone among the four that
    /// carry comments.
    ///
    /// Neither the count nor the last-comment pointer is asserted here, because
    /// the service no longer decides either: both are written by the transaction
    /// that marks the comment removed, and what a second removal of the same
    /// comment does to them is a question for the database. DiscussionCountersShould
    /// and LastCommentPointerShould answer it against PostgreSQL.
    /// </remarks>
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

        _identityProvider.Current.Returns(AuthenticatedIdentities.Of(userId));
        _repository.GetForDelete(commentId, default).Returns(comment);

        DeletePublicationCommentEntity? deleted = null;
        _repository
            .Delete(Arg.Do<DeletePublicationCommentEntity>(entity => deleted = entity), default)
            .Returns(true);

        await _service.DeleteAsync(commentId);

        _intentionManager.Received(1).ThrowIfForbidden(CommentIntention.Delete, Arg.Any<Comment>());

        deleted.Should().NotBeNull("the deletion is what the repository is handed");
        deleted!.DeletedByUserId.Should().Be(userId,
            "who removed it is the one thing the row cannot recover afterwards");
        await _countersRepository.Received(1).DecrementAsync(
            publicationId, UnreadEntryType.Message, createdUtc);
        await _eventProducer.Received(1).SendAsync(EventType.DeletedPublicationComment, commentId);
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
        var publicationId = Guid.NewGuid();

        var comment = new PublicationCommentToDelete
        {
            Id = commentId,
            EntityId = publicationId,
            CreatedUtc = DateTimeOffset.UtcNow
        };

        _identityProvider.Current.Returns(AuthenticatedIdentities.Of(Guid.NewGuid()));
        _repository.GetForDelete(commentId, default).Returns(comment);
        _repository.Delete(Arg.Any<DeletePublicationCommentEntity>(), default).Returns(false);

        await _service.DeleteAsync(commentId);

        await _countersRepository.DidNotReceive().DecrementAsync(
            Arg.Any<Guid>(), Arg.Any<UnreadEntryType>(), Arg.Any<DateTimeOffset>());
        await _eventProducer.DidNotReceive().SendAsync(
            Arg.Any<EventType>(), Arg.Any<Guid>());
    }

}
