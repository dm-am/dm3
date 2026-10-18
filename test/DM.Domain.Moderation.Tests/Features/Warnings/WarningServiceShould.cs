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
    private readonly IBanService _banService;
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
        _banService = Mock<IBanService>();
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
            new ChangeWarningPointsValidator(),
            _warningRepository,
            _banRepository,
            _banService,
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

        var act = () => _service.GetAllWarnings(null, new PagingQuery());

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden)
            .WithMessage("Список предупреждений доступен модераторам");
    }

    [Fact]
    public async Task AllowModeratorToViewAllWarnings()
    {
        _warningRepository
            .GetLog(null, Arg.Any<PagingQuery>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<Warning>)[], 0));

        var (warnings, _) = await _service.GetAllWarnings(null, new PagingQuery());

        warnings.Should().NotBeNull();
    }

    /// <summary>
    /// The open log asks nobody who they are.
    /// </summary>
    /// <remarks>
    /// The page the rules link to is read by visitors without an account, and the
    /// only other reading of warnings this service offers refuses everyone below a
    /// moderator. A role check copied onto this path would turn the page into a
    /// refusal for the readers it exists for.
    /// </remarks>
    [Fact]
    public async Task ReadTheOpenLogWithoutARole()
    {
        _identityProvider.Current.Returns(Identity.Guest());
        _warningRepository
            .GetLog(null, Arg.Any<PagingQuery>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<Warning>)[new Warning { WarningId = _warningId, Points = 3 }], 1));

        var (warnings, paging) = await _service.GetLog(null, new PagingQuery());

        warnings.Should().ContainSingle(w => w.WarningId == _warningId);
        paging.TotalEntitiesCount.Should().Be(1);
    }

    /// <summary>
    /// The open log shows the offending text where a guest could read it, so it
    /// asks where the text stood and whether it is open there, for the rows of
    /// the page and once.
    /// </summary>
    [Fact]
    public async Task DescribeTheOffendingObjectOnTheOpenLog()
    {
        _identityProvider.Current.Returns(Identity.Guest());
        _warningRepository
            .GetLog(null, Arg.Any<PagingQuery>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<Warning>)[
                new Warning
                {
                    WarningId = _warningId,
                    EntityId = Guid.NewGuid(),
                    EntityType = WarningEntityType.Comment,
                    CreatedUtc = _now,
                    EntitySnapshot = "Исходный текст"
                }
            ], 1));
        _entityResolver
            .ResolveStates(Arg.Any<IReadOnlyCollection<WarningEntityRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, WarningEntityState>
            {
                [_warningId] = new()
                {
                    PlaceKind = WarningPlaceKind.Game,
                    PlaceTitle = "Тайны Древних",
                    CreatedUtc = _now.AddHours(-1),
                    LogClosure = WarningLogClosure.PlaceNotPublic
                }
            });

        var warning = (await _service.GetLog(null, new PagingQuery())).Warnings.Single();

        await _entityResolver.Received(1).ResolveStates(
            Arg.Any<IReadOnlyCollection<WarningEntityRequest>>(), Arg.Any<CancellationToken>());
        warning.PlaceKind.Should().Be(WarningPlaceKind.Game);
        warning.PlaceTitle.Should().Be("Тайны Древних");
        warning.EntityCreatedUtc.Should().Be(_now.AddHours(-1));
        warning.LogClosure.Should().Be(WarningLogClosure.PlaceNotPublic);
    }

    /// <summary>
    /// A copy of a text that nothing can place is closed to the open log: the
    /// question whether a guest could read it there has no answer.
    /// </summary>
    [Fact]
    public async Task CloseTheOpenLogOverATextNothingPlaces()
    {
        _identityProvider.Current.Returns(Identity.Guest());
        _warningRepository
            .GetLog(null, Arg.Any<PagingQuery>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<Warning>)[
                new Warning
                {
                    WarningId = _warningId,
                    EntityId = Guid.Empty,
                    EntityType = WarningEntityType.Unknown,
                    CreatedUtc = _now,
                    EntitySnapshot = "Текст из переноса без места"
                },
                new Warning
                {
                    WarningId = Guid.NewGuid(),
                    EntityId = Guid.Empty,
                    EntityType = WarningEntityType.Unknown,
                    CreatedUtc = _now
                }
            ], 2));

        var warnings = (await _service.GetLog(null, new PagingQuery())).Warnings;

        warnings[0].LogClosure.Should().Be(WarningLogClosure.NotFound);
        warnings[1].LogClosure.Should().BeNull("a warning from a profile has no text to close");
    }

    /// <summary>
    /// A name nobody carries narrows the log to nothing, not to a refusal.
    /// </summary>
    /// <remarks>
    /// The same answer GetUserWarnings gives, and the page with a filter shows the
    /// name it was asked for, so a typo in the address is readable off the heading.
    /// </remarks>
    [Fact]
    public async Task AnswerAnUnknownNameWithAnEmptyPage()
    {
        _userLookupService.GetAsync("Unknown")
            .ThrowsAsync(new HttpException(HttpStatusCode.NotFound, "Пользователь не найден"));

        var (warnings, paging) = await _service.GetLog("Unknown", new PagingQuery());

        warnings.Should().BeEmpty();
        paging.TotalEntitiesCount.Should().Be(0);
        await _warningRepository.DidNotReceive().GetLog(
            Arg.Any<Guid?>(), Arg.Any<PagingQuery>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A named reader narrows the read itself, rather than the answer to it.
    /// </summary>
    [Fact]
    public async Task NarrowTheLogToTheNamedReader()
    {
        _userLookupService.GetAsync("Target")
            .Returns(new GeneralUser { UserId = _targetUserId, Username = "Target" });
        _warningRepository
            .GetLog(_targetUserId, Arg.Any<PagingQuery>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<Warning>)[new Warning { WarningId = _warningId }], 1));

        var (warnings, _) = await _service.GetLog("Target", new PagingQuery());

        warnings.Should().ContainSingle(w => w.WarningId == _warningId);
    }

    /// <summary>
    /// The moderation list reads the page the open log reads, through the same
    /// method of the repository.
    /// </summary>
    /// <remarks>
    /// Two reads of one list would be two orderings and two filters to keep in
    /// agreement, and the only difference the two screens have is who may call them
    /// and what a row carries.
    /// </remarks>
    [Fact]
    public async Task ReadTheModerationListThroughTheSamePage()
    {
        var paging = new PagingQuery { Skip = 40, Take = 20 };
        _warningRepository
            .GetLog(null, paging, Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<Warning>)[new Warning { WarningId = _warningId }], 61));

        var (warnings, page) = await _service.GetAllWarnings(null, paging);

        warnings.Should().ContainSingle(w => w.WarningId == _warningId);
        page.TotalEntitiesCount.Should().Be(61);
        page.CurrentPage.Should().Be(3);
        page.PageSize.Should().Be(20);
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

    /// <summary>
    /// A warning reaches only a rank below the moderator's own, and never an
    /// administrator (ModerationRank, the rule of every action against a
    /// person): a moderator warns neither a colleague nor a senior moderator nor
    /// an administrator, a senior moderator warns a moderator but not another
    /// senior moderator, and an administrator warns anybody but an administrator.
    /// </summary>
    /// <remarks>
    /// The owner's answer to whether a moderator may warn a senior moderator or
    /// an administrator (05.10): "нет". It used to be yes for anybody, and the
    /// senior moderator could then take the points off his own warning himself.
    /// </remarks>
    [Theory]
    [InlineData(UserRole.Moderator, UserRole.Moderator)]
    [InlineData(UserRole.Moderator, UserRole.SeniorModerator)]
    [InlineData(UserRole.Moderator, UserRole.Admin)]
    [InlineData(UserRole.SeniorModerator, UserRole.SeniorModerator)]
    [InlineData(UserRole.SeniorModerator, UserRole.Admin)]
    [InlineData(UserRole.Admin, UserRole.Admin)]
    public async Task RefuseAWarningToAnEqualOrHigherRank(UserRole issuer, UserRole target)
    {
        _identityProvider.Current.Returns(Identity.Success(
            new AuthenticatedUser { UserId = _moderatorUserId, Role = issuer, Username = "Issuer" },
            new Session { Id = Guid.NewGuid() },
            new UserSettings(),
            "token"));
        _userLookupService.GetAsync("Target")
            .Returns(new GeneralUser { UserId = _targetUserId, Username = "Target", Role = target });

        var act = () => _service.CreateWarning(new CreateWarning { Username = "Target", Reason = "Флуд", Points = 1 });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
        await _warningRepository.DidNotReceive().Create(Arg.Any<CreateWarningEntity>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// And reaches every rank below: the reader, the mentor, and for a senior
    /// moderator and an administrator the moderators under them.
    /// </summary>
    [Theory]
    [InlineData(UserRole.Moderator, UserRole.RegularUser)]
    [InlineData(UserRole.Moderator, UserRole.Mentor)]
    [InlineData(UserRole.SeniorModerator, UserRole.Moderator)]
    [InlineData(UserRole.Admin, UserRole.SeniorModerator)]
    public async Task IssueAWarningToALowerRank(UserRole issuer, UserRole target)
    {
        _identityProvider.Current.Returns(Identity.Success(
            new AuthenticatedUser { UserId = _moderatorUserId, Role = issuer, Username = "Issuer" },
            new Session { Id = Guid.NewGuid() },
            new UserSettings(),
            "token"));
        _userLookupService.GetAsync("Target")
            .Returns(new GeneralUser { UserId = _targetUserId, Username = "Target", Role = target });
        _warningRepository.Create(Arg.Any<CreateWarningEntity>(), Arg.Any<CancellationToken>())
            .Returns(new Warning { WarningId = _warningId });

        await _service.CreateWarning(new CreateWarning { Username = "Target", Reason = "Флуд", Points = 1 });

        await _warningRepository.Received(1).Create(
            Arg.Is<CreateWarningEntity>(w => w.TargetUserId == _targetUserId), Arg.Any<CancellationToken>());
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

    /// <summary>
    /// The warning that carries the total to the threshold hands the reader to
    /// the site's own ban.
    /// </summary>
    /// <remarks>
    /// The whole of the automatic ban from this side: the service reads the total
    /// before the write and after it, and asks for the ban on the crossing. What
    /// the ban is made of - two days, the ordinary scope, the system account - is
    /// BanService's, and BanServiceShould holds it.
    /// </remarks>
    [Fact]
    public async Task BanTheReaderTheLastPointCarriesOverTheThreshold()
    {
        _userLookupService.GetAsync("Target")
            .Returns(new GeneralUser { UserId = _targetUserId, Username = "Target" });
        _warningRepository.Create(Arg.Any<CreateWarningEntity>(), Arg.Any<CancellationToken>())
            .Returns(new Warning { WarningId = _warningId });
        // Four before the write, six after it.
        _warningRepository.GetUserWarningPoints(_targetUserId, Arg.Any<CancellationToken>())
            .Returns(4, 6);

        await _service.CreateWarning(new CreateWarning
        {
            Username = "Target",
            Reason = "Оскорбление",
            Points = 2
        });

        await _banService.Received(1).IssueAutomaticBan(_targetUserId, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A reader who already stood at the threshold is not banned again by the
    /// next warning.
    /// </summary>
    /// <remarks>
    /// Firing on the standing rather than on the crossing would put a fresh
    /// two-day window on him at every touch of his record for as long as the
    /// points live, including the moment the previous window ran out.
    /// </remarks>
    [Fact]
    public async Task NotBanAgainAReaderWhoWasAlreadyOverTheThreshold()
    {
        _userLookupService.GetAsync("Target")
            .Returns(new GeneralUser { UserId = _targetUserId, Username = "Target" });
        _warningRepository.Create(Arg.Any<CreateWarningEntity>(), Arg.Any<CancellationToken>())
            .Returns(new Warning { WarningId = _warningId });
        _warningRepository.GetUserWarningPoints(_targetUserId, Arg.Any<CancellationToken>())
            .Returns(6, 8);

        await _service.CreateWarning(new CreateWarning
        {
            Username = "Target",
            Reason = "Оскорбление",
            Points = 2
        });

        await _banService.DidNotReceive().IssueAutomaticBan(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Below the threshold nothing is issued: the points are a record until they
    /// add up to it.
    /// </summary>
    [Fact]
    public async Task NotBanBelowTheThreshold()
    {
        _userLookupService.GetAsync("Target")
            .Returns(new GeneralUser { UserId = _targetUserId, Username = "Target" });
        _warningRepository.Create(Arg.Any<CreateWarningEntity>(), Arg.Any<CancellationToken>())
            .Returns(new Warning { WarningId = _warningId });
        _warningRepository.GetUserWarningPoints(_targetUserId, Arg.Any<CancellationToken>())
            .Returns(1, 3);

        await _service.CreateWarning(new CreateWarning
        {
            Username = "Target",
            Reason = "Флуд",
            Points = 2
        });

        await _banService.DidNotReceive().IssueAutomaticBan(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A correction upwards reaches the threshold by the same door.
    /// </summary>
    /// <remarks>
    /// Raising the points of an old warning adds to the same total a fresh
    /// warning adds to, and the reader is over the line either way.
    /// </remarks>
    [Fact]
    public async Task BanWhenARaisedCorrectionCarriesTheTotalOverTheThreshold()
    {
        _warningRepository.Get(_warningId, Arg.Any<CancellationToken>()).Returns(Issued());
        _warningRepository.ChangePoints(_warningId, 6, _moderatorUserId, _now, "Нарушение тяжелее",
            Arg.Any<CancellationToken>()).Returns(true);
        _warningRepository.GetUserWarningPoints(_targetUserId, Arg.Any<CancellationToken>())
            .Returns(3, 6);

        await _service.ChangeWarningPoints(_warningId, 6, "Нарушение тяжелее");

        await _banService.Received(1).IssueAutomaticBan(_targetUserId, Arg.Any<CancellationToken>());
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

    /// <summary>
    /// A warning already in the repository, issued by the moderator this fixture
    /// authenticates as.
    /// </summary>
    private Warning Issued(int points = 3, Guid? authorId = null, Guid? warningId = null) => new()
    {
        WarningId = warningId ?? _warningId,
        Points = points,
        OriginalPoints = points,
        Author = new GeneralUser { UserId = authorId ?? _moderatorUserId, Username = "Moderator" },
        TargetUser = new GeneralUser { UserId = _targetUserId, Username = "Target" },
        CreatedUtc = _now
    };

    private void Authenticate(Guid userId, UserRole role, string username) =>
        _identityProvider.Current.Returns(Identity.Success(
            new AuthenticatedUser { UserId = userId, Role = role, Username = username },
            new Session { Id = Guid.NewGuid() },
            new UserSettings(),
            "token"));

    /// <summary>
    /// The moderator who wrote the warning may correct its points.
    /// </summary>
    /// <remarks>
    /// Half of the rule the owner set: a correction is open to the senior ranks
    /// and to whoever issued the warning, because the person best placed to say
    /// the points were wrong is often the one who set them.
    /// </remarks>
    [Fact]
    public async Task LetTheAuthorOfTheWarningChangeItsPoints()
    {
        _warningRepository.Get(_warningId, Arg.Any<CancellationToken>()).Returns(Issued());
        _warningRepository.ChangePoints(_warningId, 0, _moderatorUserId, _now, "Ошибка модератора",
            Arg.Any<CancellationToken>()).Returns(true);

        await _service.ChangeWarningPoints(_warningId, 0, "Ошибка модератора");

        await _warningRepository.Received(1).ChangePoints(
            _warningId, 0, _moderatorUserId, _now, "Ошибка модератора", Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A senior moderator may correct anybody's warning.
    /// </summary>
    [Fact]
    public async Task LetASeniorModeratorChangeThePointsOfSomebodyElsesWarning()
    {
        var seniorId = Guid.NewGuid();
        Authenticate(seniorId, UserRole.SeniorModerator, "Senior");
        _warningRepository.Get(_warningId, Arg.Any<CancellationToken>()).Returns(Issued());
        _warningRepository.ChangePoints(_warningId, 1, seniorId, _now, "Нарушение легче",
            Arg.Any<CancellationToken>()).Returns(true);

        await _service.ChangeWarningPoints(_warningId, 1, "Нарушение легче");

        await _warningRepository.Received(1).ChangePoints(
            _warningId, 1, seniorId, _now, "Нарушение легче", Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// An ordinary moderator does not touch a warning another moderator issued.
    /// </summary>
    /// <remarks>
    /// The refusal is the general one rather than one naming a rank: the rule is
    /// a rank or an authorship, and no rank is the whole of it.
    /// </remarks>
    [Fact]
    public async Task RefuseAModeratorThePointsOfSomebodyElsesWarning()
    {
        Authenticate(Guid.NewGuid(), UserRole.Moderator, "OtherModerator");
        _warningRepository.Get(_warningId, Arg.Any<CancellationToken>()).Returns(Issued());

        var act = () => _service.ChangeWarningPoints(_warningId, 0, "Считаю решение неверным");

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
        await _warningRepository.DidNotReceive().ChangePoints(
            Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A warning that does not exist is answered with 404, not with a write.
    /// </summary>
    [Fact]
    public async Task AnswerWithNotFoundWhenTheWarningIsNotThere()
    {
        _warningRepository.Get(_warningId, Arg.Any<CancellationToken>()).Returns((Warning?)null);

        var act = () => _service.ChangeWarningPoints(_warningId, 0, "Ошибка модератора");

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
        await _warningRepository.DidNotReceive().ChangePoints(
            Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    public async Task RejectChangedPointsOutsideZeroToSixRange(int points)
    {
        var act = () => _service.ChangeWarningPoints(_warningId, points, "Ошибка модератора");

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    /// <summary>
    /// The explanation is required: the log shows the correction beside the
    /// original reason, and a correction with nothing written under it leaves a
    /// verdict the points no longer agree with.
    /// </summary>
    [Fact]
    public async Task RejectAChangeOfPointsWithNoExplanation()
    {
        var act = () => _service.ChangeWarningPoints(_warningId, 0, "  ");

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    [Fact]
    public async Task RejectAnExplanationLongerThanTwoThousandCharacters()
    {
        var act = () => _service.ChangeWarningPoints(_warningId, 0, new string('a', 2001));

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    /// <summary>
    /// The person carrying the points is told that they moved.
    /// </summary>
    /// <remarks>
    /// The correction is the only thing that ever happens to a warning after it
    /// is issued, and it used to happen in silence: points taken off by a
    /// moderator's mistake, or raised, changed the violation scale of somebody
    /// nobody told, and he learned of it by opening the log himself.
    /// </remarks>
    [Fact]
    public async Task AnnounceAChangeOfWarningPoints()
    {
        _warningRepository.Get(_warningId, Arg.Any<CancellationToken>()).Returns(Issued());
        _warningRepository.ChangePoints(_warningId, 0, _moderatorUserId, _now, "Ошибка модератора",
            Arg.Any<CancellationToken>()).Returns(true);

        await _service.ChangeWarningPoints(_warningId, 0, "Ошибка модератора");

        await _eventProducer.Received(1).SendAsync(EventType.WarningPointsChanged, _warningId);
    }

    /// <summary>
    /// A refusal announces nothing: nothing was corrected.
    /// </summary>
    /// <remarks>
    /// Three shapes of refusal, and the notification is the one part of the
    /// feature that reaches a person outside moderation. An event sent past any
    /// of them tells the warned reader that his points changed when they did
    /// not, and the letter carries the numbers off the untouched row to prove
    /// it.
    /// </remarks>
    [Fact]
    public async Task AnnounceNothingWhenTheChangeIsRefusedToTheModerator()
    {
        Authenticate(Guid.NewGuid(), UserRole.Moderator, "OtherModerator");
        _warningRepository.Get(_warningId, Arg.Any<CancellationToken>()).Returns(Issued());

        var act = () => _service.ChangeWarningPoints(_warningId, 0, "Считаю решение неверным");

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
        await _eventProducer.DidNotReceive().SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>());
    }

    [Fact]
    public async Task AnnounceNothingWhenTheWarningToChangeIsNotThere()
    {
        _warningRepository.Get(_warningId, Arg.Any<CancellationToken>()).Returns((Warning?)null);

        var act = () => _service.ChangeWarningPoints(_warningId, 0, "Ошибка модератора");

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
        await _eventProducer.DidNotReceive().SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>());
    }

    [Fact]
    public async Task AnnounceNothingWhenTheChangeIsOutsideTheRules()
    {
        _warningRepository.Get(_warningId, Arg.Any<CancellationToken>()).Returns(Issued());

        var act = () => _service.ChangeWarningPoints(_warningId, 7, "Слишком много баллов");

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
        await _eventProducer.DidNotReceive().SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>());
    }

    /// <summary>
    /// The picture of a warning on a profile is deleted in the name of the
    /// moderator who asked and at the moment he asked: that mark is what takes it
    /// out of both logs and hands the file to the sweeper.
    /// </summary>
    [Fact]
    public async Task DeleteTheProfilePictureInTheNameOfTheModerator()
    {
        _warningRepository.GetTargetUserId(_warningId, Arg.Any<CancellationToken>()).Returns(_targetUserId);
        _warningRepository
            .DeleteProfilePicture(_warningId, _moderatorUserId, _now, Arg.Any<CancellationToken>())
            .Returns(true);

        await _service.DeleteProfilePicture(_warningId);

        await _warningRepository.Received(1).DeleteProfilePicture(
            _warningId, _moderatorUserId, _now, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A reader is refused before anything is written: the controller asks for
    /// the role as well, and this is the rule for any other caller of the service.
    /// </summary>
    [Fact]
    public async Task RefuseAReaderTheDeletionOfAProfilePicture()
    {
        Authenticate(Guid.NewGuid(), UserRole.RegularUser, "Reader");

        var act = () => _service.DeleteProfilePicture(_warningId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
        await _warningRepository.DidNotReceive().DeleteProfilePicture(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A moderator does not take the picture out of a warning issued to himself,
    /// before his promotion or by a higher rank: a party does not rule on his own
    /// case, as with tickets, and nothing is marked.
    /// </summary>
    /// <remarks>
    /// The picture is his own avatar. Marked in his name, the deletion reads as
    /// the owner's own doing to the log and to the orphan sweeper, so the picture
    /// stayed in the log while the request succeeded, and his current avatar, if
    /// it was still that file, left his profile.
    /// </remarks>
    [Fact]
    public async Task RefuseAModeratorThePictureOfAWarningIssuedToHimself()
    {
        _warningRepository.GetTargetUserId(_warningId, Arg.Any<CancellationToken>()).Returns(_moderatorUserId);
        _warningRepository
            .DeleteProfilePicture(_warningId, _moderatorUserId, _now, Arg.Any<CancellationToken>())
            .Returns(true);

        var act = () => _service.DeleteProfilePicture(_warningId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden)
            .WithMessage("Убрать картинку из записи лога о вас может только другой модератор");
        await _warningRepository.DidNotReceive().DeleteProfilePicture(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A warning nobody issued is answered with 404, the code the change of
    /// points gives for it, before anything is written.
    /// </summary>
    [Fact]
    public async Task AnswerTheDeletionOfAPictureOfAMissingWarningWithNotFound()
    {
        _warningRepository.GetTargetUserId(_warningId, Arg.Any<CancellationToken>()).Returns((Guid?)null);

        var act = () => _service.DeleteProfilePicture(_warningId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
        await _warningRepository.DidNotReceive().DeleteProfilePicture(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// So is a warning that is gone by the time of the write.
    /// </summary>
    [Fact]
    public async Task AnswerTheDeletionOfAPictureOfAWarningGoneByTheWriteWithNotFound()
    {
        _warningRepository.GetTargetUserId(_warningId, Arg.Any<CancellationToken>()).Returns(_targetUserId);
        _warningRepository
            .DeleteProfilePicture(_warningId, _moderatorUserId, _now, Arg.Any<CancellationToken>())
            .Returns(false);

        var act = () => _service.DeleteProfilePicture(_warningId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    /// <summary>
    /// The moderation list says on each row whether its reader may change the
    /// points, because the rule reads both the role of the reader and the author
    /// of the row.
    /// </summary>
    [Fact]
    public async Task MarkOnlyTheRowsThisModeratorMayChange()
    {
        var otherWarningId = Guid.NewGuid();
        _warningRepository
            .GetLog(null, Arg.Any<PagingQuery>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<Warning>)
                [Issued(), Issued(authorId: Guid.NewGuid(), warningId: otherWarningId)], 2));

        var (warnings, _) = await _service.GetAllWarnings(null, new PagingQuery());

        warnings.Single(w => w.WarningId == _warningId).CanChangePoints.Should().BeTrue();
        warnings.Single(w => w.WarningId == otherWarningId).CanChangePoints.Should().BeFalse();
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
        _warningRepository
            .GetLog(null, Arg.Any<PagingQuery>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<Warning>)[new Warning { WarningId = _warningId, Points = 3 }], 1));

        var (warnings, _) = await _service.GetAllWarnings(null, new PagingQuery());

        warnings.Should().ContainSingle(w => w.WarningId == _warningId);
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
    /// The moderation list carries the address of the offending object, its place
    /// and the date it was written — read per request, none of them stored on the
    /// warning — beside the snapshot, which is the text the warning was given for.
    /// </summary>
    [Fact]
    public async Task DescribeTheOffendingObjectOnTheModerationList()
    {
        var entityId = Guid.NewGuid();
        _warningRepository
            .GetLog(null, Arg.Any<PagingQuery>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<Warning>)[
                new Warning
                {
                    WarningId = _warningId,
                    EntityId = entityId,
                    EntityType = WarningEntityType.Topic,
                    CreatedUtc = _now,
                    EntitySnapshot = "Исходный текст"
                }
            ], 1));
        _entityResolver
            .ResolveStates(
                Arg.Any<IReadOnlyCollection<WarningEntityRequest>>(), Arg.Any<CancellationToken>()).Returns(new Dictionary<Guid, WarningEntityState>
                {
                    [_warningId] = new()
                    {
                        Url = "/forum/flood/12",
                        PlaceKind = WarningPlaceKind.Topic,
                        PlaceTitle = "Флуд",
                        CreatedUtc = _now.AddHours(-1)
                    }
                });

        var warning = (await _service.GetAllWarnings(null, new PagingQuery())).Warnings.Single();

        warning.EntitySnapshot.Should().Be("Исходный текст");
        warning.EntityUrl.Should().Be("/forum/flood/12");
        warning.PlaceKind.Should().Be(WarningPlaceKind.Topic);
        warning.PlaceTitle.Should().Be("Флуд");
        warning.EntityCreatedUtc.Should().Be(_now.AddHours(-1));
        await _entityResolver.Received(1).ResolveStates(
            Arg.Any<IReadOnlyCollection<WarningEntityRequest>>(), Arg.Any<CancellationToken>());
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

    /// <summary>
    /// A warning on a profile is a warning on the warned user: the profile is
    /// copied as it stands, the avatar pinned by its upload, and the identifier
    /// the request named is not read.
    /// </summary>
    /// <remarks>
    /// The kind used to be unknown to the server, so the warning was stored as
    /// one that names nothing and the log could not show what it was given for
    /// (the owner, 12.10, row 41). The object is set here and not taken from the
    /// request, so no request can make a warning carry somebody else's profile.
    /// </remarks>
    [Fact]
    public async Task TakeTheProfileOfTheWarnedUserWhicheverIdentifierTheRequestNamed()
    {
        var avatarUploadId = Guid.NewGuid();
        _userLookupService.GetAsync("Target").Returns(new GeneralUser { UserId = _targetUserId, Username = "Target" });
        _entityResolver.CaptureProfile(_targetUserId, Arg.Any<CancellationToken>()).Returns(new ProfileCapture
        {
            Status = "Статус на момент выдачи",
            Info = "[b]О себе[/b] на момент выдачи",
            AvatarUploadId = avatarUploadId
        });

        CreateWarningEntity? captured = null;
        _warningRepository.Create(Arg.Any<CreateWarningEntity>(), Arg.Any<CancellationToken>())
            .Returns(new Warning())
            .AndDoes(ci => captured = ci.ArgAt<CreateWarningEntity>(0));

        await _service.CreateWarning(new CreateWarning
        {
            Username = "Target",
            EntityId = Guid.NewGuid(),
            EntityType = "Profile",
            Reason = "Неприемлемый аватар",
            Points = 3
        });

        captured.Should().NotBeNull();
        captured!.EntityType.Should().Be(WarningEntityType.Profile);
        captured.EntityId.Should().Be(_targetUserId, "the object of a warning on a profile is the warned user");
        captured.EntitySnapshot.Should().Be("[b]О себе[/b] на момент выдачи");
        captured.ProfileStatusSnapshot.Should().Be("Статус на момент выдачи");
        captured.ProfileAvatarUploadId.Should().Be(avatarUploadId);
        await _entityResolver.Received(1).CaptureProfile(_targetUserId, Arg.Any<CancellationToken>());
        await _entityResolver.DidNotReceive().CaptureSnapshot(
            Arg.Any<WarningEntityType>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A warning on a profile has no place on the site: it is not sent to the
    /// resolver, and its "О себе" in the snapshot does not make it a text that
    /// nothing places.
    /// </summary>
    /// <remarks>
    /// Without the exception the copy of "О себе" sent the row down the branch of
    /// an unplaced text and closed it as not found, a state true of no profile.
    /// </remarks>
    [Fact]
    public async Task LeaveAWarningOnAProfileWithoutAPlace()
    {
        _identityProvider.Current.Returns(Identity.Guest());
        var onProfile = new Warning
        {
            WarningId = _warningId,
            EntityId = _targetUserId,
            EntityType = WarningEntityType.Profile,
            CreatedUtc = _now,
            EntitySnapshot = "О себе на момент выдачи"
        };
        _warningRepository
            .GetLog(null, Arg.Any<PagingQuery>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<Warning>)[onProfile], 1));

        var warning = (await _service.GetLog(null, new PagingQuery())).Warnings.Single();

        await _entityResolver.DidNotReceive().ResolveStates(
            Arg.Any<IReadOnlyCollection<WarningEntityRequest>>(), Arg.Any<CancellationToken>());
        warning.LogClosure.Should().BeNull("a profile is not a text nothing can place");
        warning.EntityUrl.Should().BeNull();
        warning.PlaceKind.Should().BeNull();
    }

    /// <summary>
    /// The running ban of a violator says whether the reader may lift it, by the
    /// rule the lift is refused by, as every other ban list does.
    /// </summary>
    /// <remarks>
    /// The bans of this list are read from the repository past BanService, and the
    /// flag stayed false for everybody.
    /// </remarks>
    [Fact]
    public async Task MarkTheRunningBanOfAViolatorWithWhetherTheReaderMayLiftIt()
    {
        var liftable = new Ban
        {
            BanId = Guid.NewGuid(),
            TargetUser = new GeneralUser { UserId = Guid.NewGuid(), Username = "Liftable" },
            EndedUtc = _now.AddDays(3)
        };
        var permanent = new Ban
        {
            BanId = Guid.NewGuid(),
            TargetUser = new GeneralUser { UserId = Guid.NewGuid(), Username = "Permanent" },
            EndedUtc = _now.AddYears(100)
        };
        _warningRepository.GetActiveWarningSummaries(Arg.Any<CancellationToken>()).Returns([]);
        _banRepository.GetAllActiveBans(Arg.Any<CancellationToken>()).Returns([liftable, permanent]);
        _banService.MayLift(liftable).Returns(true);
        _banService.MayLift(permanent).Returns(false);

        var violators = (await _service.GetViolators()).ToList();

        violators.Single(v => v.User.Username == "Liftable").ActiveBan!.CanLift.Should().BeTrue();
        violators.Single(v => v.User.Username == "Permanent").ActiveBan!.CanLift.Should().BeFalse();
    }
}
