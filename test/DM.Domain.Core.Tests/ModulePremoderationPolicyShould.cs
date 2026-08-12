using System;
using System.Net;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Statuses;
using FluentAssertions;
using Xunit;

namespace DM.Domain.Core.Tests;

/// <summary>
/// The premoderation machine a game and a blog both run on.
/// </summary>
/// <remarks>
/// The curator is asserted on both moves: it is the field the two of them exist
/// to write, and a module whose mentor is not cleared on release stays curated
/// by somebody who no longer answers for it.
/// </remarks>
public class ModulePremoderationPolicyShould
{
    private static readonly Guid Mentor = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void TakeAModuleAwaitingEditsInAndRecordTheMentorWhoDidIt()
    {
        var change = ModulePremoderationPolicy.Resolve(
            ModulePremoderationTransition.SendToPremoderation,
            PremoderationStatus.AwaitingEdits,
            Mentor);

        change.Status.Should().Be(PremoderationStatus.AwaitingApproval);
        change.MentorId.Should().Be(Mentor);
    }

    [Theory]
    [InlineData(PremoderationStatus.Approved)]
    [InlineData(PremoderationStatus.AwaitingApproval)]
    public void RefuseToTakeInAModuleThatIsNotAwaitingEdits(PremoderationStatus current)
    {
        var act = () => ModulePremoderationPolicy.Resolve(
            ModulePremoderationTransition.SendToPremoderation, current, Mentor);

        act.Should().Throw<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest)
            .WithMessage(RefusalMessage.CannotSubmitForPremoderation(current));
    }

    [Fact]
    public void ReleaseAModuleAwaitingApprovalAndClearTheCurator()
    {
        var change = ModulePremoderationPolicy.Resolve(
            ModulePremoderationTransition.RemoveFromPremoderation,
            PremoderationStatus.AwaitingApproval,
            Mentor);

        change.Status.Should().Be(PremoderationStatus.Approved);
        change.MentorId.Should().BeNull();
    }

    [Theory]
    [InlineData(PremoderationStatus.Approved)]
    [InlineData(PremoderationStatus.AwaitingEdits)]
    public void RefuseToReleaseAModuleThatIsNotAwaitingApproval(PremoderationStatus current)
    {
        var act = () => ModulePremoderationPolicy.Resolve(
            ModulePremoderationTransition.RemoveFromPremoderation, current, Mentor);

        act.Should().Throw<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest)
            .WithMessage(RefusalMessage.CannotWithdrawFromPremoderation(current));
    }

    [Fact]
    public void RefuseAMoveOutsideTheTwoItKnows()
    {
        var act = () => ModulePremoderationPolicy.Resolve(
            (ModulePremoderationTransition)42, PremoderationStatus.AwaitingEdits, Mentor);

        act.Should().Throw<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest)
            .WithMessage(RefusalMessage.UnknownPremoderationTransition);
    }
}
