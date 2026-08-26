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
    private readonly IValidator<CreateWebsiteTestimonial> _createValidator;
    private readonly IValidator<UpdateWebsiteTestimonial> _updateValidator;
    private readonly IIntentionManager _intentionManager;
    private readonly IWebsiteTestimonialRepository _repository;
    private readonly IUserReadRepository _userRepository;
    private readonly IEventProducer _eventProducer;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IIdentityProvider _identityProvider;
    private readonly WebsiteTestimonialService _service;
    private readonly Guid _currentUserId;

    /// <summary>
    /// The participant the testimonial is signed by - never the caller, who is
    /// the moderator submitting it on their behalf.
    /// </summary>
    private const string AuthorUsername = "Solohin";

    private readonly Guid _authorId = Guid.NewGuid();

    private CreateWebsiteTestimonial NewTestimonial(string text = "Great website!") =>
        new() { AuthorUsername = AuthorUsername, Text = text };

    public WebsiteTestimonialServiceShould()
    {
        _createValidator = Mock<IValidator<CreateWebsiteTestimonial>>();
        _createValidator.ValidateAsync(Arg.Any<CreateWebsiteTestimonial>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        _updateValidator = Mock<IValidator<UpdateWebsiteTestimonial>>();
        _updateValidator.ValidateAsync(Arg.Any<UpdateWebsiteTestimonial>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        _intentionManager = Mock<IIntentionManager>();

        _repository = Mock<IWebsiteTestimonialRepository>();

        _userRepository = Mock<IUserReadRepository>();
        _userRepository.FindUserIdAsync(AuthorUsername).Returns(_authorId);

        _eventProducer = Mock<IEventProducer>();

        _currentUserId = Guid.NewGuid();
        _identityProvider = Mock<IIdentityProvider>();
        _identityProvider.Current.Returns(Identities.User(_currentUserId, UserRole.RegularUser));

        _guidFactory = Mock<IGuidFactory>();
        _guidFactory.Create().Returns(Guid.NewGuid());

        _dateTimeProvider = Mock<IDateTimeProvider>();
        _dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        _service = new WebsiteTestimonialService(
            _createValidator,
            _updateValidator,
            _intentionManager,
            _repository,
            _userRepository,
            _eventProducer,
            _dateTimeProvider,
            _guidFactory,
            _identityProvider);
    }

    #region Create Tests

    [Fact]
    public async Task AuthorizeCreateAction()
    {
        var expectedTestimonial = new WebsiteTestimonial { Id = Guid.NewGuid(), Text = "Great website!" };
        _repository.GetByAuthor(_authorId).Returns((WebsiteTestimonial?)null);
        _repository.Create(Arg.Any<CreateWebsiteTestimonialEntity>()).Returns(expectedTestimonial);

        await _service.CreateAsync(NewTestimonial());

        _intentionManager.Received(1).ThrowIfForbidden(WebsiteTestimonialIntention.Create);
    }

    /// <summary>
    /// The whole point of the named author: the entry is signed by the
    /// participant, not by the moderator who filled the form in.
    /// </summary>
    [Fact]
    public async Task SignTheTestimonialWithTheNamedParticipant()
    {
        CreateWebsiteTestimonialEntity? written = null;
        _repository.GetByAuthor(_authorId).Returns((WebsiteTestimonial?)null);
        _repository.Create(Arg.Any<CreateWebsiteTestimonialEntity>())
            .Returns(new WebsiteTestimonial { Id = Guid.NewGuid(), Text = "Great website!" })
            .AndDoes(ci =>
            {
                var e = ci.ArgAt<CreateWebsiteTestimonialEntity>(0);
                written = e;
            });

        await _service.CreateAsync(NewTestimonial());

        written.Should().NotBeNull();
        written!.AuthorId.Should().Be(_authorId);
        written.AuthorId.Should().NotBe(_currentUserId);
    }

    [Fact]
    public async Task ThrowNotFoundWhenTheNamedAuthorDoesNotExist()
    {
        _userRepository.FindUserIdAsync("Nobody").Returns((Guid?)null);

        var act = async () => await _service.CreateAsync(
            new CreateWebsiteTestimonial { AuthorUsername = "Nobody", Text = "Great website!" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
        await _repository.DidNotReceive().Create(Arg.Any<CreateWebsiteTestimonialEntity>());
    }

    /// <summary>
    /// One testimonial per participant, and the participant is the named one.
    /// Checked against the caller, the rule guarded the wrong person twice
    /// over: a second entry for the same participant went through, and a
    /// moderator with a testimonial of their own could add none for anybody.
    /// </summary>
    [Fact]
    public async Task ThrowConflictWhenTheNamedParticipantAlreadyHasTestimonial()
    {
        var existingTestimonial = new WebsiteTestimonial
        {
            Id = Guid.NewGuid(),
            Author = new GeneralUser { UserId = _authorId },
            Text = "Existing testimonial"
        };
        _repository.GetByAuthor(_authorId).Returns(existingTestimonial);

        var act = async () => await _service.CreateAsync(NewTestimonial("New testimonial!"));

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task NotLookAtTheSubmittersOwnTestimonialWhenCreating()
    {
        var submittersOwn = new WebsiteTestimonial
        {
            Id = Guid.NewGuid(),
            Author = new GeneralUser { UserId = _currentUserId },
            Text = "The moderator's own entry"
        };
        _repository.GetByAuthor(_currentUserId).Returns(submittersOwn);
        _repository.GetByAuthor(_authorId).Returns((WebsiteTestimonial?)null);
        _repository.Create(Arg.Any<CreateWebsiteTestimonialEntity>())
            .Returns(new WebsiteTestimonial { Id = Guid.NewGuid(), Text = "Great website!" });

        await _service.CreateAsync(NewTestimonial());

        await _repository.Received(1).Create(Arg.Any<CreateWebsiteTestimonialEntity>());
    }

    [Fact]
    public async Task CreateTestimonialSuccessfully()
    {
        var expectedTestimonial = new WebsiteTestimonial
        {
            Id = Guid.NewGuid(),
            Author = new GeneralUser { UserId = _authorId },
            Text = "Great website!"
        };
        _repository.GetByAuthor(_authorId).Returns((WebsiteTestimonial?)null);
        _repository.Create(Arg.Any<CreateWebsiteTestimonialEntity>()).Returns(expectedTestimonial);

        var result = await _service.CreateAsync(NewTestimonial());

        result.Should().Be(expectedTestimonial);
        await _repository.Received(1).Create(Arg.Any<CreateWebsiteTestimonialEntity>());
    }

    [Fact]
    public async Task SendEventOnCreate()
    {
        var testimonialId = Guid.NewGuid();
        var expectedTestimonial = new WebsiteTestimonial { Id = testimonialId, Text = "Great website!" };
        _repository.GetByAuthor(_authorId).Returns((WebsiteTestimonial?)null);
        _repository.Create(Arg.Any<CreateWebsiteTestimonialEntity>()).Returns(expectedTestimonial);

        await _service.CreateAsync(NewTestimonial());

        await _eventProducer.Received(1).SendAsync(EventType.NewWebsiteTestimonial, testimonialId);
    }

    #endregion

    #region Get Tests

    [Fact]
    public async Task ThrowNotFoundWhenTestimonialDoesNotExist()
    {
        var testimonialId = Guid.NewGuid();
        _repository.Get(testimonialId).Returns((WebsiteTestimonial?)null);

        var act = async () => await _service.GetAsync(testimonialId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReturnTestimonialWhenExists()
    {
        var testimonialId = Guid.NewGuid();
        var testimonial = new WebsiteTestimonial { Id = testimonialId, Text = "Great website!" };
        _repository.Get(testimonialId).Returns(testimonial);

        var result = await _service.GetAsync(testimonialId);

        result.Should().Be(testimonial);
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task AuthorizeEditAction()
    {
        var testimonialId = Guid.NewGuid();
        var testimonial = new WebsiteTestimonial
        {
            Id = testimonialId,
            Author = new GeneralUser { UserId = _currentUserId },
            Text = "Original text"
        };
        _repository.Get(testimonialId).Returns(testimonial);
        _repository.Update(Arg.Any<UpdateWebsiteTestimonialEntity>()).Returns(testimonial);

        await _service.UpdateAsync(new UpdateWebsiteTestimonial { Id = testimonialId, Text = "Updated text" });

        _intentionManager.Received(1).ThrowIfForbidden(WebsiteTestimonialIntention.Edit, testimonial);
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
        var testimonial = new WebsiteTestimonial
        {
            Id = testimonialId,
            Author = new GeneralUser { UserId = Guid.NewGuid() },
            Text = "Original text"
        };
        _repository.Get(testimonialId).Returns(testimonial);
        _intentionManager
            .When(m => m.ThrowIfForbidden(WebsiteTestimonialIntention.Edit, testimonial))
            .Throw(new IntentionManagerException(
                Create.User(Guid.NewGuid()).WithRole(UserRole.Moderator).Please(),
                WebsiteTestimonialIntention.Edit,
                testimonial));

        var act = async () => await _service.UpdateAsync(
            new UpdateWebsiteTestimonial { Id = testimonialId, Text = "Updated text" });

        await act.Should().ThrowAsync<IntentionManagerException>();
        await _repository.DidNotReceive().Update(Arg.Any<UpdateWebsiteTestimonialEntity>());
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
        var testimonial = new WebsiteTestimonial
        {
            Id = testimonialId,
            Author = new GeneralUser { UserId = _authorId },
            Text = "Original text"
        };
        UpdateWebsiteTestimonialEntity? written = null;
        _repository.Get(testimonialId).Returns(testimonial);
        _repository.Update(Arg.Any<UpdateWebsiteTestimonialEntity>())
            .Returns(testimonial)
            .AndDoes(ci =>
            {
                var e = ci.ArgAt<UpdateWebsiteTestimonialEntity>(0);
                written = e;
            });

        await _service.UpdateAsync(new UpdateWebsiteTestimonial { Id = testimonialId, Text = "Updated text" });

        written.Should().NotBeNull();
        written!.ModifiedByUserId.Should().Be(_currentUserId);
        written.Text.Should().Be("Updated text");
    }

    [Fact]
    public async Task UpdateTestimonialSuccessfully()
    {
        var testimonialId = Guid.NewGuid();
        var existingTestimonial = new WebsiteTestimonial
        {
            Id = testimonialId,
            Author = new GeneralUser { UserId = _currentUserId },
            Text = "Original text"
        };
        var updatedTestimonial = new WebsiteTestimonial
        {
            Id = testimonialId,
            Author = new GeneralUser { UserId = _currentUserId },
            Text = "Updated text"
        };
        _repository.Get(testimonialId).Returns(existingTestimonial);
        _repository.Update(Arg.Any<UpdateWebsiteTestimonialEntity>()).Returns(updatedTestimonial);

        var result = await _service.UpdateAsync(new UpdateWebsiteTestimonial { Id = testimonialId, Text = "Updated text" });

        result.Should().Be(updatedTestimonial);
        await _repository.Received(1).Update(Arg.Any<UpdateWebsiteTestimonialEntity>());
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task AuthorizeDeleteAction()
    {
        var testimonialId = Guid.NewGuid();
        var testimonial = new WebsiteTestimonial
        {
            Id = testimonialId,
            Author = new GeneralUser { UserId = _currentUserId },
            Text = "Some text"
        };
        _repository.Get(testimonialId).Returns(testimonial);

        await _service.DeleteAsync(testimonialId);

        _intentionManager.Received(1).ThrowIfForbidden(WebsiteTestimonialIntention.Delete, testimonial);
    }

    [Fact]
    public async Task DeleteTestimonialSuccessfully()
    {
        var testimonialId = Guid.NewGuid();
        var testimonial = new WebsiteTestimonial
        {
            Id = testimonialId,
            Author = new GeneralUser { UserId = _currentUserId },
            Text = "Some text"
        };
        _repository.Get(testimonialId).Returns(testimonial);

        await _service.DeleteAsync(testimonialId);

        await _repository.Received(1).Delete(testimonialId, _currentUserId);
    }

    #endregion
}
