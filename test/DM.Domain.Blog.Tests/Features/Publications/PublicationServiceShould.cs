using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Blog.Authorization;
using DM.Domain.Blog.Features.Blogs;
using BlogDto = DM.Domain.Blog.Features.Blogs.Blog;
using DM.Domain.Blog.Features.Publications;
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
using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using Xunit;

namespace DM.Domain.Blog.Tests.Features.Publications;

public class PublicationServiceShould : UnitTestBase
{
    private readonly IPublicationRepository _repository;
    private readonly IBlogService _blogService;
    private readonly IUserLookupService _userLookupService;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IIntentionManager _intentionManager;
    private readonly IValidator<CreatePublication> _createPublicationValidator;
    private readonly IValidator<UpdatePublication> _updatePublicationValidator;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IEventProducer _eventProducer;
    private readonly PublicationService _service;

    public PublicationServiceShould()
    {
        _repository = Mock<IPublicationRepository>();
        _blogService = Mock<IBlogService>();
        _userLookupService = Mock<IUserLookupService>();
        _unreadCountersRepository = Mock<IUnreadCountersRepository>();
        _identityProvider = Mock<IIdentityProvider>();
        _intentionManager = Mock<IIntentionManager>();
        _createPublicationValidator = Mock<IValidator<CreatePublication>>();
        _updatePublicationValidator = Mock<IValidator<UpdatePublication>>();
        _guidFactory = Mock<IGuidFactory>();
        _dateTimeProvider = Mock<IDateTimeProvider>();
        _eventProducer = Mock<IEventProducer>();

        _identityProvider.Current.Returns(Identity.Guest());
        _dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        _createPublicationValidator
            .ValidateAsync(Arg.Any<ValidationContext<CreatePublication>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
        _updatePublicationValidator
            .ValidateAsync(Arg.Any<ValidationContext<UpdatePublication>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        _service = new PublicationService(
            _repository,
            _blogService,
            _userLookupService,
            _unreadCountersRepository,
            _identityProvider,
            _intentionManager,
            _createPublicationValidator,
            _updatePublicationValidator,
            _guidFactory,
            _dateTimeProvider,
            _eventProducer);
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

        _guidFactory.Create().Returns(publicationId);
        _blogService.GetAsync(blogId, default).Returns(blog);
        _repository.CreatePublication(Arg.Any<CreatePublicationEntity>(), default)
            .Returns(new Publication { Id = publicationId });
        _eventProducer.SendAsync(EventType.NewPublication, publicationId).Returns(Task.CompletedTask);

        await _service.CreatePublication(createPublication);

        await _eventProducer.Received(1).SendAsync(EventType.NewPublication, publicationId);
    }

    /// <summary>
    /// The right to publish is a property of the blog, and the publication
    /// service has to go and ask the blog for it rather than answer itself.
    /// </summary>
    [Fact]
    public async Task AskTheBlogWhetherAPublicationMayBeCreated()
    {
        var blogId = Guid.NewGuid();
        var publicationId = Guid.NewGuid();
        var blog = new BlogDto { Id = blogId, DraftVisibility = DraftVisibility.Public };

        _guidFactory.Create().Returns(publicationId);
        _blogService.GetAsync(blogId, default).Returns(blog);
        _repository.CreatePublication(Arg.Any<CreatePublicationEntity>(), default)
            .Returns(new Publication { Id = publicationId });

        await _service.CreatePublication(new CreatePublication
        {
            BlogId = blogId,
            Title = "Test Publication",
            Content = "Content"
        });

        _intentionManager.Received(1).ThrowIfForbidden(BlogIntention.CreatePublication, blog);
    }

    [Fact]
    public async Task AnswerNotFoundForAnUnknownPublication()
    {
        var publicationId = Guid.NewGuid();
        _repository.GetPublication(publicationId, default).Returns((Publication?)null);

        var act = async () => await _service.GetPublication(publicationId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    /// <summary>
    /// A publication is read inside its blog, and the blog is the only thing that
    /// knows whether the reader may be there at all. Asking it is what the listing
    /// has always done and the single read did not.
    /// </summary>
    [Fact]
    public async Task AskTheBlogWhetherThePublicationMayBeRead()
    {
        var (publicationId, blogId) = ExistingPublication(isPublished: true);
        SeesTheBlog(blogId, visible: true);

        await _service.GetPublication(publicationId);

        await _blogService.Received(1).IsVisibleToViewerAsync(blogId, default);
    }

    /// <summary>
    /// And the refusal on a blog the reader cannot open is the one a publication
    /// that never existed produces, sentence included: a 403 here would confirm
    /// the publication is there, which is the half of the leak that hiding the
    /// body does not close.
    /// </summary>
    [Fact]
    public async Task RefuseAPublicationInAHiddenBlogTheWayItRefusesAMissingOne()
    {
        var (publicationId, blogId) = ExistingPublication(isPublished: true);
        SeesTheBlog(blogId, visible: false);

        var missingId = Guid.NewGuid();
        _repository.GetPublication(missingId, default).Returns((Publication?)null);

        var onMissing = await Refusal(missingId);
        var onHidden = await Refusal(publicationId);

        onHidden.StatusCode.Should().Be(HttpStatusCode.NotFound);
        onHidden.StatusCode.Should().Be(onMissing.StatusCode);
        onHidden.Message.Should().Be(onMissing.Message);
    }

    /// <summary>
    /// The gate is about the blog and nothing else: a publication in a blog the
    /// reader may open is still read, and the draft gate beyond it still applies.
    /// </summary>
    [Fact]
    public async Task ReadAPublicationInAVisibleBlog()
    {
        var (publicationId, blogId) = ExistingPublication(isPublished: true);
        SeesTheBlog(blogId, visible: true);

        var publication = await _service.GetPublication(publicationId);

        publication.Id.Should().Be(publicationId);
    }

    /// <summary>
    /// The profile widget is the reachability half of the same leak: it is public,
    /// and it used to filter the author's rows by nothing but "published", so it
    /// handed a stranger both the identifier and the body of a publication sitting
    /// in a blog nobody outside it may open.
    /// </summary>
    [Fact]
    public async Task NameNoBestPublicationFromAHiddenBlog()
    {
        var (_, blogId) = BestPublicationOf("Author");
        SeesTheBlog(blogId, visible: false);

        var publication = await _service.GetBestUserPublication("Author");

        publication.Should().BeNull("the only candidate lives in a blog the reader cannot open");
    }

    [Fact]
    public async Task NameTheBestPublicationWhenItsBlogIsVisible()
    {
        var (publicationId, blogId) = BestPublicationOf("Author");
        SeesTheBlog(blogId, visible: true);

        var publication = await _service.GetBestUserPublication("Author");

        publication!.Id.Should().Be(publicationId);
    }

    private (Guid PublicationId, Guid BlogId) ExistingPublication(bool isPublished)
    {
        var publicationId = Guid.NewGuid();
        var blogId = Guid.NewGuid();
        _repository.GetPublication(publicationId, default).Returns(new Publication
        {
            Id = publicationId,
            BlogId = blogId,
            IsPublished = isPublished,
            Author = new GeneralUser { UserId = Guid.NewGuid() }
        });
        return (publicationId, blogId);
    }

    private (Guid PublicationId, Guid BlogId) BestPublicationOf(string username)
    {
        var publicationId = Guid.NewGuid();
        var blogId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        _userLookupService.GetAsync(username).Returns(new GeneralUser { UserId = authorId, Username = username });
        _repository.GetBestUserPublication(authorId, default).Returns(new Publication
        {
            Id = publicationId,
            BlogId = blogId,
            IsPublished = true,
            Author = new GeneralUser { UserId = authorId }
        });
        return (publicationId, blogId);
    }

    private void SeesTheBlog(Guid blogId, bool visible) =>
        _blogService.IsVisibleToViewerAsync(blogId, default).Returns(visible);

    private async Task<HttpException> Refusal(Guid publicationId)
    {
        var act = async () => await _service.GetPublication(publicationId);
        return (await act.Should().ThrowAsync<HttpException>()).Which;
    }
}
