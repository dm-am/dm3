using System;
using System.Collections.Generic;
using System.Linq;
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
using AwesomeAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Warnings;

public class WarningServiceShould : UnitTestBase
{
    private readonly IWarningRepository _warningRepository;
    private readonly IBanRepository _banRepository;
    private readonly IWarningEntityResolver _entityResolver;
    private readonly IUserLookupService _userLookupService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IEventProducer _eventProducer;
    private readonly WarningService _service;
    private readonly Guid _moderatorUserId = Guid.NewGuid();
    private readonly Guid _targetUserId = Guid.NewGuid();
    private readonly Guid _warningId = Guid.NewGuid();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    public WarningServiceShould()
    {
        _warningRepository = Mock<IWarningRepository>();
        _banRepository = Mock<IBanRepository>();
        _entityResolver = Mock<IWarningEntityResolver>();
        _userLookupService = Mock<IUserLookupService>();
        _identityProvider = Mock<IIdentityProvider>();
        _guidFactory = Mock<IGuidFactory>();
        _dateTimeProvider = Mock<IDateTimeProvider>();
        _eventProducer = Mock<IEventProducer>();

        var moderatorIdentity = Identity.Success(
            new AuthenticatedUser { UserId = _moderatorUserId, Role = UserRole.Moderator, Username = "Moderator" },
            new Session { Id = Guid.NewGuid() },
            new UserSettings(),
            "token");
        _identityProvider.Current.Returns(moderatorIdentity);
        _dateTimeProvider.Now.Returns(_now);
        _guidFactory.Create().Returns(_warningId);
        // Default: nothing is known about any offending object. Tests that care
        // about the evidence override this.
        _entityResolver
            .ResolveStates(
                Arg.Any<IReadOnlyCollection<WarningEntityRequest>>(), Arg.Any<CancellationToken>()).Returns(new Dictionary<Guid, WarningEntityState>());

        _service = new WarningService(
            new CreateWarningValidator(),
            _warningRepository,
            _banRepository,
            _entityResolver,
            _userLookupService,
            _identityProvider,
            _guidFactory,
            _dateTimeProvider,
            _eventProducer);
    }

    [Fact]
    public async Task ReturnEmptyListWhenGettingWarningsForNonexistentUser()
    {
        _userLookupService.GetAsync("Unknown")
            .ThrowsAsync(new HttpException(HttpStatusCode.NotFound, "Пользователь не найден"));

        var result = await _service.GetUserWarnings("Unknown");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task LetAStorageFailureThroughInsteadOfReportingNoViolations()
    {
        // "No warnings" and "we could not read them" look the same to the
        // moderator and the same in the log, because there is no log.
        _userLookupService.GetAsync("Target").Returns(new GeneralUser { UserId = _targetUserId, Username = "Target" });
        _warningRepository.GetUserWarnings(_targetUserId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("connection reset"));

        var act = () => _service.GetUserWarnings("Target");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task LetAStorageFailureThroughInsteadOfReportingZeroPoints()
    {
        _userLookupService.GetAsync("Target").Returns(new GeneralUser { UserId = _targetUserId, Username = "Target" });
        _warningRepository
            .GetUserWarningPoints(_targetUserId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("connection reset"));

        var act = () => _service.GetUserWarningPoints("Target");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ThrowWhenNonModeratorTriesToViewAllWarnings()
    {
        var userIdentity = Identity.Success(
            new AuthenticatedUser { UserId = Guid.NewGuid(), Role = UserRole.RegularUser, Username = "User" },
            new Session { Id = Guid.NewGuid() },
            new UserSettings(),
            "token");
        _identityProvider.Current.Returns(userIdentity);

        var act = () => _service.GetAllWarnings();

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden)
            .WithMessage("Список предупреждений доступен модераторам");
    }

    [Fact]
    public async Task AllowModeratorToViewAllWarnings()
    {
        var result = await _service.GetAllWarnings();

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ThrowWhenNonModeratorTriesToCreateWarning()
    {
        var userIdentity = Identity.Success(
            new AuthenticatedUser { UserId = Guid.NewGuid(), Role = UserRole.RegularUser, Username = "User" },
            new Session { Id = Guid.NewGuid() },
            new UserSettings(),
            "token");
        _identityProvider.Current.Returns(userIdentity);

        var createWarning = new CreateWarning { Username = "Target", Reason = "Bad behavior", Points = 2 };
        var act = () => _service.CreateWarning(createWarning);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden)
            .WithMessage("Выносить предупреждения может только модератор");
    }

    [Fact]
    public async Task CreateWarningWithCorrectData()
    {
        var targetUser = new GeneralUser { UserId = _targetUserId, Username = "Target" };
        _userLookupService.GetAsync("Target").Returns(targetUser);

        CreateWarningEntity? capturedEntity = null;
        _warningRepository.Create(Arg.Any<CreateWarningEntity>(), Arg.Any<CancellationToken>())
            .Returns(new Warning())
            .AndDoes(ci =>
            {
                var e = ci.ArgAt<CreateWarningEntity>(0);
                capturedEntity = e;
            });

        var entityId = Guid.NewGuid();
        var createWarning = new CreateWarning
        {
            Username = "Target",
            EntityId = entityId,
            EntityType = "Topic",
            Reason = "Spam",
            Points = 2
        };

        await _service.CreateWarning(createWarning);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.WarningId.Should().Be(_warningId);
        capturedEntity.TargetUserId.Should().Be(_targetUserId);
        capturedEntity.AuthorId.Should().Be(_moderatorUserId);
        capturedEntity.EntityId.Should().Be(entityId);
        capturedEntity.EntityType.Should().Be(WarningEntityType.Topic);
        capturedEntity.Text.Should().Be("Spam");
        capturedEntity.Points.Should().Be(2);
        capturedEntity.CreatedUtc.Should().Be(_now);
    }

    /// <summary>
    /// Game content is outside moderation, so a warning cannot name a game post.
    /// </summary>
    /// <remarks>
    /// The type used to be accepted and stored. The resolver has never been able
    /// to snapshot a post or address one, so what got written was a warning
    /// pointing at game content with no evidence behind it and no link — a
    /// verdict on something moderation does not judge, which the moderator only
    /// found out about by looking at the empty row afterwards.
    /// </remarks>
    [Fact]
    public async Task RefuseAWarningOnGameContent()
    {
        _userLookupService.GetAsync("Target").Returns(new GeneralUser { UserId = _targetUserId, Username = "Target" });

        var act = () => _service.CreateWarning(new CreateWarning
        {
            Username = "Target",
            EntityId = Guid.NewGuid(),
            EntityType = "Post",
            Reason = "Оскорбление в игровом посте",
            Points = 2
        });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest);

        await _warningRepository.DidNotReceive().Create(Arg.Any<CreateWarningEntity>(), Arg.Any<CancellationToken>());
        await _entityResolver.DidNotReceive().CaptureSnapshot(
                Arg.Any<WarningEntityType>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnnounceAnIssuedWarning()
    {
        var targetUser = new GeneralUser { UserId = _targetUserId, Username = "Target" };
        _userLookupService.GetAsync("Target").Returns(targetUser);
        _warningRepository.Create(Arg.Any<CreateWarningEntity>(), Arg.Any<CancellationToken>())
            .Returns(new Warning { WarningId = _warningId });

        await _service.CreateWarning(new CreateWarning
        {
            Username = "Target",
            Reason = "Spam",
            Points = 2
        });

        await _eventProducer.Received(1).SendAsync(EventType.WarningIssued, _warningId);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    [InlineData(10)]
    public async Task RejectWarningPointsOutsideZeroToSixRange(int points)
    {
        var createWarning = new CreateWarning
        {
            Username = "Target",
            Reason = "Very bad behavior",
            Points = points
        };

        var act = () => _service.CreateWarning(createWarning);

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    [Fact]
    public async Task CreateVerbalWarningWithZeroPoints()
    {
        var targetUser = new GeneralUser { UserId = _targetUserId, Username = "Target" };
        _userLookupService.GetAsync("Target").Returns(targetUser);

        CreateWarningEntity? capturedEntity = null;
        _warningRepository.Create(Arg.Any<CreateWarningEntity>(), Arg.Any<CancellationToken>())
            .Returns(new Warning())
            .AndDoes(ci =>
            {
                var e = ci.ArgAt<CreateWarningEntity>(0);
                capturedEntity = e;
            });

        var createWarning = new CreateWarning
        {
            Username = "Target",
            Reason = "Verbal warning",
            Points = 0
        };

        await _service.CreateWarning(createWarning);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.Points.Should().Be(0);
    }

    [Fact]
    public async Task ParseEntityTypeCorrectly()
    {
        var targetUser = new GeneralUser { UserId = _targetUserId, Username = "Target" };
        _userLookupService.GetAsync("Target").Returns(targetUser);

        CreateWarningEntity? capturedEntity = null;
        _warningRepository.Create(Arg.Any<CreateWarningEntity>(), Arg.Any<CancellationToken>())
            .Returns(new Warning())
            .AndDoes(ci =>
            {
                var e = ci.ArgAt<CreateWarningEntity>(0);
                capturedEntity = e;
            });

        var createWarning = new CreateWarning
        {
            Username = "Target",
            EntityType = "comment",
            Reason = "Inappropriate comment",
            Points = 1
        };

        await _service.CreateWarning(createWarning);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.EntityType.Should().Be(WarningEntityType.Comment);
    }

    [Fact]
    public async Task ThrowWhenNonModeratorTriesToRemoveWarning()
    {
        var userIdentity = Identity.Success(
            new AuthenticatedUser { UserId = Guid.NewGuid(), Role = UserRole.RegularUser, Username = "User" },
            new Session { Id = Guid.NewGuid() },
            new UserSettings(),
            "token");
        _identityProvider.Current.Returns(userIdentity);

        var act = () => _service.RemoveWarning(_warningId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden)
            .WithMessage("Снимать предупреждения может только модератор");
    }

    [Fact]
    public async Task RemoveWarningSuccessfully()
    {
        await _service.RemoveWarning(_warningId);

        await _warningRepository.Received(1).Remove(_warningId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetUserWarningPointsSuccessfully()
    {
        var targetUser = new GeneralUser { UserId = _targetUserId, Username = "Target" };
        _userLookupService.GetAsync("Target").Returns(targetUser);
        _warningRepository.GetUserWarningPoints(_targetUserId, Arg.Any<CancellationToken>()).Returns(5);

        var result = await _service.GetUserWarningPoints("Target");

        result.Should().Be(5);
    }

    [Fact]
    public async Task ThrowWhenNonModeratorTriesToViewViolators()
    {
        var userIdentity = Identity.Success(
            new AuthenticatedUser { UserId = Guid.NewGuid(), Role = UserRole.RegularUser, Username = "User" },
            new Session { Id = Guid.NewGuid() },
            new UserSettings(),
            "token");
        _identityProvider.Current.Returns(userIdentity);

        var act = () => _service.GetViolators();

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden)
            .WithMessage("Список нарушителей доступен модераторам");
    }

    [Fact]
    public async Task MergeWarningPointsAndActiveBansIntoViolators()
    {
        var pointsUser = new GeneralUser { UserId = Guid.NewGuid(), Username = "PointsOnly" };
        var bannedUser = new GeneralUser { UserId = Guid.NewGuid(), Username = "BannedOnly" };
        var bothUser = new GeneralUser { UserId = Guid.NewGuid(), Username = "Both" };

        _warningRepository.GetActiveWarningSummaries(Arg.Any<CancellationToken>()).Returns(
            [
                new UserWarningSummary { User = pointsUser, Points = 2, LastWarningUtc = _now.AddDays(-1) },
                new UserWarningSummary { User = bothUser, Points = 5, LastWarningUtc = _now.AddDays(-2) }
            ]);
        _banRepository.GetAllActiveBans(Arg.Any<CancellationToken>()).Returns(
            [
                new Ban { BanId = Guid.NewGuid(), TargetUser = bannedUser, EndedUtc = _now.AddDays(3) },
                new Ban { BanId = Guid.NewGuid(), TargetUser = bothUser, EndedUtc = _now.AddDays(7) }
            ]);

        var result = (await _service.GetViolators()).ToList();

        result.Should().HaveCount(3);
        // Default sort: points descending, banned user without points goes last
        result[0].User.Username.Should().Be("Both");
        result[0].Points.Should().Be(5);
        result[0].ActiveBan.Should().NotBeNull();
        result[1].User.Username.Should().Be("PointsOnly");
        result[1].Points.Should().Be(2);
        result[1].ActiveBan.Should().BeNull();
        result[2].User.Username.Should().Be("BannedOnly");
        result[2].Points.Should().Be(0);
        result[2].ActiveBan.Should().NotBeNull();
    }

    [Fact]
    public async Task FilterViolatorsByBannedAndPointsOnly()
    {
        var pointsUser = new GeneralUser { UserId = Guid.NewGuid(), Username = "PointsOnly" };
        var bannedUser = new GeneralUser { UserId = Guid.NewGuid(), Username = "BannedOnly" };

        _warningRepository.GetActiveWarningSummaries(Arg.Any<CancellationToken>())
            .Returns([new UserWarningSummary { User = pointsUser, Points = 3, LastWarningUtc = _now }]);
        _banRepository.GetAllActiveBans(Arg.Any<CancellationToken>())
            .Returns([new Ban { BanId = Guid.NewGuid(), TargetUser = bannedUser, EndedUtc = _now.AddDays(1) }]);

        var banned = (await _service.GetViolators(ViolatorsFilter.Banned)).ToList();
        var pointsOnly = (await _service.GetViolators(ViolatorsFilter.PointsOnly)).ToList();

        banned.Should().ContainSingle(v => v.User.Username == "BannedOnly");
        pointsOnly.Should().ContainSingle(v => v.User.Username == "PointsOnly");
    }

    /// <summary>
    /// The unfiltered list is the repository's, not an empty one. It used to return
    /// [] with a note that the repository method was missing, behind a Moderator+
    /// gate and a 200, so a moderator concluded there were no violations.
    /// </summary>
    [Fact]
    public async Task ReturnEveryWarningWhenNoUserIsNamed()
    {
        _warningRepository.GetAllWarnings(Arg.Any<CancellationToken>())
            .Returns([new Warning { WarningId = _warningId, Points = 3 }]);

        var result = (await _service.GetAllWarnings()).ToList();

        result.Should().ContainSingle(w => w.WarningId == _warningId);
    }

    /// <summary>
    /// The offending text is copied onto the warning as it is being issued.
    /// </summary>
    /// <remarks>
    /// The warning used to keep a reference and nothing else, and the author may
    /// edit the referenced content afterwards — no edit history on the site keeps
    /// the previous text, so the evidence for a warning could be erased by the
    /// person it was issued to. Taken here, before the write, from the same
    /// source the moderator was reading.
    /// </remarks>
    [Fact]
    public async Task TakeASnapshotOfTheOffendingTextWhenIssuingTheWarning()
    {
        var entityId = Guid.NewGuid();
        _userLookupService.GetAsync("Target").Returns(new GeneralUser { UserId = _targetUserId, Username = "Target" });
        _entityResolver
            .CaptureSnapshot(WarningEntityType.Comment, entityId, Arg.Any<CancellationToken>())
            .Returns("Оскорбление, за которое выносится предупреждение");

        CreateWarningEntity? captured = null;
        _warningRepository.Create(Arg.Any<CreateWarningEntity>(), Arg.Any<CancellationToken>())
            .Returns(new Warning())
            .AndDoes(ci =>
            {
                var e = ci.ArgAt<CreateWarningEntity>(0);
                captured = e;
            });

        await _service.CreateWarning(new CreateWarning
        {
            Username = "Target",
            EntityId = entityId,
            EntityType = "Comment",
            Reason = "Оскорбление",
            Points = 2
        });

        captured.Should().NotBeNull();
        captured!.EntitySnapshot.Should().Be("Оскорбление, за которое выносится предупреждение");
    }

    /// <summary>
    /// A warning issued from the profile block names no content, so there is
    /// nothing to snapshot and nothing to ask the resolver about.
    /// </summary>
    [Fact]
    public async Task TakeNoSnapshotWhenTheWarningNamesNoContent()
    {
        _userLookupService.GetAsync("Target").Returns(new GeneralUser { UserId = _targetUserId, Username = "Target" });

        CreateWarningEntity? captured = null;
        _warningRepository.Create(Arg.Any<CreateWarningEntity>(), Arg.Any<CancellationToken>())
            .Returns(new Warning())
            .AndDoes(ci =>
            {
                var e = ci.ArgAt<CreateWarningEntity>(0);
                captured = e;
            });

        await _service.CreateWarning(new CreateWarning
        {
            Username = "Target",
            Reason = "Общее замечание",
            Points = 1
        });

        captured.Should().NotBeNull();
        captured!.EntitySnapshot.Should().BeNull();
        await _entityResolver.DidNotReceive().CaptureSnapshot(
                Arg.Any<WarningEntityType>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The moderation list carries the address of the offending object and the
    /// mark that it was edited after the warning — both read per request, neither
    /// stored on the warning.
    /// </summary>
    [Fact]
    public async Task DescribeTheOffendingObjectOnTheModerationList()
    {
        var entityId = Guid.NewGuid();
        _warningRepository.GetAllWarnings(Arg.Any<CancellationToken>()).Returns([
                new Warning
                {
                    WarningId = _warningId,
                    EntityId = entityId,
                    EntityType = WarningEntityType.Topic,
                    CreatedUtc = _now,
                    EntitySnapshot = "Исходный текст"
                }
            ]);
        _entityResolver
            .ResolveStates(
                Arg.Any<IReadOnlyCollection<WarningEntityRequest>>(), Arg.Any<CancellationToken>()).Returns(new Dictionary<Guid, WarningEntityState>
                {
                    [_warningId] = new() { Url = "/forum/flood/12", EditedAfterWarning = true }
                });

        var warning = (await _service.GetAllWarnings()).Single();

        warning.EntitySnapshot.Should().Be("Исходный текст");
        warning.EntityUrl.Should().Be("/forum/flood/12");
        warning.EntityEditedAfterWarning.Should().BeTrue();
    }

    /// <summary>
    /// The public profile view is trimmed to points and dates, so the evidence
    /// queries are not run for it — neither their cost nor their answers belong
    /// on a page a stranger can open.
    /// </summary>
    [Fact]
    public async Task NotDescribeTheOffendingObjectForThePublicProfileView()
    {
        _userLookupService.GetAsync("Target").Returns(new GeneralUser { UserId = _targetUserId, Username = "Target" });
        _warningRepository.GetUserWarnings(_targetUserId, Arg.Any<CancellationToken>()).Returns([
                new Warning
                {
                    WarningId = _warningId,
                    EntityId = Guid.NewGuid(),
                    EntityType = WarningEntityType.Comment,
                    CreatedUtc = _now
                }
            ]);

        await _service.GetUserWarnings("Target");

        await _entityResolver.DidNotReceive().ResolveStates(
                Arg.Any<IReadOnlyCollection<WarningEntityRequest>>(), Arg.Any<CancellationToken>());
    }
}
