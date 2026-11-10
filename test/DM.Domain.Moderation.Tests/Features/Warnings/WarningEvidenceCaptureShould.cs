using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Moderation.Features.Warnings;
using DM.Domain.Moderation.Tests.Dsl;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Warnings;

public class WarningEvidenceCaptureShould : WarningServiceTestBase
{
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
        userLookupService.GetAsync("Target").Returns(new GeneralUser { UserId = targetUserId, Username = "Target" });
        entityResolver
            .CaptureSnapshot(WarningEntityType.Comment, entityId, Arg.Any<CancellationToken>())
            .Returns("Оскорбление, за которое выносится предупреждение");

        CreateWarningEntity? captured = null;
        warningRepository.Create(Arg.Do<CreateWarningEntity>(e => captured = e), Arg.Any<CancellationToken>())
            .Returns(new Warning());

        await service.CreateWarning(new CreateWarning
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

    [Fact]
    public async Task TakeNoSnapshotWhenTheWarningNamesNoContent()
    {
        userLookupService.GetAsync("Target").Returns(new GeneralUser { UserId = targetUserId, Username = "Target" });

        CreateWarningEntity? captured = null;
        warningRepository.Create(Arg.Do<CreateWarningEntity>(e => captured = e), Arg.Any<CancellationToken>())
            .Returns(new Warning());

        await service.CreateWarning(new CreateWarning
        {
            Username = "Target",
            Reason = "Общее замечание",
            Points = 1
        });

        captured.Should().NotBeNull();
        captured!.EntitySnapshot.Should().BeNull();
        await entityResolver.DidNotReceive().CaptureSnapshot(
                Arg.Any<WarningEntityType>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DescribeTheOffendingObjectOnTheModerationList()
    {
        var entityId = Guid.NewGuid();
        warningRepository
            .GetLog(null, Arg.Any<PagingQuery>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<Warning>)[
                new Warning
                {
                    WarningId = warningId,
                    EntityId = entityId,
                    EntityType = WarningEntityType.Topic,
                    CreatedUtc = now,
                    EntitySnapshot = "Исходный текст"
                }
            ], 1));
        entityResolver
            .ResolveStates(
                Arg.Any<IReadOnlyCollection<WarningEntityRequest>>(), Arg.Any<CancellationToken>()).Returns(new Dictionary<Guid, WarningEntityState>
                {
                    [warningId] = new()
                    {
                        Url = "/forum/flood/12",
                        PlaceKind = WarningPlaceKind.Topic,
                        PlaceTitle = "Флуд",
                        CreatedUtc = now.AddHours(-1)
                    }
                });

        var warning = (await service.GetAllWarnings(null, new PagingQuery())).Warnings.Single();

        warning.EntitySnapshot.Should().Be("Исходный текст");
        warning.EntityUrl.Should().Be("/forum/flood/12");
        warning.PlaceKind.Should().Be(WarningPlaceKind.Topic);
        warning.PlaceTitle.Should().Be("Флуд");
        warning.EntityCreatedUtc.Should().Be(now.AddHours(-1));
        await entityResolver.Received(1).ResolveStates(
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
        userLookupService.GetAsync("Target").Returns(new GeneralUser { UserId = targetUserId, Username = "Target" });
        warningRepository.GetUserWarnings(targetUserId, Arg.Any<CancellationToken>()).Returns([
                new Warning
                {
                    WarningId = warningId,
                    EntityId = Guid.NewGuid(),
                    EntityType = WarningEntityType.Comment,
                    CreatedUtc = now
                }
            ]);

        await service.GetUserWarnings("Target");

        await entityResolver.DidNotReceive().ResolveStates(
                Arg.Any<IReadOnlyCollection<WarningEntityRequest>>(), Arg.Any<CancellationToken>());
    }

    // The object is set here and not taken from the request, so no request can make a warning carry
    // somebody else's profile.
    [Fact]
    public async Task TakeTheProfileOfTheWarnedUserWhicheverIdentifierTheRequestNamed()
    {
        var avatarUploadId = Guid.NewGuid();
        userLookupService.GetAsync("Target").Returns(new GeneralUser { UserId = targetUserId, Username = "Target" });
        entityResolver.CaptureProfile(targetUserId, Arg.Any<CancellationToken>()).Returns(new ProfileCapture
        {
            Status = "Статус на момент выдачи",
            Info = "[b]О себе[/b] на момент выдачи",
            AvatarUploadId = avatarUploadId
        });

        CreateWarningEntity? captured = null;
        warningRepository.Create(Arg.Do<CreateWarningEntity>(e => captured = e), Arg.Any<CancellationToken>())
            .Returns(new Warning());

        await service.CreateWarning(new CreateWarning
        {
            Username = "Target",
            EntityId = Guid.NewGuid(),
            EntityType = "Profile",
            Reason = "Неприемлемый аватар",
            Points = 3
        });

        captured.Should().NotBeNull();
        captured!.EntityType.Should().Be(WarningEntityType.Profile);
        captured.EntityId.Should().Be(targetUserId, "the object of a warning on a profile is the warned user");
        captured.EntitySnapshot.Should().Be("[b]О себе[/b] на момент выдачи");
        captured.ProfileStatusSnapshot.Should().Be("Статус на момент выдачи");
        captured.ProfileAvatarUploadId.Should().Be(avatarUploadId);
        await entityResolver.Received(1).CaptureProfile(targetUserId, Arg.Any<CancellationToken>());
        await entityResolver.DidNotReceive().CaptureSnapshot(
            Arg.Any<WarningEntityType>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // A warning on a profile has no place on the site: it is not sent to the resolver, and its "О
    // себе" in the snapshot does not make it a text that nothing places.
    [Fact]
    public async Task LeaveAWarningOnAProfileWithoutAPlace()
    {
        identityProvider.Current.Returns(Identity.Guest());
        var onProfile = new Warning
        {
            WarningId = warningId,
            EntityId = targetUserId,
            EntityType = WarningEntityType.Profile,
            CreatedUtc = now,
            EntitySnapshot = "О себе на момент выдачи"
        };
        warningRepository
            .GetLog(null, Arg.Any<PagingQuery>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<Warning>)[onProfile], 1));

        var warning = (await service.GetLog(null, new PagingQuery())).Warnings.Single();

        await entityResolver.DidNotReceive().ResolveStates(
            Arg.Any<IReadOnlyCollection<WarningEntityRequest>>(), Arg.Any<CancellationToken>());
        warning.LogClosure.Should().BeNull("a profile is not a text nothing can place");
        warning.EntityUrl.Should().BeNull();
        warning.PlaceKind.Should().BeNull();
    }
}
