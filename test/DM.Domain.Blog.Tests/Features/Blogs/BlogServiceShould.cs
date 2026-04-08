using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Identity;
using DM.Domain.Blog.Authorization;
using DM.Domain.Blog.Features.Blacklists;
using DM.Domain.Blog.Features.Blogs;
using BlogDto = DM.Domain.Blog.Features.Blogs.Blog;
using DM.Domain.Blog.Features.Subscriptions;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Core.Users;
using DM.Testing;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Xunit;

namespace DM.Domain.Blog.Tests.Features.Blogs;

public class BlogServiceShould : UnitTestBase
{
    private readonly Mock<IBlogRepository> _repository;
    private readonly Mock<IBlogBlacklistRepository> _blacklistRepository;
    private readonly Mock<IUserLookupService> _userLookupService;
    private readonly Mock<IBlogSubscriptionService> _subscriptionService;
    private readonly Mock<IUnreadCountersRepository> _unreadCountersRepository;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly Mock<IValidator<CreateBlog>> _createBlogValidator;
    private readonly Mock<IValidator<UpdateBlog>> _updateBlogValidator;
    private readonly Mock<IValidator<CreateRubric>> _createRubricValidator;
    private readonly Mock<IValidator<CreatePublication>> _createPublicationValidator;
    private readonly Mock<IValidator<UpdatePublication>> _updatePublicationValidator;
    private readonly Mock<IGuidFactory> _guidFactory;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly Mock<IEventProducer> _eventProducer;
    private readonly BlogService _service;

    public BlogServiceShould()
    {
        _repository = Mock<IBlogRepository>();
        _blacklistRepository = Mock<IBlogBlacklistRepository>();
        _userLookupService = Mock<IUserLookupService>();
        _subscriptionService = Mock<IBlogSubscriptionService>();
        _unreadCountersRepository = Mock<IUnreadCountersRepository>();
        _identityProvider = Mock<IIdentityProvider>();
        _intentionManager = Mock<IIntentionManager>();
        _createBlogValidator = Mock<IValidator<CreateBlog>>();
        _updateBlogValidator = Mock<IValidator<UpdateBlog>>();
        _createRubricValidator = Mock<IValidator<CreateRubric>>();
        _createPublicationValidator = Mock<IValidator<CreatePublication>>();
        _updatePublicationValidator = Mock<IValidator<UpdatePublication>>();
        _guidFactory = Mock<IGuidFactory>();
        _dateTimeProvider = Mock<IDateTimeProvider>();
        _eventProducer = Mock<IEventProducer>();

        _identityProvider.Setup(p => p.Current).Returns(Identity.Guest());
        _dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);

        _createBlogValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreateBlog>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _updateBlogValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<UpdateBlog>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _createPublicationValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreatePublication>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _service = new BlogService(
            _repository.Object,
            _blacklistRepository.Object,
            _userLookupService.Object,
            _subscriptionService.Object,
            _unreadCountersRepository.Object,
            _identityProvider.Object,
            _intentionManager.Object,
            _createBlogValidator.Object,
            _updateBlogValidator.Object,
            _createRubricValidator.Object,
            _createPublicationValidator.Object,
            _updatePublicationValidator.Object,
            _guidFactory.Object,
            _dateTimeProvider.Object,
            _eventProducer.Object);
    }

    [Fact]
    public async Task AuthorizeCreateBlogAction()
    {
        var blogId = Guid.NewGuid();
        var createBlog = new CreateBlog { Title = "Test Blog", DraftVisibility = DraftVisibility.Public };

        _guidFactory.Setup(f => f.Create()).Returns(blogId);
        _repository.Setup(r => r.CreateBlog(It.IsAny<CreateBlogEntity>(), default))
            .ReturnsAsync(new BlogDto { Id = blogId });

        await _service.Create(createBlog);

        _intentionManager.Verify(m => m.ThrowIfForbidden(BlogIntention.Create), Times.Once);
    }

    [Fact]
    public async Task CreateBlogWithCorrectData()
    {
        var blogId = Guid.NewGuid();
        var createBlog = new CreateBlog
        {
            Title = "Test Blog",
            Description = "Description",
            DraftVisibility = DraftVisibility.Public,
            CommentsEnabled = true
        };

        _guidFactory.Setup(f => f.Create()).Returns(blogId);
        _repository.Setup(r => r.CreateBlog(It.IsAny<CreateBlogEntity>(), default))
            .ReturnsAsync(new BlogDto { Id = blogId });

        var result = await _service.Create(createBlog);

        result.Id.Should().Be(blogId);
        _repository.Verify(r => r.CreateBlog(
            It.Is<CreateBlogEntity>(e =>
                e.BlogId == blogId &&
                e.Title == "Test Blog" &&
                e.Description == "Description"),
            default), Times.Once);
    }

    [Fact]
    public async Task AuthorizeUpdateBlogAction()
    {
        var blogId = Guid.NewGuid();
        var blog = new BlogDto { Id = blogId, DraftVisibility = DraftVisibility.Public };
        var updateBlog = new UpdateBlog { BlogId = blogId, Title = "Updated" };

        _repository.Setup(r => r.Get(blogId, default)).ReturnsAsync(blog);
        _repository.Setup(r => r.UpdateBlog(It.IsAny<UpdateBlogEntity>(), default))
            .ReturnsAsync(blog);

        await _service.Update(updateBlog);

        _intentionManager.Verify(m => m.ThrowIfForbidden(BlogIntention.Edit, blog), Times.Once);
    }

    [Fact]
    public async Task PublishEventWhenCreatingPublication()
    {
        var blogId = Guid.NewGuid();
        var publicationId = Guid.NewGuid();
        var blog = new BlogDto { Id = blogId, DraftVisibility = DraftVisibility.Public };
        var createPublication = new CreatePublication
        {
            BlogId = blogId,
            Title = "Test Publication",
            Content = "Content"
        };

        _guidFactory.Setup(f => f.Create()).Returns(publicationId);
        _repository.Setup(r => r.Get(blogId, default)).ReturnsAsync(blog);
        _repository.Setup(r => r.CreatePublication(It.IsAny<CreatePublicationEntity>(), default))
            .ReturnsAsync(new Publication { Id = publicationId });
        _eventProducer.Setup(p => p.SendAsync(EventType.NewPublication, publicationId))
            .Returns(Task.CompletedTask);

        await _service.CreatePublication(createPublication);

        _eventProducer.Verify(p => p.SendAsync(EventType.NewPublication, publicationId), Times.Once);
    }

    [Fact]
    public async Task ThrowWhenSubscribingToOwnBlog()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var user = new AuthenticatedUser { UserId = userId, Username = "user" };
        var session = new Session();
        var identity = Identity.Success(user, session, UserSettings.Default, "token");

        var blog = new BlogDto
        {
            Id = blogId,
            Author = new GeneralUser { UserId = userId }
        };

        _identityProvider.Setup(p => p.Current).Returns(identity);
        _repository.Setup(r => r.Get(blogId, default)).ReturnsAsync(blog);

        var act = async () => await _service.Subscribe(blogId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden && e.Message.Contains("own blog"));
    }
}
