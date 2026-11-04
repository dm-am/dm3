using System;
using System.Collections.Generic;
using System.Threading;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Users;
using DM.Domain.Moderation.Features.Warnings;
using DM.Domain.Moderation.Features.Bans;
using DM.Testing;
using DM.Testing.Dsl;
using NSubstitute;

namespace DM.Domain.Moderation.Tests.Dsl;

/// <summary>
/// The warning service with its own validators over substitutes of everything
/// else it asks: a signed-in moderator, a clock that stands still, and a
/// resolver that knows nothing about any offending object.
/// </summary>
public abstract class WarningServiceTestBase : UnitTestBase
{
    protected readonly IWarningRepository warningRepository;
    protected readonly IBanRepository banRepository;
    protected readonly IBanService banService;
    protected readonly IWarningEntityResolver entityResolver;
    protected readonly IUserLookupService userLookupService;
    protected readonly IIdentityProvider identityProvider;
    protected readonly IGuidFactory guidFactory;
    protected readonly IDateTimeProvider dateTimeProvider;
    protected readonly IEventProducer eventProducer;

    /// <summary>
    /// The game reading the open log asks about a game closed to a guest: it reads none, unless a test says so.
    /// </summary>
    protected readonly IPlaceReading gameReading;
    private protected readonly WarningService service;
    protected readonly Guid moderatorUserId = Guid.NewGuid();
    protected readonly Guid targetUserId = Guid.NewGuid();
    protected readonly Guid warningId = Guid.NewGuid();
    protected readonly DateTimeOffset now = DateTimeOffset.UtcNow;

    protected WarningServiceTestBase()
    {
        warningRepository = Mock<IWarningRepository>();
        banRepository = Mock<IBanRepository>();
        banService = Mock<IBanService>();
        entityResolver = Mock<IWarningEntityResolver>();
        userLookupService = Mock<IUserLookupService>();
        identityProvider = Mock<IIdentityProvider>();
        guidFactory = Mock<IGuidFactory>();
        dateTimeProvider = Mock<IDateTimeProvider>();
        eventProducer = Mock<IEventProducer>();
        gameReading = Mock<IPlaceReading>();
        gameReading.Kind.Returns(ReadablePlaceKind.Game);
        gameReading.ReadableAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid>());

        identityProvider.Current.Returns(Identities.User(moderatorUserId, "Moderator", UserRole.Moderator));
        dateTimeProvider.Now.Returns(now);
        guidFactory.Create().Returns(warningId);
        // Default: nothing is known about any offending object. Tests that care
        // about the evidence override this.
        entityResolver
            .ResolveStates(
                Arg.Any<IReadOnlyCollection<WarningEntityRequest>>(), Arg.Any<CancellationToken>()).Returns(new Dictionary<Guid, WarningEntityState>());

        service = new WarningService(
            new CreateWarningValidator(),
            new ChangeWarningPointsValidator(),
            warningRepository,
            banRepository,
            banService,
            entityResolver,
            userLookupService,
            identityProvider,
            guidFactory,
            dateTimeProvider,
            eventProducer,
            [gameReading]);
    }

    /// <summary>
    /// A warning already in the repository, issued by the moderator this fixture
    /// authenticates as.
    /// </summary>
    protected Warning Issued(int points = 3, Guid? authorId = null, Guid? id = null) => new()
    {
        WarningId = id ?? warningId,
        Points = points,
        OriginalPoints = points,
        Author = new GeneralUser { UserId = authorId ?? moderatorUserId, Username = "Moderator" },
        TargetUser = new GeneralUser { UserId = targetUserId, Username = "Target" },
        CreatedUtc = now
    };

    protected void Authenticate(Guid userId, UserRole role, string username) =>
        identityProvider.Current.Returns(Identities.User(userId, username, role));
}
