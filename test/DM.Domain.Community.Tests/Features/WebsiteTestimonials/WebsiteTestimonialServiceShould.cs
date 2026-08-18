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
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Xunit;

namespace DM.Domain.Community.Tests.Features.WebsiteTestimonials;

public class WebsiteTestimonialServiceShould : UnitTestBase
{
    private readonly Mock<IValidator<CreateWebsiteTestimonial>> _createValidator;
    private readonly Mock<IValidator<UpdateWebsiteTestimonial>> _updateValidator;
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly Mock<IWebsiteTestimonialRepository> _repository;
    private readonly Mock<IUserReadRepository> _userRepository;
    private readonly Mock<IEventProducer> _eventProducer;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly Mock<IGuidFactory> _guidFactory;
    private readonly Mock<IIdentityProvider> _identityProvider;
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
        _createValidator.Setup(v => v.ValidateAsync(It.IsAny<CreateWebsiteTestimonial>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _updateValidator = Mock<IValidator<UpdateWebsiteTestimonial>>();
        _updateValidator.Setup(v => v.ValidateAsync(It.IsAny<UpdateWebsiteTestimonial>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _intentionManager = Mock<IIntentionManager>();

        _repository = Mock<IWebsiteTestimonialRepository>();

        _userRepository = Mock<IUserReadRepository>();
        _userRepository.Setup(r => r.FindUserIdAsync(AuthorUsername)).ReturnsAsync(_authorId);

        _eventProducer = Mock<IEventProducer>();

        _currentUserId = Guid.NewGuid();
        _identityProvider = Mock<IIdentityProvider>();
        _identityProvider.Setup(p => p.Current).Returns(Identities.User(_currentUserId, UserRole.RegularUser));

        _guidFactory = Mock<IGuidFactory>();
        _guidFactory.Setup(g => g.Create()).Returns(Guid.NewGuid());

        _dateTimeProvider = Mock<IDateTimeProvider>();
        _dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);

        _service = new WebsiteTestimonialService(
            _createValidator.Object,
            _updateValidator.Object,
            _intentionManager.Object,
            _repository.Object,
            _userRepository.Object,
            _eventProducer.Object,
            _dateTimeProvider.Object,
            _guidFactory.Object,
            _identityProvider.Object);
    }

    #region Create Tests

    [Fact]
    public async Task AuthorizeCreateAction()
    {
        var expectedTestimonial = new WebsiteTestimonial { Id = Guid.NewGuid(), Text = "Great website!" };
        _repository.Setup(r => r.GetByAuthor(_authorId)).ReturnsAsync((WebsiteTestimonial?)null);
        _repository.Setup(r => r.Create(It.IsAny<CreateWebsiteTestimonialEntity>())).ReturnsAsync(expectedTestimonial);

        await _service.CreateAsync(NewTestimonial());

        _intentionManager.Verify(m => m.ThrowIfForbidden(WebsiteTestimonialIntention.Create), Times.Once);
    }

    /// <summary>
    /// The whole point of the named author: the entry is signed by the
    /// participant, not by the moderator who filled the form in.
    /// </summary>
    [Fact]
    public async Task SignTheTestimonialWithTheNamedParticipant()
    {
        CreateWebsiteTestimonialEntity? written = null;
        _repository.Setup(r => r.GetByAuthor(_authorId)).ReturnsAsync((WebsiteTestimonial?)null);
        _repository.Setup(r => r.Create(It.IsAny<CreateWebsiteTestimonialEntity>()))
            .Callback<CreateWebsiteTestimonialEntity>(e => written = e)
            .ReturnsAsync(new WebsiteTestimonial { Id = Guid.NewGuid(), Text = "Great website!" });

        await _service.CreateAsync(NewTestimonial());

        written.Should().NotBeNull();
        written!.AuthorId.Should().Be(_authorId);
        written.AuthorId.Should().NotBe(_currentUserId);
    }

    [Fact]
    public async Task ThrowNotFoundWhenTheNamedAuthorDoesNotExist()
    {
        _userRepository.Setup(r => r.FindUserIdAsync("Nobody")).ReturnsAsync((Guid?)null);

        var act = async () => await _service.CreateAsync(
            new CreateWebsiteTestimonial { AuthorUsername = "Nobody", Text = "Great website!" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
        _repository.Verify(r => r.Create(It.IsAny<CreateWebsiteTestimonialEntity>()), Times.Never);
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
        _repository.Setup(r => r.GetByAuthor(_authorId)).ReturnsAsync(existingTestimonial);

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
        _repository.Setup(r => r.GetByAuthor(_currentUserId)).ReturnsAsync(submittersOwn);
        _repository.Setup(r => r.GetByAuthor(_authorId)).ReturnsAsync((WebsiteTestimonial?)null);
        _repository.Setup(r => r.Create(It.IsAny<CreateWebsiteTestimonialEntity>()))
            .ReturnsAsync(new WebsiteTestimonial { Id = Guid.NewGuid(), Text = "Great website!" });

        await _service.CreateAsync(NewTestimonial());

        _repository.Verify(r => r.Create(It.IsAny<CreateWebsiteTestimonialEntity>()), Times.Once);
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
        _repository.Setup(r => r.GetByAuthor(_authorId)).ReturnsAsync((WebsiteTestimonial?)null);
        _repository.Setup(r => r.Create(It.IsAny<CreateWebsiteTestimonialEntity>())).ReturnsAsync(expectedTestimonial);

        var result = await _service.CreateAsync(NewTestimonial());

        result.Should().Be(expectedTestimonial);
        _repository.Verify(r => r.Create(It.IsAny<CreateWebsiteTestimonialEntity>()), Times.Once);
    }

    [Fact]
    public async Task SendEventOnCreate()
    {
        var testimonialId = Guid.NewGuid();
        var expectedTestimonial = new WebsiteTestimonial { Id = testimonialId, Text = "Great website!" };
        _repository.Setup(r => r.GetByAuthor(_authorId)).ReturnsAsync((WebsiteTestimonial?)null);
        _repository.Setup(r => r.Create(It.IsAny<CreateWebsiteTestimonialEntity>())).ReturnsAsync(expectedTestimonial);

        await _service.CreateAsync(NewTestimonial());

        _eventProducer.Verify(e => e.SendAsync(EventType.NewWebsiteTestimonial, testimonialId), Times.Once);
    }

    #endregion

    #region Get Tests

    [Fact]
    public async Task ThrowNotFoundWhenTestimonialDoesNotExist()
    {
        var testimonialId = Guid.NewGuid();
        _repository.Setup(r => r.Get(testimonialId)).ReturnsAsync((WebsiteTestimonial?)null);

        var act = async () => await _service.GetAsync(testimonialId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReturnTestimonialWhenExists()
    {
        var testimonialId = Guid.NewGuid();
        var testimonial = new WebsiteTestimonial { Id = testimonialId, Text = "Great website!" };
        _repository.Setup(r => r.Get(testimonialId)).ReturnsAsync(testimonial);

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
        _repository.Setup(r => r.Get(testimonialId)).ReturnsAsync(testimonial);
        _repository.Setup(r => r.Update(It.IsAny<UpdateWebsiteTestimonialEntity>())).ReturnsAsync(testimonial);

        await _service.UpdateAsync(new UpdateWebsiteTestimonial { Id = testimonialId, Text = "Updated text" });

        _intentionManager.Verify(m => m.ThrowIfForbidden(WebsiteTestimonialIntention.Edit, testimonial), Times.Once);
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
        _repository.Setup(r => r.Get(testimonialId)).ReturnsAsync(testimonial);
        _intentionManager
            .Setup(m => m.ThrowIfForbidden(WebsiteTestimonialIntention.Edit, testimonial))
            .Throws(new IntentionManagerException(
                Create.User(Guid.NewGuid()).WithRole(UserRole.Moderator).Please(),
                WebsiteTestimonialIntention.Edit,
                testimonial));

        var act = async () => await _service.UpdateAsync(
            new UpdateWebsiteTestimonial { Id = testimonialId, Text = "Updated text" });

        await act.Should().ThrowAsync<IntentionManagerException>();
        _repository.Verify(r => r.Update(It.IsAny<UpdateWebsiteTestimonialEntity>()), Times.Never);
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
        _repository.Setup(r => r.Get(testimonialId)).ReturnsAsync(testimonial);
        _repository.Setup(r => r.Update(It.IsAny<UpdateWebsiteTestimonialEntity>()))
            .Callback<UpdateWebsiteTestimonialEntity>(e => written = e)
            .ReturnsAsync(testimonial);

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
        _repository.Setup(r => r.Get(testimonialId)).ReturnsAsync(existingTestimonial);
        _repository.Setup(r => r.Update(It.IsAny<UpdateWebsiteTestimonialEntity>())).ReturnsAsync(updatedTestimonial);

        var result = await _service.UpdateAsync(new UpdateWebsiteTestimonial { Id = testimonialId, Text = "Updated text" });

        result.Should().Be(updatedTestimonial);
        _repository.Verify(r => r.Update(It.IsAny<UpdateWebsiteTestimonialEntity>()), Times.Once);
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
        _repository.Setup(r => r.Get(testimonialId)).ReturnsAsync(testimonial);

        await _service.DeleteAsync(testimonialId);

        _intentionManager.Verify(m => m.ThrowIfForbidden(WebsiteTestimonialIntention.Delete, testimonial), Times.Once);
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
        _repository.Setup(r => r.Get(testimonialId)).ReturnsAsync(testimonial);

        await _service.DeleteAsync(testimonialId);

        _repository.Verify(r => r.Delete(testimonialId, _currentUserId), Times.Once);
    }

    #endregion
}
