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
using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using Xunit;

namespace DM.Domain.Community.Tests.Features.UserEndorsements;

public class UserEndorsementServiceShould : UnitTestBase
{
    private readonly IValidator<CreateUserEndorsement> _createValidator;
    private readonly IValidator<UpdateUserEndorsement> _updateValidator;
    private readonly IIntentionManager _intentionManager;
    private readonly IUserEndorsementRepository _repository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly UserEndorsementService _service;
    private readonly Guid _currentUserId;

    public UserEndorsementServiceShould()
    {
        _createValidator = Mock<IValidator<CreateUserEndorsement>>();
        _createValidator.ValidateAsync(Arg.Any<CreateUserEndorsement>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        _updateValidator = Mock<IValidator<UpdateUserEndorsement>>();
        _updateValidator.ValidateAsync(Arg.Any<UpdateUserEndorsement>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        _intentionManager = Mock<IIntentionManager>();

        _repository = Mock<IUserEndorsementRepository>();

        _currentUserId = Guid.NewGuid();
        _identityProvider = Mock<IIdentityProvider>();
        _identityProvider.Current.Returns(Identities.User(_currentUserId, UserRole.RegularUser));

        _guidFactory = Mock<IGuidFactory>();
        _guidFactory.Create().Returns(Guid.NewGuid());

        _dateTimeProvider = Mock<IDateTimeProvider>();
        _dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        _service = new UserEndorsementService(
            _createValidator,
            _updateValidator,
            _intentionManager,
            _repository,
            _identityProvider,
            _guidFactory,
            _dateTimeProvider);
    }

    #region Create Tests

    [Fact]
    public async Task AuthorizeCreateAction()
    {
        var targetUserId = Guid.NewGuid();
        SetupSuccessfulCreate(targetUserId);

        await _service.CreateAsync(new CreateUserEndorsement { TargetUserId = targetUserId, Text = "Great player!" });

        _intentionManager.Received(1).ThrowIfForbidden(UserEndorsementIntention.Create);
    }

    [Fact]
    public async Task ThrowForbiddenWhenNewbieTryingToCreateEndorsement()
    {
        var targetUserId = Guid.NewGuid();
        _repository.GetUserPostCountAsync(_currentUserId).Returns(50); // Newbie

        var act = async () => await _service.CreateAsync(new CreateUserEndorsement { TargetUserId = targetUserId, Text = "Great player!" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ThrowForbiddenWhenEndorsingYourself()
    {
        _repository.GetUserPostCountAsync(_currentUserId).Returns(200);

        var act = async () => await _service.CreateAsync(new CreateUserEndorsement { TargetUserId = _currentUserId, Text = "I'm great!" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ThrowForbiddenWhenUsersHaveNotPlayedTogether()
    {
        var targetUserId = Guid.NewGuid();
        _repository.GetUserPostCountAsync(_currentUserId).Returns(200);
        _repository.HavePlayedTogetherAsync(_currentUserId, targetUserId).Returns(false);

        var act = async () => await _service.CreateAsync(new CreateUserEndorsement { TargetUserId = targetUserId, Text = "Great player!" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ThrowConflictWhenEndorsementAlreadyExists()
    {
        var targetUserId = Guid.NewGuid();
        _repository.GetUserPostCountAsync(_currentUserId).Returns(200);
        _repository.HavePlayedTogetherAsync(_currentUserId, targetUserId).Returns(true);
        _repository.ExistsAsync(_currentUserId, targetUserId).Returns(true);

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
        await _repository.Received(1).CreateAsync(Arg.Any<CreateUserEndorsementEntity>());
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
        await _repository.DidNotReceive().GetUserPostCountAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task RefuseEligibilityForNewbie()
    {
        var targetUserId = Guid.NewGuid();
        AllowCreateIntention();
        _repository.GetUserPostCountAsync(_currentUserId).Returns(ProbationPolicy.NewbiePostThreshold - 1);

        var eligibility = await _service.GetEligibilityAsync(targetUserId);

        eligibility.CanCreate.Should().BeFalse();
        eligibility.Reason.Should().Contain(ProbationPolicy.NewbiePostThreshold.ToString());
    }

    [Fact]
    public async Task RefuseEligibilityWhenUsersHaveNotPlayedTogether()
    {
        var targetUserId = Guid.NewGuid();
        AllowCreateIntention();
        _repository.GetUserPostCountAsync(_currentUserId).Returns(200);
        _repository.HavePlayedTogetherAsync(_currentUserId, targetUserId).Returns(false);

        var eligibility = await _service.GetEligibilityAsync(targetUserId);

        eligibility.CanCreate.Should().BeFalse();
        eligibility.Reason.Should().Be("Рекомендовать можно только тех, с кем вы играли в одной игре");
    }

    [Fact]
    public async Task RefuseEligibilityWhenPairAlreadyHasOne()
    {
        var targetUserId = Guid.NewGuid();
        AllowCreateIntention();
        _repository.GetUserPostCountAsync(_currentUserId).Returns(200);
        _repository.HavePlayedTogetherAsync(_currentUserId, targetUserId).Returns(true);
        _repository.ExistsAsync(_currentUserId, targetUserId).Returns(true);

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
        _repository.GetUserPostCountAsync(_currentUserId)
            .Returns(isNewbie ? ProbationPolicy.NewbiePostThreshold - 1 : 200);
        _repository.HavePlayedTogetherAsync(_currentUserId, targetUserId).Returns(havePlayedTogether);
        _repository.ExistsAsync(_currentUserId, targetUserId).Returns(alreadyExists);

        var eligibility = await _service.GetEligibilityAsync(targetUserId);
        var act = async () => await _service.CreateAsync(
            new CreateUserEndorsement { TargetUserId = targetUserId, Text = "Great player!" });

        eligibility.CanCreate.Should().BeFalse();
        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message == eligibility.Reason && e.StatusCode == eligibility.Status);
        await _repository.DidNotReceive().CreateAsync(Arg.Any<CreateUserEndorsementEntity>());
    }

    /// <summary>
    /// The intention resolver admits Create to anyone signed in, so the mock
    /// stands in for that and the eligibility rules are what is under test.
    /// </summary>
    private void AllowCreateIntention() =>
        _intentionManager.IsAllowed(UserEndorsementIntention.Create).Returns(true);

    #endregion

    #region Get Tests

    [Fact]
    public async Task ThrowNotFoundWhenEndorsementDoesNotExist()
    {
        var endorsementId = Guid.NewGuid();
        _repository.GetAsync(endorsementId).Returns((UserEndorsement?)null);

        var act = async () => await _service.GetAsync(endorsementId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReturnEndorsementWhenExists()
    {
        var endorsementId = Guid.NewGuid();
        var endorsement = new UserEndorsement { Id = endorsementId, Text = "Great player!" };
        _repository.GetAsync(endorsementId).Returns(endorsement);

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
        _repository.GetAsync(endorsementId).Returns(endorsement);
        _repository.UpdateAsync(Arg.Any<UpdateUserEndorsementEntity>()).Returns(endorsement);

        await _service.UpdateAsync(new UpdateUserEndorsement { EndorsementId = endorsementId, Text = "Updated text" });

        _intentionManager.Received(1).ThrowIfForbidden(UserEndorsementIntention.Edit, endorsement);
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
        _repository.GetAsync(endorsementId).Returns(endorsement);

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
        _repository.GetAsync(endorsementId).Returns(endorsement);

        var result = await _service.UpdateAsync(new UpdateUserEndorsement { EndorsementId = endorsementId, Text = "" });

        result.Should().Be(endorsement);
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<UpdateUserEndorsementEntity>());
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
        _repository.GetAsync(endorsementId).Returns(endorsement);
        _repository.UpdateAsync(Arg.Any<UpdateUserEndorsementEntity>()).Returns(endorsement);

        await _service.DeleteAsync(endorsementId);

        _intentionManager.Received(1).ThrowIfForbidden(UserEndorsementIntention.Delete, endorsement);
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
        _repository.GetAsync(endorsementId).Returns(endorsement);
        _repository.UpdateAsync(Arg.Any<UpdateUserEndorsementEntity>()).Returns(endorsement);

        await _service.DeleteAsync(endorsementId);

        await _repository.Received(1).UpdateAsync(Arg.Is<UpdateUserEndorsementEntity>(e => e.IsRemoved == true));
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
        _repository.HavePlayedTogetherAsync(_currentUserId, targetUserId).Returns(true);

        var result = await _service.HavePlayedTogetherAsync(_currentUserId, targetUserId);

        result.Should().BeTrue();
        await _repository.Received(1).HavePlayedTogetherAsync(_currentUserId, targetUserId);
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

        _repository.GetUserPostCountAsync(_currentUserId).Returns(200);
        _repository.HavePlayedTogetherAsync(_currentUserId, targetUserId).Returns(true);
        _repository.ExistsAsync(_currentUserId, targetUserId).Returns(false);
        _repository.CreateAsync(Arg.Any<CreateUserEndorsementEntity>()).Returns(expectedEndorsement);
    }
}
