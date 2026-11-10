using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Moderation.Features.Bans;
using DM.Domain.Moderation.Tests.Dsl;
using AwesomeAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Bans;

public class BanHistoryShould : BanServiceTestBase
{
    [Fact]
    public async Task ReturnEmptyListWhenGettingBansForNonexistentUser()
    {
        userLookupService.GetAsync("Unknown")
            .ThrowsAsync(new HttpException(HttpStatusCode.NotFound, "Пользователь не найден"));

        var result = await service.GetUserBans("Unknown");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task LetAStorageFailureThroughInsteadOfReportingACleanRecord()
    {
        // The catch in GetUserBans answers one question - does this user exist
        // - and an empty list is the answer to it. A dropped connection is not
        // that answer: it used to reach the moderator's page as "no bans",
        // which is exactly what an unblemished profile looks like.
        userLookupService.GetAsync("Target").Returns(new GeneralUser { UserId = targetUserId, Username = "Target" });
        banRepository.GetUserBans(targetUserId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("connection reset"));

        var act = () => service.GetUserBans("Target");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task LetAStorageFailureThroughWhenReadingTheActiveBan()
    {
        // Same rule on the public route: "we could not check" must not leave
        // the service as "not banned".
        userLookupService.GetAsync("Target").Returns(new GeneralUser { UserId = targetUserId, Username = "Target" });
        banRepository.GetActiveBan(targetUserId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("connection reset"));

        var act = () => service.GetActiveBan("Target");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ThrowWhenNonModeratorTriesToViewAllBans()
    {
        SetCurrentUser(UserRole.RegularUser);

        var act = () => service.GetAllActiveBans();

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden)
            .WithMessage("Список банов доступен модераторам");
    }

    [Fact]
    public async Task AllowModeratorToViewAllActiveBans()
    {
        SetCurrentUser(UserRole.Moderator);
        banRepository.GetAllActiveBans(Arg.Any<CancellationToken>()).Returns([BanOfKind("running")]);

        var result = await service.GetAllActiveBans();

        result.Should().ContainSingle().Which.CanLift.Should().BeFalse("lifting is the senior moderator's");
    }

    // The ban log is public, as the warning log is: a guest reads it, and is offered no lift.
    [Theory]
    [InlineData(UserRole.Guest)]
    [InlineData(UserRole.RegularUser)]
    public async Task LetAnyoneReadTheBanHistory(UserRole role)
    {
        SetCurrentUser(role);
        IReadOnlyList<BanLogEntry> page =
            [new() { Kind = BanLogEntryKind.Issued, OccurredUtc = DateTimeOffset.UnixEpoch, Ban = BanOfKind("running") }];
        banRepository.GetBanHistory(null, 0, 20, Arg.Any<CancellationToken>()).Returns((page, 1));

        var (entries, totalCount, _) = await service.GetBanHistory(null, 0, 20);

        totalCount.Should().Be(1);
        entries.Should().ContainSingle().Which.Ban.CanLift.Should().BeFalse();
    }

    [Fact]
    public async Task AllowModeratorToViewBanHistory()
    {
        SetCurrentUser(UserRole.Moderator);
        banRepository.GetBanHistory(null, 0, 20, Arg.Any<CancellationToken>()).Returns(([], 0));

        var (entries, totalCount, _) = await service.GetBanHistory(null, 0, 20);

        entries.Should().BeEmpty();
        totalCount.Should().Be(0);
        await banRepository.Received(1).GetBanHistory(null, 0, 20, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NarrowTheBanHistoryToTheNamedPerson()
    {
        SetCurrentUser(UserRole.Moderator);
        userLookupService.GetAsync("Target").Returns(new GeneralUser { UserId = targetUserId, Username = "Target" });
        banRepository.GetBanHistory(targetUserId, 0, 20, Arg.Any<CancellationToken>()).Returns(([], 0));

        await service.GetBanHistory("Target", 0, 20);

        await banRepository.Received(1).GetBanHistory(targetUserId, 0, 20, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A name nobody carries is an empty page, as the warning log answers it: the
    /// list is not read at all, and certainly not read whole.
    /// </summary>
    [Fact]
    public async Task AnswerANameNobodyCarriesWithAnEmptyBanHistory()
    {
        SetCurrentUser(UserRole.Moderator);
        userLookupService.GetAsync("Unknown")
            .ThrowsAsync(new HttpException(HttpStatusCode.NotFound, "Пользователь не найден"));

        var (entries, totalCount, _) = await service.GetBanHistory("Unknown", 0, 20);

        entries.Should().BeEmpty();
        totalCount.Should().Be(0);
        await banRepository.DidNotReceive().GetBanHistory(
            Arg.Any<Guid?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkEachBanOfTheHistoryWithWhetherTheReaderMayLiftIt()
    {
        var others = BanOfKind("running");
        var own = BanOfKind("own");
        IReadOnlyList<BanLogEntry> page =
        [
            new() { Kind = BanLogEntryKind.Issued, OccurredUtc = others.StartedUtc, Ban = others },
            new() { Kind = BanLogEntryKind.Issued, OccurredUtc = own.StartedUtc, Ban = own }
        ];
        banRepository.GetBanHistory(null, 0, 20, Arg.Any<CancellationToken>()).Returns((page, 2));

        var (entries, _, _) = await service.GetBanHistory(null, 0, 20);

        entries.Select(e => e.Ban.CanLift).Should().Equal(true, false);
    }

    [Fact]
    public async Task OpenThePageThatHoldsTheEntryOfTheGivenNumber()
    {
        SetCurrentUser(UserRole.Moderator);
        userLookupService.GetAsync("Target").Returns(new GeneralUser { UserId = targetUserId, Username = "Target" });
        banRepository.GetBanHistoryPosition(targetUserId, 2077, Arg.Any<CancellationToken>()).Returns(45);
        banRepository.GetBanHistory(targetUserId, 40, 20, Arg.Any<CancellationToken>()).Returns(([], 50));

        var (_, totalCount, skip) = await service.GetBanHistory("Target", 0, 20, 2077);

        skip.Should().Be(40, "the 46th entry of the list stands on its third page of twenty");
        totalCount.Should().Be(50);
        await banRepository.Received(1).GetBanHistory(targetUserId, 40, 20, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task KeepThePageAskedForWhenTheListHasNoSuchEntry()
    {
        SetCurrentUser(UserRole.Moderator);
        banRepository.GetBanHistoryPosition(null, 9999, Arg.Any<CancellationToken>()).Returns((int?)null);
        banRepository.GetBanHistory(null, 20, 20, Arg.Any<CancellationToken>()).Returns(([], 30));

        var (_, _, skip) = await service.GetBanHistory(null, 20, 20, 9999);

        skip.Should().Be(20);
        await banRepository.Received(1).GetBanHistory(null, 20, 20, Arg.Any<CancellationToken>());
    }
}
