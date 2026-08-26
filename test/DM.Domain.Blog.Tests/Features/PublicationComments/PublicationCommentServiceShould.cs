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
            _eventProducer);
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
        _repository.Create(Arg.Any<CreateComment>(), userId, publicationId, Arg.Any<int>(), default)
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
        var identity = AuthenticatedIdentities.Of(userId);

        _identityProvider.Current.Returns(identity);
        _publicationService.GetPublication(publicationId, default).Returns(publication);
        _blogService.GetBlogAsync(blogId, default).Returns(blog);
        _repository.Create(Arg.Any<CreateComment>(), userId, publicationId, Arg.Any<int>(), default)
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
    /// Deleting the last comment moves the pointer to the one before it.
    /// </summary>
    /// <remarks>
    /// Two things were uncovered here, and this is both of them. Deletion in this
    /// service had no test at all, alone among the four that carry comments; and
    /// the branch that moves the "last comment" pointer has none in any of the
    /// four, because every one of them deletes a comment that is not the last.
    ///
    /// What that branch costs when it is wrong: the publication keeps pointing at
    /// a comment that no longer exists, so the list shows a last activity nobody
    /// can open, and the count beside it is one too many.
    /// </remarks>
    [Fact]
    public async Task PointAtTheCommentBeforeTheLastOneWhenTheLastIsDeleted()
    {
        var commentId = Guid.NewGuid();
        var publicationId = Guid.NewGuid();
        var successorId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var comment = new PublicationCommentToDelete
        {
            Id = commentId,
            EntityId = publicationId,
            IsLastComment = true,
            PublicationCommentCount = 5,
            CreatedUtc = DateTimeOffset.UtcNow
        };

        _identityProvider.Current.Returns(AuthenticatedIdentities.Of(userId));
        _repository.GetForDelete(commentId, default).Returns(comment);
        _repository
            .GetNewestCommentIdExcept(publicationId, commentId, default).Returns(successorId);

        DeletePublicationCommentEntity? deleted = null;
        _repository
            .When(r => r.Delete(Arg.Any<DeletePublicationCommentEntity>(), default))
            .Do(ci =>
                {
                    var entity = ci.ArgAt<DeletePublicationCommentEntity>(0);
                    deleted = entity;
                });

        await _service.DeleteAsync(commentId);

        _intentionManager.Received(1).ThrowIfForbidden(CommentIntention.Delete, Arg.Any<Comment>());

        deleted.Should().NotBeNull("the deletion is what the repository is handed");
        deleted!.NewLastCommentId.Should().Be(successorId,
            "the pointer has to move off the comment being removed, or the publication " +
            "keeps advertising an activity nobody can open");
        deleted.NewCommentCount.Should().Be(4,
            "the count beside it is one less than it was");
        deleted.DeletedByUserId.Should().Be(userId,
            "who removed it is the one thing the row cannot recover afterwards");
    }

    [Fact]
    public async Task LeaveThePointerAloneWhenTheDeletedCommentIsNotTheLast()
    {
        var commentId = Guid.NewGuid();
        var publicationId = Guid.NewGuid();

        var comment = new PublicationCommentToDelete
        {
            Id = commentId,
            EntityId = publicationId,
            IsLastComment = false,
            PublicationCommentCount = 5,
            CreatedUtc = DateTimeOffset.UtcNow
        };

        _identityProvider.Current.Returns(AuthenticatedIdentities.Of(Guid.NewGuid()));
        _repository.GetForDelete(commentId, default).Returns(comment);

        DeletePublicationCommentEntity? deleted = null;
        _repository
            .When(r => r.Delete(Arg.Any<DeletePublicationCommentEntity>(), default))
            .Do(ci =>
                {
                    var entity = ci.ArgAt<DeletePublicationCommentEntity>(0);
                    deleted = entity;
                });

        await _service.DeleteAsync(commentId);

        deleted!.NewLastCommentId.Should().BeNull(
            "the last comment is somebody else's and stays where it is");
        // A search for a successor that is not needed is a query per deletion.
        await _repository.DidNotReceive().GetNewestCommentIdExcept(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

}
