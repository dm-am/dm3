using System;
using System.Threading;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Users;
using DM.Domain.Moderation.Features.Bans;
using DM.Testing;
using DM.Testing.Dsl;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;

namespace DM.Domain.Moderation.Tests.Dsl;

/// <summary>
/// The ban service over substitutes of everything it asks: a validator that
/// passes, a signed-in senior moderator and a clock that stands still.
/// </summary>
public abstract class BanServiceTestBase : UnitTestBase
{
    protected readonly IBanRepository banRepository;
    protected readonly IUserLookupService userLookupService;
    protected readonly IIdentityProvider identityProvider;
    protected readonly IGuidFactory guidFactory;
    protected readonly IDateTimeProvider dateTimeProvider;
    protected readonly IEventProducer eventProducer;
    private protected readonly BanService service;
    protected readonly Guid moderatorUserId = Guid.NewGuid();
    protected readonly Guid targetUserId = Guid.NewGuid();
    protected readonly Guid banId = Guid.NewGuid();
    protected readonly DateTimeOffset now = DateTimeOffset.UtcNow;

    protected BanServiceTestBase()
    {
        banRepository = Mock<IBanRepository>();
        userLookupService = Mock<IUserLookupService>();
        identityProvider = Mock<IIdentityProvider>();
        guidFactory = Mock<IGuidFactory>();
        dateTimeProvider = Mock<IDateTimeProvider>();
        eventProducer = Mock<IEventProducer>();

        // Ban creation and lifting require SeniorModerator, so tests default to it
        SetCurrentUser(UserRole.SeniorModerator);
        dateTimeProvider.Now.Returns(now);
        guidFactory.Create().Returns(banId);

        var createValidator = Mock<IValidator<CreateBan>>();
        createValidator
            .ValidateAsync(Arg.Any<ValidationContext<CreateBan>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        service = new BanService(
            banRepository,
            userLookupService,
            identityProvider,
            guidFactory,
            dateTimeProvider,
            createValidator,
            eventProducer);
    }

    protected void SetCurrentUser(UserRole role)
    {
        identityProvider.Current.Returns(Identities.User(moderatorUserId, "Moderator", role));
    }

    /// <summary>
    /// A ban of the kind a case of the table names, for the reader of
    /// <see cref="SetCurrentUser" />.
    /// </summary>
    protected Ban BanOfKind(string kind) => kind switch
    {
        "running" => new Ban
        {
            BanId = banId,
            TargetUserId = targetUserId,
            StartedUtc = now.AddDays(-1),
            EndedUtc = now.AddDays(6)
        },
        "own" => new Ban
        {
            BanId = banId,
            TargetUserId = moderatorUserId,
            StartedUtc = now.AddDays(-1),
            EndedUtc = now.AddDays(6)
        },
        "permanent" => new Ban
        {
            BanId = banId,
            TargetUserId = targetUserId,
            StartedUtc = now.AddDays(-1),
            EndedUtc = now.AddDays(-1).AddYears(Ban.PermanentYears)
        },
        "voluntary" => new Ban
        {
            BanId = banId,
            TargetUserId = targetUserId,
            IsVoluntary = true,
            StartedUtc = now.AddDays(-1),
            EndedUtc = now.AddDays(-1).AddYears(Ban.PermanentYears)
        },
        "lifted" => new Ban
        {
            BanId = banId,
            TargetUserId = targetUserId,
            StartedUtc = now.AddDays(-1),
            EndedUtc = now.AddDays(6),
            LiftedUtc = now.AddHours(-1)
        },
        "expired" => new Ban
        {
            BanId = banId,
            TargetUserId = targetUserId,
            StartedUtc = now.AddDays(-9),
            EndedUtc = now.AddDays(-2)
        },
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}
