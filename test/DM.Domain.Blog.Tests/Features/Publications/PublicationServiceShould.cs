using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly IPublicationRepository repository;
    private readonly IBlogService blogService;
    private readonly IUserLookupService userLookupService;
    private readonly IUnreadCountersRepository unreadCountersRepository;
    private readonly IIdentityProvider identityProvider;
    private readonly IIntentionManager intentionManager;
    private readonly IValidator<CreatePublication> createPublicationValidator;
    private readonly IValidator<UpdatePublication> updatePublicationValidator;
    private readonly IGuidFactory guidFactory;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IEventProducer eventProducer;
    private readonly PublicationService service;

    public PublicationServiceShould()
    {
        repository = Mock<IPublicationRepository>();
        blogService = Mock<IBlogService>();
        userLookupService = Mock<IUserLookupService>();
        unreadCountersRepository = Mock<IUnreadCountersRepository>();
        identityProvider = Mock<IIdentityProvider>();
        intentionManager = Mock<IIntentionManager>();
        createPublicationValidator = Mock<IValidator<CreatePublication>>();
        updatePublicationValidator = Mock<IValidator<UpdatePublication>>();
        guidFactory = Mock<IGuidFactory>();
        dateTimeProvider = Mock<IDateTimeProvider>();
        eventProducer = Mock<IEventProducer>();

        identityProvider.Current.Returns(Identity.Guest());
        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        createPublicationValidator
            .ValidateAsync(Arg.Any<ValidationContext<CreatePublication>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
        updatePublicationValidator
            .ValidateAsync(Arg.Any<ValidationContext<UpdatePublication>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        service = new PublicationService(
            repository,
            blogService,
            userLookupService,
            unreadCountersRepository,
            identityProvider,
            intentionManager,
            createPublicationValidator,
            updatePublicationValidator,
            guidFactory,
            dateTimeProvider,
            eventProducer);
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

        guidFactory.Create().Returns(publicationId);
        blogService.GetAsync(blogId, default).Returns(blog);
        repository.CreatePublication(Arg.Any<CreatePublicationEntity>(), default)
            .Returns(new Publication { Id = publicationId });
        eventProducer.SendAsync(EventType.NewPublication, publicationId).Returns(Task.CompletedTask);

        await service.CreatePublication(createPublication);

        await eventProducer.Received(1).SendAsync(EventType.NewPublication, publicationId);
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

        guidFactory.Create().Returns(publicationId);
        blogService.GetAsync(blogId, default).Returns(blog);
        repository.CreatePublication(Arg.Any<CreatePublicationEntity>(), default)
            .Returns(new Publication { Id = publicationId });

        await service.CreatePublication(new CreatePublication
        {
            BlogId = blogId,
            Title = "Test Publication",
            Content = "Content"
        });

        intentionManager.Received(1).ThrowIfForbidden(BlogIntention.CreatePublication, blog);
    }

    [Fact]
    public async Task AnswerNotFoundForAnUnknownPublication()
    {
        var publicationId = Guid.NewGuid();
        repository.GetPublication(publicationId, default).Returns((Publication?)null);

        var act = async () => await service.GetPublication(publicationId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    /// <summary>
    /// A publication is read inside its blog, and the blog is the only thing that
    /// knows whether the reader may be there at all.
    /// </summary>
    [Fact]
    public async Task AskTheBlogWhetherThePublicationMayBeRead()
    {
        var (publicationId, blogId) = ExistingPublication();
        SeesTheBlog(blogId, visible: true);

        await service.GetPublication(publicationId);

        await blogService.Received(1).IsVisibleToViewerAsync(blogId, default);
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
        var (publicationId, blogId) = ExistingPublication();
        SeesTheBlog(blogId, visible: false);

        var missingId = Guid.NewGuid();
        repository.GetPublication(missingId, default).Returns((Publication?)null);

        var onMissing = await Refusal(missingId);
        var onHidden = await Refusal(publicationId);

        onHidden.StatusCode.Should().Be(HttpStatusCode.NotFound);
        onHidden.StatusCode.Should().Be(onMissing.StatusCode);
        onHidden.Message.Should().Be(onMissing.Message);
    }

    /// <summary>
    /// The gate is about the blog and nothing else: a publication in a blog the
    /// reader may open is still read.
    /// </summary>
    [Fact]
    public async Task ReadAPublicationInAVisibleBlog()
    {
        var (publicationId, blogId) = ExistingPublication();
        SeesTheBlog(blogId, visible: true);

        var publication = await service.GetPublication(publicationId);

        publication.Id.Should().Be(publicationId);
    }

    /// <summary>
    /// The profile widget is public, so "published" alone must not hand a stranger
    /// a publication in a blog nobody outside it may open.
    /// </summary>
    [Fact]
    public async Task NameNoBestPublicationFromAHiddenBlog()
    {
        var (_, blogId) = BestPublicationOf("Author");
        SeesTheBlog(blogId, visible: false);

        var publication = await service.GetBestUserPublication("Author");

        publication.Should().BeNull("the only candidate lives in a blog the reader cannot open");
    }

    [Fact]
    public async Task NameTheBestPublicationWhenItsBlogIsVisible()
    {
        var (publicationId, blogId) = BestPublicationOf("Author");
        SeesTheBlog(blogId, visible: true);

        var publication = await service.GetBestUserPublication("Author");

        publication!.Id.Should().Be(publicationId);
    }

    private (Guid PublicationId, Guid BlogId) ExistingPublication()
    {
        var publicationId = Guid.NewGuid();
        var blogId = Guid.NewGuid();
        repository.GetPublication(publicationId, default).Returns(new Publication
        {
            Id = publicationId,
            BlogId = blogId,
            Author = new GeneralUser { UserId = Guid.NewGuid() }
        });
        return (publicationId, blogId);
    }

    private (Guid PublicationId, Guid BlogId) BestPublicationOf(string username)
    {
        var publicationId = Guid.NewGuid();
        var blogId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        userLookupService.GetAsync(username).Returns(new GeneralUser { UserId = authorId, Username = username });
        repository.GetBestUserPublication(
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(authorId)), default)
            .Returns(new Publication
            {
                Id = publicationId,
                BlogId = blogId,
                Author = new GeneralUser { UserId = authorId }
            });
        return (publicationId, blogId);
    }

    private void SeesTheBlog(Guid blogId, bool visible) =>
        blogService.IsVisibleToViewerAsync(blogId, default).Returns(visible);

    private async Task<HttpException> Refusal(Guid publicationId)
    {
        var act = async () => await service.GetPublication(publicationId);
        return (await act.Should().ThrowAsync<HttpException>()).Which;
    }
}
