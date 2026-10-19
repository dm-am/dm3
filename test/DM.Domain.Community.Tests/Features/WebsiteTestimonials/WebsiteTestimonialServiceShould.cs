using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Community.Authorization;
using DM.Domain.Community.Features.WebsiteTestimonials;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Users;
using DM.Testing.Dsl;
using DM.Testing;
using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace DM.Domain.Community.Tests.Features.WebsiteTestimonials;

public class WebsiteTestimonialServiceShould : UnitTestBase
{
    private readonly IValidator<CreateWebsiteTestimonial> createValidator;
    private readonly IValidator<UpdateWebsiteTestimonial> updateValidator;
    private readonly IIntentionManager intentionManager;
    private readonly IWebsiteTestimonialRepository repository;
    private readonly IUserReadRepository userRepository;
    private readonly IEventProducer eventProducer;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IGuidFactory guidFactory;
    private readonly IIdentityProvider identityProvider;
    private readonly WebsiteTestimonialService service;
    private readonly Guid currentUserId;

    /// <summary>
    /// The participant the testimonial is signed by - never the caller, who is
    /// the moderator submitting it on their behalf.
    /// </summary>
    private const string AuthorUsername = "Solohin";

    private readonly Guid authorId = Guid.NewGuid();

    private static CreateWebsiteTestimonial NewTestimonial(string text = "Great website!") =>
        new() { AuthorUsername = AuthorUsername, Text = text };

    public WebsiteTestimonialServiceShould()
    {
        createValidator = Mock<IValidator<CreateWebsiteTestimonial>>();
        createValidator.ValidateAsync(Arg.Any<CreateWebsiteTestimonial>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        updateValidator = Mock<IValidator<UpdateWebsiteTestimonial>>();
        updateValidator.ValidateAsync(Arg.Any<UpdateWebsiteTestimonial>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        intentionManager = Mock<IIntentionManager>();

        repository = Mock<IWebsiteTestimonialRepository>();

        userRepository = Mock<IUserReadRepository>();
        userRepository.FindUserIdAsync(AuthorUsername).Returns(authorId);

        eventProducer = Mock<IEventProducer>();

        currentUserId = Guid.NewGuid();
        identityProvider = Mock<IIdentityProvider>();
        identityProvider.Current.Returns(Identities.User(currentUserId, UserRole.RegularUser));

        guidFactory = Mock<IGuidFactory>();
        guidFactory.Create().Returns(Guid.NewGuid());

        dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        service = new WebsiteTestimonialService(
            createValidator,
            updateValidator,
            intentionManager,
            repository,
            userRepository,
            eventProducer,
            dateTimeProvider,
            guidFactory,
            identityProvider);
    }

    [Fact]
    public async Task AuthorizeCreateAction()
    {
        var expectedTestimonial = new WebsiteTestimonial { Id = Guid.NewGuid(), Text = "Great website!" };
        repository.GetByAuthor(authorId).Returns((WebsiteTestimonial?)null);
        repository.Create(Arg.Any<CreateWebsiteTestimonialEntity>()).Returns(expectedTestimonial);

        await service.CreateAsync(NewTestimonial());

        intentionManager.Received(1).ThrowIfForbidden(WebsiteTestimonialIntention.Create);
    }

    /// <summary>
    /// The whole point of the named author: the entry is signed by the
    /// participant, not by the moderator who filled the form in.
    /// </summary>
    [Fact]
    public async Task SignTheTestimonialWithTheNamedParticipant()
    {
        CreateWebsiteTestimonialEntity? written = null;
        repository.GetByAuthor(authorId).Returns((WebsiteTestimonial?)null);
        repository.Create(Arg.Do<CreateWebsiteTestimonialEntity>(e => written = e))
            .Returns(new WebsiteTestimonial { Id = Guid.NewGuid(), Text = "Great website!" });

        await service.CreateAsync(NewTestimonial());

        written.Should().NotBeNull();
        written!.AuthorId.Should().Be(authorId);
        written.AuthorId.Should().NotBe(currentUserId);
    }

    [Fact]
    public async Task ThrowNotFoundWhenTheNamedAuthorDoesNotExist()
    {
        userRepository.FindUserIdAsync("Nobody").Returns((Guid?)null);

        var act = async () => await service.CreateAsync(
            new CreateWebsiteTestimonial { AuthorUsername = "Nobody", Text = "Great website!" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
        await repository.DidNotReceive().Create(Arg.Any<CreateWebsiteTestimonialEntity>());
    }

    /// <summary>
    /// One testimonial per participant, and the participant is the named one, not the caller.
    /// </summary>
    [Fact]
    public async Task ThrowConflictWhenTheNamedParticipantAlreadyHasTestimonial()
    {
        var existingTestimonial = TestimonialBy(authorId);
        repository.GetByAuthor(authorId).Returns(existingTestimonial);

        var act = async () => await service.CreateAsync(NewTestimonial("New testimonial!"));

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict);
    }

    /// <summary>
    /// Two requests can both pass the pre-check, and the loser reads the same sentence the pre-check writes.
    /// </summary>
    [Fact]
    public async Task ThrowConflictWhenTheIndexRefusesTheEntryThatLostTheRace()
    {
        repository.GetByAuthor(authorId).Returns((WebsiteTestimonial?)null);
        repository.Create(Arg.Any<CreateWebsiteTestimonialEntity>())
            .ThrowsAsync(new DuplicateEntityException("Duplicate website testimonial"));

        var act = async () => await service.CreateAsync(NewTestimonial());

        var refusal = await act.Should().ThrowAsync<HttpException>();
        refusal.Which.StatusCode.Should().Be(HttpStatusCode.Conflict);
        refusal.Which.Message.Should().Be(RefusalMessage.TestimonialAlreadyExists(AuthorUsername),
            "the pre-check and the index answer with one sentence");
        await eventProducer.DidNotReceive().SendAsync(EventType.NewWebsiteTestimonial, Arg.Any<Guid>());
    }

    [Fact]
    public async Task NotLookAtTheSubmittersOwnTestimonialWhenCreating()
    {
        var submittersOwn = TestimonialBy(currentUserId);
        repository.GetByAuthor(currentUserId).Returns(submittersOwn);
        repository.GetByAuthor(authorId).Returns((WebsiteTestimonial?)null);
        repository.Create(Arg.Any<CreateWebsiteTestimonialEntity>())
            .Returns(new WebsiteTestimonial { Id = Guid.NewGuid(), Text = "Great website!" });

        await service.CreateAsync(NewTestimonial());

        await repository.Received(1).Create(Arg.Any<CreateWebsiteTestimonialEntity>());
    }

    [Fact]
    public async Task SendEventOnCreate()
    {
        var testimonialId = Guid.NewGuid();
        var expectedTestimonial = new WebsiteTestimonial { Id = testimonialId, Text = "Great website!" };
        repository.GetByAuthor(authorId).Returns((WebsiteTestimonial?)null);
        repository.Create(Arg.Any<CreateWebsiteTestimonialEntity>()).Returns(expectedTestimonial);

        await service.CreateAsync(NewTestimonial());

        await eventProducer.Received(1).SendAsync(EventType.NewWebsiteTestimonial, testimonialId);
    }

    [Fact]
    public async Task ThrowNotFoundWhenTestimonialDoesNotExist()
    {
        var testimonialId = Guid.NewGuid();
        repository.Get(testimonialId).Returns((WebsiteTestimonial?)null);

        var act = async () => await service.GetAsync(testimonialId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReturnTestimonialWhenExists()
    {
        var testimonialId = Guid.NewGuid();
        var testimonial = new WebsiteTestimonial { Id = testimonialId, Text = "Great website!" };
        repository.Get(testimonialId).Returns(testimonial);

        var result = await service.GetAsync(testimonialId);

        result.Should().Be(testimonial);
    }

    [Fact]
    public async Task AuthorizeEditAction()
    {
        var testimonialId = Guid.NewGuid();
        var testimonial = TestimonialBy(currentUserId, testimonialId);
        repository.Get(testimonialId).Returns(testimonial);
        repository.Update(Arg.Any<UpdateWebsiteTestimonialEntity>()).Returns(testimonial);

        await service.UpdateAsync(new UpdateWebsiteTestimonial { Id = testimonialId, Text = "Updated text" });

        intentionManager.Received(1).ThrowIfForbidden(WebsiteTestimonialIntention.Edit, testimonial);
    }

    /// <summary>
    /// A refusal has to stop the write, not merely be recorded. The resolver
    /// decides who is a stranger here (see WebsiteTestimonialIntentionResolverShould);
    /// what this asserts is that its "no" reaches the repository as silence.
    /// </summary>
    [Fact]
    public async Task NotRewriteAnothersTestimonialWhenTheIntentionIsRefused()
    {
        var testimonialId = Guid.NewGuid();
        var testimonial = TestimonialBy(Guid.NewGuid(), testimonialId);
        repository.Get(testimonialId).Returns(testimonial);
        intentionManager
            .When(m => m.ThrowIfForbidden(WebsiteTestimonialIntention.Edit, testimonial))
            .Throw(new IntentionManagerException(
                Create.User(Guid.NewGuid()).WithRole(UserRole.Moderator).Please(),
                WebsiteTestimonialIntention.Edit,
                testimonial));

        var act = async () => await service.UpdateAsync(
            new UpdateWebsiteTestimonial { Id = testimonialId, Text = "Updated text" });

        await act.Should().ThrowAsync<IntentionManagerException>();
        await repository.DidNotReceive().Update(Arg.Any<UpdateWebsiteTestimonialEntity>());
    }

    /// <summary>
    /// Who pressed save is recorded separately from who signed the entry: the
    /// author line is a claim about a participant, and a moderator's correction
    /// must not quietly transfer it to the moderator.
    /// </summary>
    [Fact]
    public async Task KeepTheAuthorWhenAModeratorRewritesTheText()
    {
        var testimonialId = Guid.NewGuid();
        var testimonial = TestimonialBy(authorId, testimonialId);
        UpdateWebsiteTestimonialEntity? written = null;
        repository.Get(testimonialId).Returns(testimonial);
        repository.Update(Arg.Do<UpdateWebsiteTestimonialEntity>(e => written = e))
            .Returns(testimonial);

        await service.UpdateAsync(new UpdateWebsiteTestimonial { Id = testimonialId, Text = "Updated text" });

        written.Should().NotBeNull();
        written!.ModifiedByUserId.Should().Be(currentUserId);
        written.Text.Should().Be("Updated text");
    }

    [Fact]
    public async Task AuthorizeDeleteAction()
    {
        var testimonialId = Guid.NewGuid();
        var testimonial = TestimonialBy(currentUserId, testimonialId);
        repository.Get(testimonialId).Returns(testimonial);

        await service.DeleteAsync(testimonialId);

        intentionManager.Received(1).ThrowIfForbidden(WebsiteTestimonialIntention.Delete, testimonial);
    }

    [Fact]
    public async Task DeleteTestimonialSuccessfully()
    {
        var testimonialId = Guid.NewGuid();
        var testimonial = TestimonialBy(currentUserId, testimonialId);
        repository.Get(testimonialId).Returns(testimonial);

        await service.DeleteAsync(testimonialId);

        await repository.Received(1).Delete(testimonialId, currentUserId);
    }

    private static WebsiteTestimonial TestimonialBy(Guid authorUserId, Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Author = new GeneralUser { UserId = authorUserId },
        Text = "Original text"
    };
}
