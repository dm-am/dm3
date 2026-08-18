using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Community.Authorization;
using DM.Domain.Community.Features.UserEndorsements;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Testing.Dsl;
using DM.Testing;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Xunit;

namespace DM.Domain.Community.Tests.Features.UserEndorsements;

public class UserEndorsementServiceShould : UnitTestBase
{
    private readonly Mock<IValidator<CreateUserEndorsement>> _createValidator;
    private readonly Mock<IValidator<UpdateUserEndorsement>> _updateValidator;
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly Mock<IUserEndorsementRepository> _repository;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<IGuidFactory> _guidFactory;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly UserEndorsementService _service;
    private readonly Guid _currentUserId;

    public UserEndorsementServiceShould()
    {
        _createValidator = Mock<IValidator<CreateUserEndorsement>>();
        _createValidator.Setup(v => v.ValidateAsync(It.IsAny<CreateUserEndorsement>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _updateValidator = Mock<IValidator<UpdateUserEndorsement>>();
        _updateValidator.Setup(v => v.ValidateAsync(It.IsAny<UpdateUserEndorsement>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _intentionManager = Mock<IIntentionManager>();

        _repository = Mock<IUserEndorsementRepository>();

        _currentUserId = Guid.NewGuid();
        _identityProvider = Mock<IIdentityProvider>();
        _identityProvider.Setup(p => p.Current).Returns(Identities.User(_currentUserId, UserRole.RegularUser));

        _guidFactory = Mock<IGuidFactory>();
        _guidFactory.Setup(g => g.Create()).Returns(Guid.NewGuid());

        _dateTimeProvider = Mock<IDateTimeProvider>();
        _dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);

        _service = new UserEndorsementService(
            _createValidator.Object,
            _updateValidator.Object,
            _intentionManager.Object,
            _repository.Object,
            _identityProvider.Object,
            _guidFactory.Object,
            _dateTimeProvider.Object);
    }

    #region Create Tests

    [Fact]
    public async Task AuthorizeCreateAction()
    {
        var targetUserId = Guid.NewGuid();
        SetupSuccessfulCreate(targetUserId);

        await _service.CreateAsync(new CreateUserEndorsement { TargetUserId = targetUserId, Text = "Great player!" });

        _intentionManager.Verify(m => m.ThrowIfForbidden(UserEndorsementIntention.Create), Times.Once);
    }

    [Fact]
    public async Task ThrowForbiddenWhenNewbieTryingToCreateEndorsement()
    {
        var targetUserId = Guid.NewGuid();
        _repository.Setup(r => r.GetUserPostCountAsync(_currentUserId)).ReturnsAsync(50); // Newbie

        var act = async () => await _service.CreateAsync(new CreateUserEndorsement { TargetUserId = targetUserId, Text = "Great player!" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ThrowForbiddenWhenEndorsingYourself()
    {
        _repository.Setup(r => r.GetUserPostCountAsync(_currentUserId)).ReturnsAsync(200);

        var act = async () => await _service.CreateAsync(new CreateUserEndorsement { TargetUserId = _currentUserId, Text = "I'm great!" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ThrowForbiddenWhenUsersHaveNotPlayedTogether()
    {
        var targetUserId = Guid.NewGuid();
        _repository.Setup(r => r.GetUserPostCountAsync(_currentUserId)).ReturnsAsync(200);
        _repository.Setup(r => r.HavePlayedTogetherAsync(_currentUserId, targetUserId)).ReturnsAsync(false);

        var act = async () => await _service.CreateAsync(new CreateUserEndorsement { TargetUserId = targetUserId, Text = "Great player!" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ThrowConflictWhenEndorsementAlreadyExists()
    {
        var targetUserId = Guid.NewGuid();
        _repository.Setup(r => r.GetUserPostCountAsync(_currentUserId)).ReturnsAsync(200);
        _repository.Setup(r => r.HavePlayedTogetherAsync(_currentUserId, targetUserId)).ReturnsAsync(true);
        _repository.Setup(r => r.ExistsAsync(_currentUserId, targetUserId)).ReturnsAsync(true);

        var act = async () => await _service.CreateAsync(new CreateUserEndorsement { TargetUserId = targetUserId, Text = "Great player!" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateEndorsementSuccessfully()
    {
        var targetUserId = Guid.NewGuid();
        var expectedEndorsement = new UserEndorsement
        {
            Id = Guid.NewGuid(),
            TargetUser = new GeneralUser { UserId = targetUserId },
            Text = "Great player!"
        };
        SetupSuccessfulCreate(targetUserId, expectedEndorsement);

        var result = await _service.CreateAsync(new CreateUserEndorsement { TargetUserId = targetUserId, Text = "Great player!" });

        result.Should().Be(expectedEndorsement);
        _repository.Verify(r => r.CreateAsync(It.IsAny<CreateUserEndorsementEntity>()), Times.Once);
    }

    #endregion

    #region Eligibility Tests

    // The write form asks this before it draws anything, so every answer here
    // is a control the site does or does not offer. The refusal sentences are
    // asserted verbatim: they are what the reader is shown instead of the form.

    [Fact]
    public async Task AllowEligibilityWhenEveryRuleIsSatisfied()
    {
        var targetUserId = Guid.NewGuid();
        AllowCreateIntention();
        SetupSuccessfulCreate(targetUserId);

        var eligibility = await _service.GetEligibilityAsync(targetUserId);

        eligibility.CanCreate.Should().BeTrue();
        eligibility.Reason.Should().BeNull();
    }

    [Fact]
    public async Task RefuseEligibilityForGuest()
    {
        // IsAllowed is false by default on the mock — the anonymous case.
        var eligibility = await _service.GetEligibilityAsync(Guid.NewGuid());

        eligibility.CanCreate.Should().BeFalse();
        eligibility.Reason.Should().Be(RefusalMessage.AuthenticationRequired);
        eligibility.Status.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RefuseEligibilityForYourself()
    {
        AllowCreateIntention();

        var eligibility = await _service.GetEligibilityAsync(_currentUserId);

        eligibility.CanCreate.Should().BeFalse();
        eligibility.Reason.Should().Be("Нельзя рекомендовать самого себя");
        eligibility.Status.Should().Be(HttpStatusCode.Forbidden);
        // The refusal is free: a self-recommendation is not worth a query.
        _repository.Verify(r => r.GetUserPostCountAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task RefuseEligibilityForNewbie()
    {
        var targetUserId = Guid.NewGuid();
        AllowCreateIntention();
        _repository.Setup(r => r.GetUserPostCountAsync(_currentUserId))
            .ReturnsAsync(ProbationPolicy.NewbiePostThreshold - 1);

        var eligibility = await _service.GetEligibilityAsync(targetUserId);

        eligibility.CanCreate.Should().BeFalse();
        eligibility.Reason.Should().Contain(ProbationPolicy.NewbiePostThreshold.ToString());
    }

    [Fact]
    public async Task RefuseEligibilityWhenUsersHaveNotPlayedTogether()
    {
        var targetUserId = Guid.NewGuid();
        AllowCreateIntention();
        _repository.Setup(r => r.GetUserPostCountAsync(_currentUserId)).ReturnsAsync(200);
        _repository.Setup(r => r.HavePlayedTogetherAsync(_currentUserId, targetUserId)).ReturnsAsync(false);

        var eligibility = await _service.GetEligibilityAsync(targetUserId);

        eligibility.CanCreate.Should().BeFalse();
        eligibility.Reason.Should().Be("Рекомендовать можно только тех, с кем вы играли в одной игре");
    }

    [Fact]
    public async Task RefuseEligibilityWhenPairAlreadyHasOne()
    {
        var targetUserId = Guid.NewGuid();
        AllowCreateIntention();
        _repository.Setup(r => r.GetUserPostCountAsync(_currentUserId)).ReturnsAsync(200);
        _repository.Setup(r => r.HavePlayedTogetherAsync(_currentUserId, targetUserId)).ReturnsAsync(true);
        _repository.Setup(r => r.ExistsAsync(_currentUserId, targetUserId)).ReturnsAsync(true);

        var eligibility = await _service.GetEligibilityAsync(targetUserId);

        eligibility.CanCreate.Should().BeFalse();
        eligibility.Reason.Should().Be(RefusalMessage.AlreadyEndorsedUser);
        eligibility.Status.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>
    /// The whole point of the shared evaluation: whatever the query refuses
    /// with is what the create call refuses with, word for word and status for
    /// status. Two copies of the rule list would drift, and a client drawing
    /// its control on the query would then offer a rejected POST.
    /// </summary>
    [Theory]
    [InlineData(true, false, false)]   // newbie
    [InlineData(false, false, false)]  // never played together
    [InlineData(false, true, true)]    // pair already has one
    public async Task RefuseCreateWithTheSentenceTheEligibilityQueryGave(
        bool isNewbie, bool havePlayedTogether, bool alreadyExists)
    {
        var targetUserId = Guid.NewGuid();
        AllowCreateIntention();
        _repository.Setup(r => r.GetUserPostCountAsync(_currentUserId))
            .ReturnsAsync(isNewbie ? ProbationPolicy.NewbiePostThreshold - 1 : 200);
        _repository.Setup(r => r.HavePlayedTogetherAsync(_currentUserId, targetUserId))
            .ReturnsAsync(havePlayedTogether);
        _repository.Setup(r => r.ExistsAsync(_currentUserId, targetUserId)).ReturnsAsync(alreadyExists);

        var eligibility = await _service.GetEligibilityAsync(targetUserId);
        var act = async () => await _service.CreateAsync(
            new CreateUserEndorsement { TargetUserId = targetUserId, Text = "Great player!" });

        eligibility.CanCreate.Should().BeFalse();
        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message == eligibility.Reason && e.StatusCode == eligibility.Status);
        _repository.Verify(r => r.CreateAsync(It.IsAny<CreateUserEndorsementEntity>()), Times.Never);
    }

    /// <summary>
    /// The intention resolver admits Create to anyone signed in, so the mock
    /// stands in for that and the eligibility rules are what is under test.
    /// </summary>
    private void AllowCreateIntention() =>
        _intentionManager.Setup(m => m.IsAllowed(UserEndorsementIntention.Create)).Returns(true);

    #endregion

    #region Get Tests

    [Fact]
    public async Task ThrowNotFoundWhenEndorsementDoesNotExist()
    {
        var endorsementId = Guid.NewGuid();
        _repository.Setup(r => r.GetAsync(endorsementId)).ReturnsAsync((UserEndorsement?)null);

        var act = async () => await _service.GetAsync(endorsementId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReturnEndorsementWhenExists()
    {
        var endorsementId = Guid.NewGuid();
        var endorsement = new UserEndorsement { Id = endorsementId, Text = "Great player!" };
        _repository.Setup(r => r.GetAsync(endorsementId)).ReturnsAsync(endorsement);

        var result = await _service.GetAsync(endorsementId);

        result.Should().Be(endorsement);
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task AuthorizeEditAction()
    {
        var endorsementId = Guid.NewGuid();
        var endorsement = new UserEndorsement
        {
            Id = endorsementId,
            Author = new GeneralUser { UserId = _currentUserId },
            TargetUser = new GeneralUser { UserId = Guid.NewGuid() },
            Text = "Original text",
            CreatedUtc = DateTimeOffset.UtcNow
        };
        _repository.Setup(r => r.GetAsync(endorsementId)).ReturnsAsync(endorsement);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<UpdateUserEndorsementEntity>())).ReturnsAsync(endorsement);

        await _service.UpdateAsync(new UpdateUserEndorsement { EndorsementId = endorsementId, Text = "Updated text" });

        _intentionManager.Verify(m => m.ThrowIfForbidden(UserEndorsementIntention.Edit, endorsement), Times.Once);
    }

    [Fact]
    public async Task ThrowForbiddenWhenEditWindowExpired()
    {
        var endorsementId = Guid.NewGuid();
        var endorsement = new UserEndorsement
        {
            Id = endorsementId,
            Author = new GeneralUser { UserId = _currentUserId },
            TargetUser = new GeneralUser { UserId = Guid.NewGuid() },
            Text = "Original text",
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-2) // Past edit window
        };
        _repository.Setup(r => r.GetAsync(endorsementId)).ReturnsAsync(endorsement);

        var act = async () => await _service.UpdateAsync(new UpdateUserEndorsement { EndorsementId = endorsementId, Text = "Updated text" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReturnUnmodifiedEndorsementWhenTextIsEmpty()
    {
        var endorsementId = Guid.NewGuid();
        var endorsement = new UserEndorsement
        {
            Id = endorsementId,
            Author = new GeneralUser { UserId = _currentUserId },
            TargetUser = new GeneralUser { UserId = Guid.NewGuid() },
            Text = "Original text",
            CreatedUtc = DateTimeOffset.UtcNow
        };
        _repository.Setup(r => r.GetAsync(endorsementId)).ReturnsAsync(endorsement);

        var result = await _service.UpdateAsync(new UpdateUserEndorsement { EndorsementId = endorsementId, Text = "" });

        result.Should().Be(endorsement);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<UpdateUserEndorsementEntity>()), Times.Never);
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task AuthorizeDeleteAction()
    {
        var endorsementId = Guid.NewGuid();
        var endorsement = new UserEndorsement
        {
            Id = endorsementId,
            Author = new GeneralUser { UserId = _currentUserId },
            TargetUser = new GeneralUser { UserId = Guid.NewGuid() },
            Text = "Some text"
        };
        _repository.Setup(r => r.GetAsync(endorsementId)).ReturnsAsync(endorsement);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<UpdateUserEndorsementEntity>())).ReturnsAsync(endorsement);

        await _service.DeleteAsync(endorsementId);

        _intentionManager.Verify(m => m.ThrowIfForbidden(UserEndorsementIntention.Delete, endorsement), Times.Once);
    }

    [Fact]
    public async Task SoftDeleteEndorsement()
    {
        var endorsementId = Guid.NewGuid();
        var endorsement = new UserEndorsement
        {
            Id = endorsementId,
            Author = new GeneralUser { UserId = _currentUserId },
            TargetUser = new GeneralUser { UserId = Guid.NewGuid() },
            Text = "Some text"
        };
        _repository.Setup(r => r.GetAsync(endorsementId)).ReturnsAsync(endorsement);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<UpdateUserEndorsementEntity>())).ReturnsAsync(endorsement);

        await _service.DeleteAsync(endorsementId);

        _repository.Verify(r => r.UpdateAsync(It.Is<UpdateUserEndorsementEntity>(e => e.IsRemoved == true)), Times.Once);
    }

    #endregion

    #region CanEdit Tests

    [Fact]
    public void ReturnTrueWhenWithinEditWindow()
    {
        var endorsement = new UserEndorsement { CreatedUtc = DateTimeOffset.UtcNow.AddHours(-12) };

        var result = _service.CanEdit(endorsement);

        result.Should().BeTrue();
    }

    [Fact]
    public void ReturnFalseWhenOutsideEditWindow()
    {
        var endorsement = new UserEndorsement { CreatedUtc = DateTimeOffset.UtcNow.AddDays(-2) };

        var result = _service.CanEdit(endorsement);

        result.Should().BeFalse();
    }

    #endregion

    #region HavePlayedTogether Tests

    [Fact]
    public async Task DelegateHavePlayedTogetherToRepository()
    {
        var targetUserId = Guid.NewGuid();
        _repository.Setup(r => r.HavePlayedTogetherAsync(_currentUserId, targetUserId)).ReturnsAsync(true);

        var result = await _service.HavePlayedTogetherAsync(_currentUserId, targetUserId);

        result.Should().BeTrue();
        _repository.Verify(r => r.HavePlayedTogetherAsync(_currentUserId, targetUserId), Times.Once);
    }

    #endregion

    private void SetupSuccessfulCreate(Guid targetUserId, UserEndorsement? expectedEndorsement = null)
    {
        expectedEndorsement ??= new UserEndorsement
        {
            Id = Guid.NewGuid(),
            TargetUser = new GeneralUser { UserId = targetUserId },
            Text = "Great player!"
        };

        _repository.Setup(r => r.GetUserPostCountAsync(_currentUserId)).ReturnsAsync(200);
        _repository.Setup(r => r.HavePlayedTogetherAsync(_currentUserId, targetUserId)).ReturnsAsync(true);
        _repository.Setup(r => r.ExistsAsync(_currentUserId, targetUserId)).ReturnsAsync(false);
        _repository.Setup(r => r.CreateAsync(It.IsAny<CreateUserEndorsementEntity>())).ReturnsAsync(expectedEndorsement);
    }
}
