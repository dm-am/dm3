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
    private readonly IValidator<CreateUserEndorsement> createValidator;
    private readonly IValidator<UpdateUserEndorsement> updateValidator;
    private readonly IIntentionManager intentionManager;
    private readonly IUserEndorsementRepository repository;
    private readonly IIdentityProvider identityProvider;
    private readonly IGuidFactory guidFactory;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly UserEndorsementService service;
    private readonly Guid currentUserId;
    private readonly DateTimeOffset now = DateTimeOffset.UtcNow;

    public UserEndorsementServiceShould()
    {
        createValidator = Mock<IValidator<CreateUserEndorsement>>();
        createValidator.ValidateAsync(Arg.Any<CreateUserEndorsement>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        updateValidator = Mock<IValidator<UpdateUserEndorsement>>();
        updateValidator.ValidateAsync(Arg.Any<UpdateUserEndorsement>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        intentionManager = Mock<IIntentionManager>();

        repository = Mock<IUserEndorsementRepository>();

        currentUserId = Guid.NewGuid();
        identityProvider = Mock<IIdentityProvider>();
        identityProvider.Current.Returns(Identities.User(currentUserId, UserRole.RegularUser));

        guidFactory = Mock<IGuidFactory>();
        guidFactory.Create().Returns(Guid.NewGuid());

        dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Now.Returns(now);

        service = new UserEndorsementService(
            createValidator,
            updateValidator,
            intentionManager,
            repository,
            identityProvider,
            guidFactory,
            dateTimeProvider);
    }

    [Fact]
    public async Task AuthorizeCreateAction()
    {
        var targetUserId = Guid.NewGuid();
        SetupSuccessfulCreate(targetUserId);

        await service.CreateAsync(new CreateUserEndorsement { TargetUserId = targetUserId, Text = "Great player!" });

        intentionManager.Received(1).ThrowIfForbidden(UserEndorsementIntention.Create);
    }

    [Fact]
    public async Task ThrowForbiddenWhenEndorsingYourself()
    {
        repository.GetUserPostCountAsync(currentUserId).Returns(ProbationPolicy.NewbiePostThreshold);

        var act = async () => await service.CreateAsync(new CreateUserEndorsement { TargetUserId = currentUserId, Text = "I'm great!" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
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

        var result = await service.CreateAsync(new CreateUserEndorsement { TargetUserId = targetUserId, Text = "Great player!" });

        result.Should().Be(expectedEndorsement);
        await repository.Received(1).CreateAsync(Arg.Any<CreateUserEndorsementEntity>());
    }

    // The write form asks this before it draws anything, so every answer here
    // is a control the site does or does not offer. The refusal sentences are
    // asserted verbatim: they are what the reader is shown instead of the form.

    [Fact]
    public async Task AllowEligibilityWhenEveryRuleIsSatisfied()
    {
        var targetUserId = Guid.NewGuid();
        AllowCreateIntention();
        SetupSuccessfulCreate(targetUserId);

        var eligibility = await service.GetEligibilityAsync(targetUserId);

        eligibility.CanCreate.Should().BeTrue();
        eligibility.Reason.Should().BeNull();
    }

    [Fact]
    public async Task RefuseEligibilityForGuest()
    {
        // IsAllowed is false by default on the mock — the anonymous case.
        var eligibility = await service.GetEligibilityAsync(Guid.NewGuid());

        eligibility.CanCreate.Should().BeFalse();
        eligibility.Reason.Should().Be(RefusalMessage.AuthenticationRequired);
        eligibility.Status.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RefuseEligibilityForYourself()
    {
        AllowCreateIntention();

        var eligibility = await service.GetEligibilityAsync(currentUserId);

        eligibility.CanCreate.Should().BeFalse();
        eligibility.Reason.Should().Be("Нельзя рекомендовать самого себя");
        eligibility.Status.Should().Be(HttpStatusCode.Forbidden);
        // The refusal is free: a self-recommendation is not worth a query.
        await repository.DidNotReceive().GetUserPostCountAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task RefuseEligibilityForNewbie()
    {
        var targetUserId = Guid.NewGuid();
        AllowCreateIntention();
        repository.GetUserPostCountAsync(currentUserId).Returns(ProbationPolicy.NewbiePostThreshold - 1);

        var eligibility = await service.GetEligibilityAsync(targetUserId);

        eligibility.CanCreate.Should().BeFalse();
        eligibility.Reason.Should().Contain(ProbationPolicy.NewbiePostThreshold.ToString());
    }

    [Fact]
    public async Task RefuseEligibilityWhenUsersHaveNotPlayedTogether()
    {
        var targetUserId = Guid.NewGuid();
        AllowCreateIntention();
        repository.GetUserPostCountAsync(currentUserId).Returns(ProbationPolicy.NewbiePostThreshold);
        repository.HavePlayedTogetherAsync(currentUserId, targetUserId).Returns(false);

        var eligibility = await service.GetEligibilityAsync(targetUserId);

        eligibility.CanCreate.Should().BeFalse();
        eligibility.Reason.Should().Be("Рекомендовать можно только тех, с кем вы играли в одной игре");
    }

    [Fact]
    public async Task RefuseEligibilityWhenPairAlreadyHasOne()
    {
        var targetUserId = Guid.NewGuid();
        AllowCreateIntention();
        repository.GetUserPostCountAsync(currentUserId).Returns(ProbationPolicy.NewbiePostThreshold);
        repository.HavePlayedTogetherAsync(currentUserId, targetUserId).Returns(true);
        repository.ExistsAsync(currentUserId, targetUserId).Returns(true);

        var eligibility = await service.GetEligibilityAsync(targetUserId);

        eligibility.CanCreate.Should().BeFalse();
        eligibility.Reason.Should().Be(RefusalMessage.AlreadyEndorsedUser);
        eligibility.Status.Should().Be(HttpStatusCode.Conflict);
    }

    // Two copies of the rule list would drift, and a client drawing its control on the query would
    // then offer a rejected POST.
    [Theory]
    [InlineData(true, false, false)]   // newbie
    [InlineData(false, false, false)]  // never played together
    [InlineData(false, true, true)]    // pair already has one
    public async Task RefuseCreateWithTheSentenceTheEligibilityQueryGave(
        bool isNewbie, bool havePlayedTogether, bool alreadyExists)
    {
        var targetUserId = Guid.NewGuid();
        AllowCreateIntention();
        repository.GetUserPostCountAsync(currentUserId)
            .Returns(isNewbie ? ProbationPolicy.NewbiePostThreshold - 1 : ProbationPolicy.NewbiePostThreshold);
        repository.HavePlayedTogetherAsync(currentUserId, targetUserId).Returns(havePlayedTogether);
        repository.ExistsAsync(currentUserId, targetUserId).Returns(alreadyExists);

        var eligibility = await service.GetEligibilityAsync(targetUserId);
        var act = async () => await service.CreateAsync(
            new CreateUserEndorsement { TargetUserId = targetUserId, Text = "Great player!" });

        eligibility.CanCreate.Should().BeFalse();
        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message == eligibility.Reason && e.StatusCode == eligibility.Status);
        await repository.DidNotReceive().CreateAsync(Arg.Any<CreateUserEndorsementEntity>());
    }

    // The intention resolver admits Create to anyone signed in, so the mock stands in for that and
    // the eligibility rules are what is under test.
    private void AllowCreateIntention() =>
        intentionManager.IsAllowed(UserEndorsementIntention.Create).Returns(true);

    [Fact]
    public async Task ThrowNotFoundWhenEndorsementDoesNotExist()
    {
        var endorsementId = Guid.NewGuid();
        repository.GetAsync(endorsementId).Returns((UserEndorsement?)null);

        var act = async () => await service.GetAsync(endorsementId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReturnEndorsementWhenExists()
    {
        var endorsementId = Guid.NewGuid();
        var endorsement = new UserEndorsement { Id = endorsementId, Text = "Great player!" };
        repository.GetAsync(endorsementId).Returns(endorsement);

        var result = await service.GetAsync(endorsementId);

        result.Should().Be(endorsement);
    }

    [Fact]
    public async Task AuthorizeEditAction()
    {
        var endorsementId = Guid.NewGuid();
        var endorsement = OwnEndorsement(endorsementId);
        repository.GetAsync(endorsementId).Returns(endorsement);
        repository.UpdateAsync(Arg.Any<UpdateUserEndorsementEntity>()).Returns(endorsement);

        await service.UpdateAsync(new UpdateUserEndorsement { EndorsementId = endorsementId, Text = "Updated text" });

        intentionManager.Received(1).ThrowIfForbidden(UserEndorsementIntention.Edit, endorsement);
    }

    [Fact]
    public async Task ThrowForbiddenWhenEditWindowExpired()
    {
        var endorsementId = Guid.NewGuid();
        var endorsement = OwnEndorsement(endorsementId, now.AddDays(-2));
        repository.GetAsync(endorsementId).Returns(endorsement);

        var act = async () => await service.UpdateAsync(new UpdateUserEndorsement { EndorsementId = endorsementId, Text = "Updated text" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReturnUnmodifiedEndorsementWhenTextIsEmpty()
    {
        var endorsementId = Guid.NewGuid();
        var endorsement = OwnEndorsement(endorsementId);
        repository.GetAsync(endorsementId).Returns(endorsement);

        var result = await service.UpdateAsync(new UpdateUserEndorsement { EndorsementId = endorsementId, Text = "" });

        result.Should().Be(endorsement);
        await repository.DidNotReceive().UpdateAsync(Arg.Any<UpdateUserEndorsementEntity>());
    }

    [Fact]
    public async Task AuthorizeDeleteAction()
    {
        var endorsementId = Guid.NewGuid();
        var endorsement = OwnEndorsement(endorsementId);
        repository.GetAsync(endorsementId).Returns(endorsement);
        repository.UpdateAsync(Arg.Any<UpdateUserEndorsementEntity>()).Returns(endorsement);

        await service.DeleteAsync(endorsementId);

        intentionManager.Received(1).ThrowIfForbidden(UserEndorsementIntention.Delete, endorsement);
    }

    [Fact]
    public async Task SoftDeleteEndorsement()
    {
        var endorsementId = Guid.NewGuid();
        var endorsement = OwnEndorsement(endorsementId);
        repository.GetAsync(endorsementId).Returns(endorsement);
        repository.UpdateAsync(Arg.Any<UpdateUserEndorsementEntity>()).Returns(endorsement);

        await service.DeleteAsync(endorsementId);

        await repository.Received(1).UpdateAsync(Arg.Is<UpdateUserEndorsementEntity>(e => e.IsRemoved == true));
    }

    [Fact]
    public async Task UpdateAnEndorsementWithinTheEditWindow()
    {
        var endorsementId = Guid.NewGuid();
        var endorsement = OwnEndorsement(endorsementId, now.AddHours(-12));
        repository.GetAsync(endorsementId).Returns(endorsement);
        repository.UpdateAsync(Arg.Any<UpdateUserEndorsementEntity>()).Returns(endorsement);

        await service.UpdateAsync(new UpdateUserEndorsement { EndorsementId = endorsementId, Text = "Updated text" });

        await repository.Received(1).UpdateAsync(Arg.Is<UpdateUserEndorsementEntity>(e => e.Text == "Updated text"));
    }

    private UserEndorsement OwnEndorsement(Guid id, DateTimeOffset? createdUtc = null) => new()
    {
        Id = id,
        Author = new GeneralUser { UserId = currentUserId },
        TargetUser = new GeneralUser { UserId = Guid.NewGuid() },
        Text = "Original text",
        CreatedUtc = createdUtc ?? now
    };

    private void SetupSuccessfulCreate(Guid targetUserId, UserEndorsement? expectedEndorsement = null)
    {
        expectedEndorsement ??= new UserEndorsement
        {
            Id = Guid.NewGuid(),
            TargetUser = new GeneralUser { UserId = targetUserId },
            Text = "Great player!"
        };

        repository.GetUserPostCountAsync(currentUserId).Returns(ProbationPolicy.NewbiePostThreshold);
        repository.HavePlayedTogetherAsync(currentUserId, targetUserId).Returns(true);
        repository.ExistsAsync(currentUserId, targetUserId).Returns(false);
        repository.CreateAsync(Arg.Any<CreateUserEndorsementEntity>()).Returns(expectedEndorsement);
    }
}
