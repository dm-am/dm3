using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Moderation.Features.Warnings;
using DM.Domain.Moderation.Tests.Dsl;
using AwesomeAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Warnings;

public class WarningLogShould : WarningServiceTestBase
{
    [Fact]
    public async Task ReturnEmptyListWhenGettingWarningsForNonexistentUser()
    {
        userLookupService.GetAsync("Unknown")
            .ThrowsAsync(new HttpException(HttpStatusCode.NotFound, "Пользователь не найден"));

        var result = await service.GetUserWarnings("Unknown");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task LetAStorageFailureThroughInsteadOfReportingNoViolations()
    {
        // "No warnings" and "we could not read them" look the same to the
        // moderator and the same in the log, because there is no log.
        userLookupService.GetAsync("Target").Returns(new GeneralUser { UserId = targetUserId, Username = "Target" });
        warningRepository.GetUserWarnings(targetUserId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("connection reset"));

        var act = () => service.GetUserWarnings("Target");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task LetAStorageFailureThroughInsteadOfReportingZeroPoints()
    {
        userLookupService.GetAsync("Target").Returns(new GeneralUser { UserId = targetUserId, Username = "Target" });
        warningRepository
            .GetUserWarningPoints(targetUserId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("connection reset"));

        var act = () => service.GetUserWarningPoints("Target");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ThrowWhenNonModeratorTriesToViewAllWarnings()
    {
        Authenticate(Guid.NewGuid(), UserRole.RegularUser, "User");

        var act = () => service.GetAllWarnings(null, new PagingQuery());

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden)
            .WithMessage("Список предупреждений доступен модераторам");
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
        identityProvider.Current.Returns(Identity.Guest());
        warningRepository
            .GetLog(null, Arg.Any<PagingQuery>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<Warning>)[new Warning { WarningId = warningId, Points = 3 }], 1));

        var (warnings, paging) = await service.GetLog(null, new PagingQuery());

        warnings.Should().ContainSingle(w => w.WarningId == warningId);
        paging.TotalEntitiesCount.Should().Be(1);
    }

    [Fact]
    public async Task DescribeTheOffendingObjectOnTheOpenLog()
    {
        identityProvider.Current.Returns(Identity.Guest());
        AClosedGameOnTheLog();

        var warning = (await service.GetLog(null, new PagingQuery())).Warnings.Single();

        await entityResolver.Received(1).ResolveStates(
            Arg.Any<IReadOnlyCollection<WarningEntityRequest>>(), Arg.Any<CancellationToken>());
        warning.PlaceKind.Should().Be(WarningPlaceKind.Game);
        warning.PlaceTitle.Should().Be("Тайны Древних");
        warning.EntityCreatedUtc.Should().Be(now.AddHours(-1));
        warning.LogClosure.Should().Be(WarningLogClosure.PlaceNotPublic);
    }

    private Guid AClosedGameOnTheLog(WarningLogClosure? closureForReader = null)
    {
        var gameId = Guid.NewGuid();
        warningRepository
            .GetLog(null, Arg.Any<PagingQuery>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<Warning>)[
                new Warning
                {
                    WarningId = warningId,
                    EntityId = Guid.NewGuid(),
                    EntityType = WarningEntityType.Comment,
                    CreatedUtc = now,
                    EntitySnapshot = "Исходный текст"
                }
            ], 1));
        entityResolver
            .ResolveStates(Arg.Any<IReadOnlyCollection<WarningEntityRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, WarningEntityState>
            {
                [warningId] = new()
                {
                    PlaceKind = WarningPlaceKind.Game,
                    PlaceTitle = "Тайны Древних",
                    CreatedUtc = now.AddHours(-1),
                    LogClosure = WarningLogClosure.PlaceNotPublic,
                    ClosedPlace = new ReadablePlace(ReadablePlaceKind.Game, gameId),
                    ClosureForReader = closureForReader
                }
            });
        return gameId;
    }

    [Fact]
    public async Task OpenAClosedPlaceToTheViewerWhoReadsIt()
    {
        var gameId = AClosedGameOnTheLog();
        gameReading.ReadableAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid> { gameId });

        var warning = (await service.GetLog(null, new PagingQuery())).Warnings.Single();

        warning.LogClosure.Should().BeNull();
        warning.LogShowsText.Should().BeTrue();
        await gameReading.Received(1).ReadableAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { gameId })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task KeepTheClosureOfTheTextForTheViewerWhoReadsThePlace()
    {
        var gameId = AClosedGameOnTheLog(WarningLogClosure.RemovedByModeration);
        gameReading.ReadableAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid> { gameId });

        var warning = (await service.GetLog(null, new PagingQuery())).Warnings.Single();

        warning.LogClosure.Should().Be(WarningLogClosure.RemovedByModeration);
        warning.LogShowsText.Should().BeTrue();
    }

    /// <summary>
    /// A viewer the game's read refuses keeps the place closed, as one it does not find.
    /// </summary>
    [Fact]
    public async Task KeepAClosedPlaceClosedToTheViewerWhoDoesNotReadIt()
    {
        AClosedGameOnTheLog();

        var warning = (await service.GetLog(null, new PagingQuery())).Warnings.Single();

        warning.LogClosure.Should().Be(WarningLogClosure.PlaceNotPublic);
        warning.LogShowsText.Should().BeFalse();
    }

    [Fact]
    public async Task AskNothingForAGuest()
    {
        identityProvider.Current.Returns(Identity.Guest());
        AClosedGameOnTheLog();

        var warning = (await service.GetLog(null, new PagingQuery())).Warnings.Single();

        warning.LogShowsText.Should().BeFalse();
        await gameReading.DidNotReceive().ReadableAsync(
            Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A copy of a text that nothing can place is closed to the open log: the
    /// question whether a guest could read it there has no answer.
    /// </summary>
    [Fact]
    public async Task CloseTheOpenLogOverATextNothingPlaces()
    {
        identityProvider.Current.Returns(Identity.Guest());
        warningRepository
            .GetLog(null, Arg.Any<PagingQuery>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<Warning>)[
                new Warning
                {
                    WarningId = warningId,
                    EntityId = Guid.Empty,
                    EntityType = WarningEntityType.Unknown,
                    CreatedUtc = now,
                    EntitySnapshot = "Текст из переноса без места"
                },
                new Warning
                {
                    WarningId = Guid.NewGuid(),
                    EntityId = Guid.Empty,
                    EntityType = WarningEntityType.Unknown,
                    CreatedUtc = now
                }
            ], 2));

        var warnings = (await service.GetLog(null, new PagingQuery())).Warnings;

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
        userLookupService.GetAsync("Unknown")
            .ThrowsAsync(new HttpException(HttpStatusCode.NotFound, "Пользователь не найден"));

        var (warnings, paging) = await service.GetLog("Unknown", new PagingQuery());

        warnings.Should().BeEmpty();
        paging.TotalEntitiesCount.Should().Be(0);
        await warningRepository.DidNotReceive().GetLog(
            Arg.Any<Guid?>(), Arg.Any<PagingQuery>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A named reader narrows the read itself, rather than the answer to it.
    /// </summary>
    [Fact]
    public async Task NarrowTheLogToTheNamedReader()
    {
        userLookupService.GetAsync("Target")
            .Returns(new GeneralUser { UserId = targetUserId, Username = "Target" });
        warningRepository
            .GetLog(targetUserId, Arg.Any<PagingQuery>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<Warning>)[new Warning { WarningId = warningId }], 1));

        var (warnings, _) = await service.GetLog("Target", new PagingQuery());

        warnings.Should().ContainSingle(w => w.WarningId == warningId);
    }

    // Two reads of one list would be two orderings and two filters to keep in agreement, and the
    // only difference the two screens have is who may call them and what a row carries.
    [Fact]
    public async Task ReadTheModerationListThroughTheSamePage()
    {
        var paging = new PagingQuery { Skip = 40, Take = 20 };
        warningRepository
            .GetLog(null, paging, Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<Warning>)[new Warning { WarningId = warningId }], 61));

        var (warnings, page) = await service.GetAllWarnings(null, paging);

        warnings.Should().ContainSingle(w => w.WarningId == warningId);
        page.TotalEntitiesCount.Should().Be(61);
        page.CurrentPage.Should().Be(3);
        page.PageSize.Should().Be(20);
    }
}
