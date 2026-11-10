using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Moderation.Features.Warnings;
using DM.Domain.Moderation.Tests.Dsl;
using AwesomeAssertions;
using FluentValidation;
using NSubstitute;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Warnings;

public class WarningPointsChangeShould : WarningServiceTestBase
{
    private Task<Warning> ChangeTo(int? points, string reason) =>
        service.ChangeWarningPoints(warningId, new ChangeWarningPoints { Points = points, Reason = reason });

    // A correction is open to the senior ranks and to the issuer, who is often best placed to say
    // the points were wrong.
    [Fact]
    public async Task LetTheAuthorOfTheWarningChangeItsPoints()
    {
        warningRepository.Get(warningId, Arg.Any<CancellationToken>()).Returns(Issued());
        warningRepository.ChangePoints(warningId, 0, moderatorUserId, now, "Ошибка модератора",
            Arg.Any<CancellationToken>()).Returns(true);

        await ChangeTo(0, "Ошибка модератора");

        await warningRepository.Received(1).ChangePoints(
            warningId, 0, moderatorUserId, now, "Ошибка модератора", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LetASeniorModeratorChangeThePointsOfSomebodyElsesWarning()
    {
        var seniorId = Guid.NewGuid();
        Authenticate(seniorId, UserRole.SeniorModerator, "Senior");
        warningRepository.Get(warningId, Arg.Any<CancellationToken>()).Returns(Issued());
        warningRepository.ChangePoints(warningId, 1, seniorId, now, "Нарушение легче",
            Arg.Any<CancellationToken>()).Returns(true);

        await ChangeTo(1, "Нарушение легче");

        await warningRepository.Received(1).ChangePoints(
            warningId, 1, seniorId, now, "Нарушение легче", Arg.Any<CancellationToken>());
    }

    // The refusal is the general one rather than one naming a rank: the rule is a rank or an
    // authorship, and no rank is the whole of it.
    [Fact]
    public async Task RefuseAModeratorThePointsOfSomebodyElsesWarning()
    {
        Authenticate(Guid.NewGuid(), UserRole.Moderator, "OtherModerator");
        warningRepository.Get(warningId, Arg.Any<CancellationToken>()).Returns(Issued());

        var act = () => ChangeTo(0, "Считаю решение неверным");

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
        await warningRepository.DidNotReceive().ChangePoints(
            Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnswerWithNotFoundWhenTheWarningIsNotThere()
    {
        warningRepository.Get(warningId, Arg.Any<CancellationToken>()).Returns((Warning?)null);

        var act = () => ChangeTo(0, "Ошибка модератора");

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
        await warningRepository.DidNotReceive().ChangePoints(
            Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    public async Task RejectChangedPointsOutsideZeroToSixRange(int points)
    {
        var act = () => ChangeTo(points, "Ошибка модератора");

        await act.Should().ThrowAsync<ValidationException>();
    }

    /// <summary>
    /// The explanation is required: the log shows the correction beside the
    /// original reason, and a correction with nothing written under it leaves a
    /// verdict the points no longer agree with.
    /// </summary>
    [Fact]
    public async Task RejectAChangeOfPointsWithNoExplanation()
    {
        var act = () => ChangeTo(0, "  ");

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task RejectAnExplanationLongerThanTwoThousandCharacters()
    {
        var act = () => ChangeTo(0, new string('a', 2001));

        await act.Should().ThrowAsync<ValidationException>();
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
        warningRepository.Get(warningId, Arg.Any<CancellationToken>()).Returns(Issued());
        warningRepository.ChangePoints(warningId, 0, moderatorUserId, now, "Ошибка модератора",
            Arg.Any<CancellationToken>()).Returns(true);

        await ChangeTo(0, "Ошибка модератора");

        await eventProducer.Received(1).SendAsync(EventType.WarningPointsChanged, warningId);
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
        warningRepository.Get(warningId, Arg.Any<CancellationToken>()).Returns(Issued());

        var act = () => ChangeTo(0, "Считаю решение неверным");

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
        await eventProducer.DidNotReceive().SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>());
    }

    [Fact]
    public async Task AnnounceNothingWhenTheWarningToChangeIsNotThere()
    {
        warningRepository.Get(warningId, Arg.Any<CancellationToken>()).Returns((Warning?)null);

        var act = () => ChangeTo(0, "Ошибка модератора");

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
        await eventProducer.DidNotReceive().SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>());
    }

    [Fact]
    public async Task AnnounceNothingWhenTheChangeIsOutsideTheRules()
    {
        warningRepository.Get(warningId, Arg.Any<CancellationToken>()).Returns(Issued());

        var act = () => ChangeTo(7, "Слишком много баллов");

        await act.Should().ThrowAsync<ValidationException>();
        await eventProducer.DidNotReceive().SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>());
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
        warningRepository
            .GetLog(null, Arg.Any<PagingQuery>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<Warning>)
                [Issued(), Issued(authorId: Guid.NewGuid(), id: otherWarningId)], 2));

        var (warnings, _) = await service.GetAllWarnings(null, new PagingQuery());

        warnings.Single(w => w.WarningId == warningId).CanChangePoints.Should().BeTrue();
        warnings.Single(w => w.WarningId == otherWarningId).CanChangePoints.Should().BeFalse();
    }
}
