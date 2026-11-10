using System.Threading;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Blog.Authorization;
using DM.Domain.Blog.Features.Blacklists;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Events;
using DM.Domain.Core.Identity;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Core.Users;
using DM.Testing;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using BlogDto = DM.Domain.Blog.Features.Blogs.Blog;

namespace DM.Domain.Blog.Tests.Dsl;

// A real blog service over substitutes, read by a guest until a test signs somebody in.
public abstract class BlogServiceTestBase : UnitTestBase
{
    protected readonly IBlogRepository repository = Mock<IBlogRepository>();
    protected readonly IBlogBlacklistRepository blacklistRepository = Mock<IBlogBlacklistRepository>();
    protected readonly IUserBlacklistChecker userBlacklistChecker = Mock<IUserBlacklistChecker>();
    protected readonly IUserLookupService userLookupService = Mock<IUserLookupService>();
    protected readonly IUnreadCountersRepository unreadCountersRepository = Mock<IUnreadCountersRepository>();
    protected readonly IIdentityProvider identityProvider = Mock<IIdentityProvider>();
    protected readonly IIntentionManager intentionManager = Mock<IIntentionManager>();
    protected readonly IValidator<CreateBlog> createBlogValidator = Mock<IValidator<CreateBlog>>();
    protected readonly IValidator<UpdateBlog> updateBlogValidator = Mock<IValidator<UpdateBlog>>();
    protected readonly IValidator<CreateRubric> createRubricValidator = Mock<IValidator<CreateRubric>>();
    protected readonly IValidator<UpdateRubric> updateRubricValidator = Mock<IValidator<UpdateRubric>>();
    protected readonly IGuidFactory guidFactory = Mock<IGuidFactory>();
    protected readonly IDateTimeProvider dateTimeProvider = Mock<IDateTimeProvider>();
    protected readonly IEventProducer eventProducer = Mock<IEventProducer>();
    private protected readonly BlogService service;

    protected BlogServiceTestBase()
    {
        identityProvider.Current.Returns(Identity.Guest());
        createBlogValidator
            .ValidateAsync(Arg.Any<ValidationContext<CreateBlog>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
        updateBlogValidator
            .ValidateAsync(Arg.Any<ValidationContext<UpdateBlog>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
        updateRubricValidator
            .ValidateAsync(Arg.Any<ValidationContext<UpdateRubric>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        service = new BlogService(
            repository,
            blacklistRepository,
            userBlacklistChecker,
            userLookupService,
            unreadCountersRepository,
            identityProvider,
            intentionManager,
            createBlogValidator,
            updateBlogValidator,
            createRubricValidator,
            updateRubricValidator,
            guidFactory,
            dateTimeProvider,
            eventProducer);
    }

    // The caller holds no role in any blog: neither of the two view gates opens for them, which is
    // what a stranger's read answers.
    protected void HideEveryBlogFromTheCaller() =>
        intentionManager.IsAllowed(Arg.Any<BlogIntention>(), Arg.Any<BlogDto>()).Returns(false);
}
