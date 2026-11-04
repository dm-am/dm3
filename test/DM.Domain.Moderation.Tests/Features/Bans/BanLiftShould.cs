using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Moderation.Features.Bans;
using DM.Domain.Moderation.Tests.Dsl;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Bans;

/// <summary>
/// The lift of a ban: who may lift which ban, and what the lift records.
/// </summary>
public class BanLiftShould : BanServiceTestBase
{
    [Fact]
    public async Task ThrowWhenModeratorTriesToLiftBan()
    {
        SetCurrentUser(UserRole.Moderator);

        var act = () => service.LiftBan(banId, "Разобрались");

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden)
            .WithMessage("Снимать бан может только старший модератор");
    }

    [Fact]
    public async Task ThrowNotFoundWhenLiftingMissingBan()
    {
        banRepository.Get(banId, Arg.Any<CancellationToken>()).Returns((Ban?)null);

        var act = () => service.LiftBan(banId, "Разобрались");

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ForbidSeniorModeratorFromLiftingPermanentBan()
    {
        banRepository.Get(banId, Arg.Any<CancellationToken>())
            .Returns(new Ban { BanId = banId, EndedUtc = now.AddYears(Ban.PermanentYears) });

        var act = () => service.LiftBan(banId, "Разобрались");

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
        await banRepository.DidNotReceive().Lift(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The list offers the lift exactly where the lift goes through.
    /// </summary>
    /// <remarks>
    /// The flag and the refusal are asked of the same ban by the same reader, and
    /// the expected answer is written out per case, so the table holds the rule
    /// itself as well as the agreement of its two readings.
    /// </remarks>
    [Theory]
    [InlineData(UserRole.SeniorModerator, "running", true)]
    [InlineData(UserRole.Admin, "running", true)]
    [InlineData(UserRole.Moderator, "running", false)]
    [InlineData(UserRole.RegularUser, "running", false)]
    [InlineData(UserRole.Guest, "running", false)]
    [InlineData(UserRole.SeniorModerator, "own", false)]
    [InlineData(UserRole.Admin, "own", false)]
    [InlineData(UserRole.SeniorModerator, "permanent", false)]
    [InlineData(UserRole.Admin, "permanent", true)]
    [InlineData(UserRole.SeniorModerator, "voluntary", true)]
    [InlineData(UserRole.SeniorModerator, "lifted", false)]
    [InlineData(UserRole.Admin, "lifted", false)]
    [InlineData(UserRole.SeniorModerator, "expired", false)]
    [InlineData(UserRole.Admin, "expired", false)]
    public async Task OfferTheLiftExactlyWhereTheLiftGoesThrough(UserRole reader, string kind, bool mayLift)
    {
        SetCurrentUser(reader);
        var ban = BanOfKind(kind);
        banRepository.Get(banId, Arg.Any<CancellationToken>()).Returns(ban);

        var flag = service.MayLift(ban);
        var lift = () => service.LiftBan(banId, "Разобрались");

        flag.Should().Be(mayLift);
        if (mayLift)
        {
            await lift.Should().NotThrowAsync();
            await banRepository.Received(1).Lift(banId, moderatorUserId, now, "Разобрались",
                Arg.Any<CancellationToken>());
        }
        else
        {
            await lift.Should().ThrowAsync<HttpException>();
            await banRepository.DidNotReceive().Lift(Arg.Any<Guid>(), Arg.Any<Guid>(),
                Arg.Any<DateTimeOffset>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        }
    }

    /// <summary>
    /// A ban that has run out is refused as a conflict, not as a missing ban: it
    /// is there, there is only nothing left of it to end.
    /// </summary>
    [Fact]
    public async Task RefuseToLiftABanThatHasRunOut()
    {
        banRepository.Get(banId, Arg.Any<CancellationToken>()).Returns(BanOfKind("expired"));

        var act = () => service.LiftBan(banId, "Разобрались");

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RefuseToLiftYourOwnBan()
    {
        // A democratic ban leaves the role and authentication intact, so without
        // this a banned senior moderator simply lifts it from himself.
        banRepository.Get(banId, Arg.Any<CancellationToken>())
            .Returns(new Ban { BanId = banId, TargetUserId = moderatorUserId, EndedUtc = now.AddDays(7) });

        var act = () => service.LiftBan(banId, "Разобрались");

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// A lift without words is refused before anything is read or written.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RefuseToLiftABanWithoutAReason(string reason)
    {
        banRepository.Get(banId, Arg.Any<CancellationToken>())
            .Returns(new Ban { BanId = banId, TargetUserId = targetUserId, EndedUtc = now.AddDays(7) });

        var act = () => service.LiftBan(banId, reason);

        var refusal = await act.Should().ThrowAsync<HttpBadRequestException>();
        refusal.Which.ValidationErrors.Should().ContainKey("reason")
            .WhoseValue.Should().Be(ValidationError.Empty);
        await banRepository.DidNotReceive().Lift(Arg.Any<Guid>(), Arg.Any<Guid>(),
            Arg.Any<DateTimeOffset>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefuseALiftReasonLongerThanAPointsExplanation()
    {
        banRepository.Get(banId, Arg.Any<CancellationToken>())
            .Returns(new Ban { BanId = banId, TargetUserId = targetUserId, EndedUtc = now.AddDays(7) });

        var act = () => service.LiftBan(banId, new string('а', 2001));

        var refusal = await act.Should().ThrowAsync<HttpBadRequestException>();
        refusal.Which.ValidationErrors["reason"].Should().Be(ValidationError.Long);
    }

    [Fact]
    public async Task RecordWhoLiftedTheBanAndWhy()
    {
        banRepository.Get(banId, Arg.Any<CancellationToken>())
            .Returns(new Ban { BanId = banId, TargetUserId = targetUserId, EndedUtc = now.AddDays(7) });

        await service.LiftBan(banId, "Разобрались");

        await banRepository.Received(1).Lift(banId, moderatorUserId, now, "Разобрались",
            Arg.Any<CancellationToken>());
    }
}
