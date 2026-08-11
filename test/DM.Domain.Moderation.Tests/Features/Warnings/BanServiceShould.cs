using FluentValidation.Results;
using FluentValidation;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Users;
using DM.Domain.Moderation.Features.Warnings;
using DM.Testing;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Warnings;

public class BanServiceShould : UnitTestBase
{
    private readonly Mock<IBanRepository> _banRepository;
    private readonly Mock<IUserLookupService> _userLookupService;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<IGuidFactory> _guidFactory;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly Mock<IEventProducer> _eventProducer;
    private readonly BanService _service;
    private readonly Guid _moderatorUserId = Guid.NewGuid();
    private readonly Guid _targetUserId = Guid.NewGuid();
    private readonly Guid _banId = Guid.NewGuid();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    public BanServiceShould()
    {
        _banRepository = Mock<IBanRepository>();
        _userLookupService = Mock<IUserLookupService>();
        _identityProvider = Mock<IIdentityProvider>();
        _guidFactory = Mock<IGuidFactory>();
        _dateTimeProvider = Mock<IDateTimeProvider>();
        _eventProducer = Mock<IEventProducer>();

        // Ban creation and lifting require SeniorModerator, so tests default to it
        SetCurrentUser(UserRole.SeniorModerator);
        _dateTimeProvider.Setup(d => d.Now).Returns(_now);
        _guidFactory.Setup(g => g.Create()).Returns(_banId);

        var createValidator = Mock<IValidator<CreateBan>>();
        createValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreateBan>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _service = new BanService(
            _banRepository.Object,
            _userLookupService.Object,
            _identityProvider.Object,
            _guidFactory.Object,
            _dateTimeProvider.Object,
            createValidator.Object,
            _eventProducer.Object);
    }

    private void SetCurrentUser(UserRole role)
    {
        var identity = Identity.Success(
            new AuthenticatedUser { UserId = _moderatorUserId, Role = role, Username = "Moderator" },
            new Session { Id = Guid.NewGuid() },
            new UserSettings(),
            "token");
        _identityProvider.Setup(p => p.Current).Returns(identity);
    }

    [Fact]
    public async Task ReturnEmptyListWhenGettingBansForNonexistentUser()
    {
        _userLookupService.Setup(s => s.GetAsync("Unknown"))
            .ThrowsAsync(new HttpException(HttpStatusCode.Gone, "Пользователь не найден"));

        var result = await _service.GetUserBans("Unknown");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task LetAStorageFailureThroughInsteadOfReportingACleanRecord()
    {
        // The catch in GetUserBans answers one question - does this user exist
        // - and an empty list is the answer to it. A dropped connection is not
        // that answer: it used to reach the moderator's page as "no bans",
        // which is exactly what an unblemished profile looks like.
        _userLookupService.Setup(s => s.GetAsync("Target"))
            .ReturnsAsync(new GeneralUser { UserId = _targetUserId, Username = "Target" });
        _banRepository.Setup(r => r.GetUserBans(_targetUserId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("connection reset"));

        var act = () => _service.GetUserBans("Target");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task LetAStorageFailureThroughWhenReadingTheActiveBan()
    {
        // Same rule on the public route: "we could not check" must not leave
        // the service as "not banned".
        _userLookupService.Setup(s => s.GetAsync("Target"))
            .ReturnsAsync(new GeneralUser { UserId = _targetUserId, Username = "Target" });
        _banRepository.Setup(r => r.GetActiveBan(_targetUserId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("connection reset"));

        var act = () => _service.GetActiveBan("Target");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ThrowWhenNonModeratorTriesToViewAllBans()
    {
        var userIdentity = Identity.Success(
            new AuthenticatedUser { UserId = Guid.NewGuid(), Role = UserRole.RegularUser, Username = "User" },
            new Session { Id = Guid.NewGuid() },
            new UserSettings(),
            "token");
        _identityProvider.Setup(p => p.Current).Returns(userIdentity);

        var act = () => _service.GetAllActiveBans();

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden)
            .WithMessage("Список банов доступен модераторам");
    }

    [Fact]
    public async Task AllowModeratorToViewAllActiveBans()
    {
        SetCurrentUser(UserRole.Moderator);
        _banRepository.Setup(r => r.GetAllActiveBans(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _service.GetAllActiveBans();

        result.Should().NotBeNull();
        _banRepository.Verify(r => r.GetAllActiveBans(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ThrowWhenNonModeratorTriesToViewBanHistory()
    {
        SetCurrentUser(UserRole.RegularUser);

        var act = () => _service.GetBanHistory(0, 20);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden)
            .WithMessage("История банов доступна модераторам");
    }

    [Fact]
    public async Task AllowModeratorToViewBanHistory()
    {
        SetCurrentUser(UserRole.Moderator);
        _banRepository.Setup(r => r.GetBanHistory(0, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(([], 0));

        var (bans, totalCount) = await _service.GetBanHistory(0, 20);

        bans.Should().BeEmpty();
        totalCount.Should().Be(0);
        _banRepository.Verify(r => r.GetBanHistory(0, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ThrowWhenModeratorTriesToCreateNonVoluntaryBan()
    {
        SetCurrentUser(UserRole.Moderator);

        var createBan = new CreateBan { Username = "Target", IsVoluntary = false, DurationHours = 24 };
        var act = () => _service.CreateBan(createBan);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden)
            .WithMessage("Банить может только старший модератор");
    }

    /// <summary>
    /// The voluntary branch bans its author and nobody else.
    /// </summary>
    /// <remarks>
    /// It is the one branch that skips the senior-moderator gate, the
    /// administrator exemption and the strictly-lower-role rule at once, so a
    /// caller who could set the flag could ban anyone - and the ban would be filed
    /// under the victim's own name.
    /// </remarks>
    [Fact]
    public async Task RefuseAVoluntaryBanAimedAtSomebodyElse()
    {
        SetCurrentUser(UserRole.RegularUser);
        _userLookupService.Setup(s => s.GetAsync("Target"))
            .ReturnsAsync(new GeneralUser { UserId = _targetUserId, Username = "Target" });

        var act = () => _service.CreateBan(new CreateBan { Username = "Target", IsVoluntary = true });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden)
            .WithMessage("Добровольный бан можно наложить только на себя");
        _banRepository.Verify(
            r => r.Create(It.IsAny<CreateBanEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LetAUserBanThemselvesVoluntarily()
    {
        SetCurrentUser(UserRole.RegularUser);
        _userLookupService.Setup(s => s.GetAsync("Self"))
            .ReturnsAsync(new GeneralUser { UserId = _moderatorUserId, Username = "Self" });
        _banRepository.Setup(r => r.GetActiveBan(_moderatorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ban?)null);

        CreateBanEntity? capturedEntity = null;
        _banRepository.Setup(r => r.Create(It.IsAny<CreateBanEntity>(), It.IsAny<CancellationToken>()))
            .Callback<CreateBanEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync(new Ban());

        await _service.CreateBan(new CreateBan { Username = "Self", IsVoluntary = true, Comment = "Отдых" });

        capturedEntity.Should().NotBeNull();
        capturedEntity!.TargetUserId.Should().Be(_moderatorUserId);
        capturedEntity.AuthorId.Should().Be(_moderatorUserId);
        capturedEntity.IsVoluntary.Should().BeTrue();
    }

    [Fact]
    public async Task ThrowWhenCreatingBanForAlreadyBannedUser()
    {
        var targetUser = new GeneralUser { UserId = _targetUserId, Username = "Target" };
        var existingBan = new Ban { BanId = Guid.NewGuid(), EndedUtc = _now.AddDays(1) };

        _userLookupService.Setup(s => s.GetAsync("Target")).ReturnsAsync(targetUser);
        _banRepository.Setup(r => r.GetActiveBan(_targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingBan);

        var createBan = new CreateBan { Username = "Target", DurationHours = 24 };
        var act = () => _service.CreateBan(createBan);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict)
            .Where(e => e.Message.Contains("уже забанен"));
    }

    [Fact]
    public async Task CreateBanWithDurationInHours()
    {
        var targetUser = new GeneralUser { UserId = _targetUserId, Username = "Target" };
        _userLookupService.Setup(s => s.GetAsync("Target")).ReturnsAsync(targetUser);
        _banRepository.Setup(r => r.GetActiveBan(_targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ban?)null);

        CreateBanEntity? capturedEntity = null;
        _banRepository.Setup(r => r.Create(It.IsAny<CreateBanEntity>(), It.IsAny<CancellationToken>()))
            .Callback<CreateBanEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync(new Ban());

        var createBan = new CreateBan
        {
            Username = "Target",
            DurationHours = 24,
            Comment = "Spam",
            IsVoluntary = false
        };

        await _service.CreateBan(createBan);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.BanId.Should().Be(_banId);
        capturedEntity.TargetUserId.Should().Be(_targetUserId);
        capturedEntity.AuthorId.Should().Be(_moderatorUserId);
        capturedEntity.StartedUtc.Should().Be(_now);
        capturedEntity.EndedUtc.Should().Be(_now.AddHours(24));
        capturedEntity.IsVoluntary.Should().BeFalse();
    }

    [Fact]
    public async Task AnnounceAnIssuedBan()
    {
        var targetUser = new GeneralUser { UserId = _targetUserId, Username = "Target" };
        _userLookupService.Setup(s => s.GetAsync("Target")).ReturnsAsync(targetUser);
        _banRepository.Setup(r => r.GetActiveBan(_targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ban?)null);
        _banRepository.Setup(r => r.Create(It.IsAny<CreateBanEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Ban { BanId = _banId });

        await _service.CreateBan(new CreateBan
        {
            Username = "Target",
            DurationHours = 24,
            Comment = "Spam",
            IsVoluntary = false
        });

        _eventProducer.Verify(p => p.SendAsync(EventType.BanIssued, _banId), Times.Once);
    }

    [Fact]
    public async Task NotAnnounceAVoluntarySelfBan()
    {
        // The ban notification exists to tell a person about something he did not
        // do. Its only recipient is the target, who here is the author as well.
        var targetUser = new GeneralUser { UserId = _moderatorUserId, Username = "Moderator" };
        _userLookupService.Setup(s => s.GetAsync("Moderator")).ReturnsAsync(targetUser);
        _banRepository.Setup(r => r.GetActiveBan(_moderatorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ban?)null);
        _banRepository.Setup(r => r.Create(It.IsAny<CreateBanEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Ban { BanId = _banId });

        await _service.CreateBan(new CreateBan
        {
            Username = "Moderator",
            DurationHours = 48,
            Comment = "Перерыв",
            IsVoluntary = true
        });

        _eventProducer.Verify(p => p.SendAsync(It.IsAny<EventType>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task PersistDemocraticBanScopeWhenRequested()
    {
        var targetUser = new GeneralUser { UserId = _targetUserId, Username = "Target" };
        _userLookupService.Setup(s => s.GetAsync("Target")).ReturnsAsync(targetUser);
        _banRepository.Setup(r => r.GetActiveBan(_targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ban?)null);

        CreateBanEntity? capturedEntity = null;
        _banRepository.Setup(r => r.Create(It.IsAny<CreateBanEntity>(), It.IsAny<CancellationToken>()))
            .Callback<CreateBanEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync(new Ban());

        var createBan = new CreateBan
        {
            Username = "Target",
            DurationHours = 24,
            Comment = "Democratic ban",
            AccessRestrictionPolicy = AccessPolicy.DemocraticBan
        };

        await _service.CreateBan(createBan);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.AccessRestrictionPolicy.Should().Be(AccessPolicy.DemocraticBan);
    }

    [Fact]
    public async Task DefaultBanScopeToFullBan()
    {
        var targetUser = new GeneralUser { UserId = _targetUserId, Username = "Target" };
        _userLookupService.Setup(s => s.GetAsync("Target")).ReturnsAsync(targetUser);
        _banRepository.Setup(r => r.GetActiveBan(_targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ban?)null);

        CreateBanEntity? capturedEntity = null;
        _banRepository.Setup(r => r.Create(It.IsAny<CreateBanEntity>(), It.IsAny<CancellationToken>()))
            .Callback<CreateBanEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync(new Ban());

        // No AccessRestrictionPolicy set → the DTO default (FullBan) applies.
        var createBan = new CreateBan { Username = "Target", DurationHours = 24, Comment = "Spam" };

        await _service.CreateBan(createBan);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.AccessRestrictionPolicy.Should().Be(AccessPolicy.FullBan);
    }

    [Fact]
    public async Task CoerceUnsupportedBanScopeToFullBan()
    {
        var targetUser = new GeneralUser { UserId = _targetUserId, Username = "Target" };
        _userLookupService.Setup(s => s.GetAsync("Target")).ReturnsAsync(targetUser);
        _banRepository.Setup(r => r.GetActiveBan(_targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ban?)null);

        CreateBanEntity? capturedEntity = null;
        _banRepository.Setup(r => r.Create(It.IsAny<CreateBanEntity>(), It.IsAny<CancellationToken>()))
            .Callback<CreateBanEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync(new Ban());

        // A scope that is neither Democratic nor Full falls back to FullBan.
        var createBan = new CreateBan
        {
            Username = "Target",
            DurationHours = 24,
            Comment = "Spam",
            AccessRestrictionPolicy = AccessPolicy.NotSpecified
        };

        await _service.CreateBan(createBan);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.AccessRestrictionPolicy.Should().Be(AccessPolicy.FullBan);
    }

    [Fact]
    public async Task CreatePermanentBanWhenNoDurationProvided()
    {
        var targetUser = new GeneralUser { UserId = _targetUserId, Username = "Target" };
        _userLookupService.Setup(s => s.GetAsync("Target")).ReturnsAsync(targetUser);
        _banRepository.Setup(r => r.GetActiveBan(_targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ban?)null);

        CreateBanEntity? capturedEntity = null;
        _banRepository.Setup(r => r.Create(It.IsAny<CreateBanEntity>(), It.IsAny<CancellationToken>()))
            .Callback<CreateBanEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync(new Ban());

        var createBan = new CreateBan { Username = "Target", Comment = "Permanent ban" };
        await _service.CreateBan(createBan);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.EndedUtc.Should().Be(_now.AddYears(100));
    }

    [Fact]
    public async Task AllowVoluntaryBanCreatedByUser()
    {
        var targetUser = new GeneralUser { UserId = _moderatorUserId, Username = "Moderator" };
        _userLookupService.Setup(s => s.GetAsync("Moderator")).ReturnsAsync(targetUser);
        _banRepository.Setup(r => r.GetActiveBan(_moderatorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ban?)null);

        CreateBanEntity? capturedEntity = null;
        _banRepository.Setup(r => r.Create(It.IsAny<CreateBanEntity>(), It.IsAny<CancellationToken>()))
            .Callback<CreateBanEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync(new Ban());

        var createBan = new CreateBan
        {
            Username = "Moderator",
            IsVoluntary = true,
            DurationHours = 48
        };

        await _service.CreateBan(createBan);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.IsVoluntary.Should().BeTrue();
        capturedEntity.AuthorId.Should().Be(_moderatorUserId);
    }

    [Fact]
    public async Task ThrowWhenModeratorTriesToLiftBan()
    {
        SetCurrentUser(UserRole.Moderator);

        var act = () => _service.LiftBan(_banId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden)
            .WithMessage("Снимать бан может только старший модератор");
    }

    [Fact]
    public async Task ThrowNotFoundWhenLiftingMissingBan()
    {
        _banRepository.Setup(r => r.Get(_banId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ban?)null);

        var act = () => _service.LiftBan(_banId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LiftTemporaryBanAsSeniorModerator()
    {
        _banRepository.Setup(r => r.Get(_banId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Ban { BanId = _banId, EndedUtc = _now.AddDays(7) });

        await _service.LiftBan(_banId);

        _banRepository.Verify(r => r.Lift(_banId, It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ForbidSeniorModeratorFromLiftingPermanentBan()
    {
        // Permanent bans are stored with a far-future end date
        _banRepository.Setup(r => r.Get(_banId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Ban { BanId = _banId, EndedUtc = _now.AddYears(100) });

        var act = () => _service.LiftBan(_banId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
        _banRepository.Verify(r => r.Lift(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AllowAdminToLiftPermanentBan()
    {
        SetCurrentUser(UserRole.Admin);
        _banRepository.Setup(r => r.Get(_banId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Ban { BanId = _banId, EndedUtc = _now.AddYears(100) });

        await _service.LiftBan(_banId);

        _banRepository.Verify(r => r.Lift(_banId, It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AllowSeniorModeratorToLiftPermanentVoluntaryBan()
    {
        _banRepository.Setup(r => r.Get(_banId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Ban { BanId = _banId, EndedUtc = _now.AddYears(100), IsVoluntary = true });

        await _service.LiftBan(_banId);

        _banRepository.Verify(r => r.Lift(_banId, It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }
    // --- Кто кого может банить (BE-18, BE-19) ---

    private void ArrangeTarget(UserRole targetRole)
    {
        _userLookupService.Setup(s => s.GetAsync("Target"))
            .ReturnsAsync(new GeneralUser { UserId = _targetUserId, Username = "Target", Role = targetRole });
        _banRepository.Setup(r => r.GetActiveBan(_targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ban?)null);
        _banRepository.Setup(r => r.Create(It.IsAny<CreateBanEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Ban());
    }

    /// <summary>
    /// The same arrangement, with the caller as the target: a genuine self-ban.
    /// </summary>
    private void ArrangeSelf(UserRole role)
    {
        SetCurrentUser(role);
        _userLookupService.Setup(s => s.GetAsync("Target"))
            .ReturnsAsync(new GeneralUser { UserId = _moderatorUserId, Username = "Target", Role = role });
        _banRepository.Setup(r => r.GetActiveBan(_moderatorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ban?)null);
        _banRepository.Setup(r => r.Create(It.IsAny<CreateBanEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Ban());
    }

    private static CreateBan BanRequest() => new()
    {
        Username = "Target",
        DurationHours = 24,
        Comment = "Spam",
        IsVoluntary = false
    };

    [Theory]
    [InlineData(UserRole.RegularUser)]
    [InlineData(UserRole.Mentor)]
    [InlineData(UserRole.Moderator)]
    public async Task BanAUserWhoseRoleIsBelowYours(UserRole targetRole)
    {
        ArrangeTarget(targetRole);

        var act = () => _service.CreateBan(BanRequest());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RefuseToBanAnEqualRole()
    {
        // Otherwise two senior moderators can ban each other in turn.
        ArrangeTarget(UserRole.SeniorModerator);

        var act = () => _service.CreateBan(BanRequest());

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RefuseToBanAnAdministrator()
    {
        // An administrator is a site owner: no in-app authority over one, and a
        // senior moderator locking the only admin out would be unrecoverable.
        SetCurrentUser(UserRole.Admin);
        ArrangeTarget(UserRole.Admin);

        var act = () => _service.CreateBan(BanRequest());

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task StillAllowAVoluntarySelfBan()
    {
        // The role comparison must not catch the one legitimate self-ban. Named
        // a self-ban and arranged as one: the target resolves to the caller.
        // Arranged against somebody else it passed for as long as the voluntary
        // flag was the whole of the check, which is the defect below.
        ArrangeSelf(UserRole.SeniorModerator);
        var request = BanRequest();
        request.IsVoluntary = true;

        var act = () => _service.CreateBan(request);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RefuseAVoluntaryBanOnSomebodyElse()
    {
        // The flag skips the senior-moderator gate and both role comparisons, so
        // without a target check it is an unauthenticated ban of anybody at all,
        // an administrator included, filed under the victim's own name.
        SetCurrentUser(UserRole.RegularUser);
        ArrangeTarget(UserRole.Admin);
        var request = BanRequest();
        request.IsVoluntary = true;

        var act = () => _service.CreateBan(request);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RefuseToLiftYourOwnBan()
    {
        // A democratic ban leaves the role and authentication intact, so without
        // this a banned senior moderator simply lifts it from himself.
        _banRepository.Setup(r => r.Get(_banId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Ban { BanId = _banId, TargetUserId = _moderatorUserId, EndedUtc = _now.AddDays(7) });

        var act = () => _service.LiftBan(_banId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RecordWhoLiftedTheBanAndWhy()
    {
        _banRepository.Setup(r => r.Get(_banId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Ban { BanId = _banId, TargetUserId = _targetUserId, EndedUtc = _now.AddDays(7) });

        await _service.LiftBan(_banId, "Разобрались");

        _banRepository.Verify(r => r.Lift(_banId, _moderatorUserId, _now, "Разобрались",
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
