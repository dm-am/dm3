using System;
using System.Net;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Statuses;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Core.Tests;

/// <summary>
/// The premoderation machine a game and a blog both run on: the status a module
/// is born in, and the three moves that change it afterwards.
/// </summary>
/// <remarks>
/// The curator is asserted on every move, because it is the field the moves exist
/// to write and each of the three treats it differently: approving clears it,
/// returning for edits names the mentor who did it, and the author's move must not
/// touch it at all — an author recorded as the curator of their own module would
/// be reviewing themselves.
/// </remarks>
public class ModulePremoderationPolicyShould
{
    private static readonly Guid Mentor = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Author = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static GeneralUser User(bool newbie, bool underWatch) => new()
    {
        UserId = Author,
        QuantityRating = newbie ? 0 : ProbationPolicy.NewbiePostThreshold,
        IsUnderModerationWatch = underWatch
    };

    #region Initial status

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void PremoderateAModuleCreatedByANewbieOrAWatchedAuthor(bool newbie, bool underWatch)
    {
        ModulePremoderationPolicy.InitialStatus(User(newbie, underWatch))
            .Should().Be(PremoderationStatus.AwaitingEdits);
    }

    [Fact]
    public void LeaveAModuleCreatedByAnyoneElseApproved()
    {
        ModulePremoderationPolicy.InitialStatus(User(newbie: false, underWatch: false))
            .Should().Be(PremoderationStatus.Approved);
    }

    #endregion

    #region SetApproved

    [Theory]
    [InlineData(PremoderationStatus.Approved)]
    [InlineData(PremoderationStatus.AwaitingApproval)]
    [InlineData(PremoderationStatus.AwaitingEdits)]
    public void ApproveAModuleFromAnyStatusAndClearTheCurator(PremoderationStatus current)
    {
        var change = ModulePremoderationPolicy.Resolve(
            ModulePremoderationTransition.SetApproved, current, Mentor);

        change.Status.Should().Be(PremoderationStatus.Approved);
        change.SetMentorId.Should().BeTrue();
        change.MentorId.Should().BeNull();
    }

    #endregion

    #region SetAwaitingEdits

    [Theory]
    [InlineData(PremoderationStatus.Approved)]
    [InlineData(PremoderationStatus.AwaitingApproval)]
    [InlineData(PremoderationStatus.AwaitingEdits)]
    public void ReturnAModuleForEditsFromAnyStatusAndRecordTheMentorWhoDidIt(PremoderationStatus current)
    {
        var change = ModulePremoderationPolicy.Resolve(
            ModulePremoderationTransition.SetAwaitingEdits, current, Mentor);

        change.Status.Should().Be(PremoderationStatus.AwaitingEdits);
        change.SetMentorId.Should().BeTrue();
        change.MentorId.Should().Be(Mentor);
    }

    #endregion

    #region SubmitForApproval

    [Fact]
    public void SubmitAModuleAwaitingEditsWithoutTouchingTheCurator()
    {
        var change = ModulePremoderationPolicy.Resolve(
            ModulePremoderationTransition.SubmitForApproval,
            PremoderationStatus.AwaitingEdits,
            Author);

        change.Status.Should().Be(PremoderationStatus.AwaitingApproval);
        change.SetMentorId.Should().BeFalse();
        change.MentorId.Should().BeNull();
    }

    [Theory]
    [InlineData(PremoderationStatus.Approved)]
    [InlineData(PremoderationStatus.AwaitingApproval)]
    public void RefuseToSubmitAModuleThatIsNotAwaitingEdits(PremoderationStatus current)
    {
        var act = () => ModulePremoderationPolicy.Resolve(
            ModulePremoderationTransition.SubmitForApproval, current, Author);

        act.Should().Throw<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest)
            .WithMessage(RefusalMessage.CannotSubmitForApproval(current));
    }

    #endregion

    [Fact]
    public void RefuseAMoveOutsideTheThreeItKnows()
    {
        var act = () => ModulePremoderationPolicy.Resolve(
            (ModulePremoderationTransition)42, PremoderationStatus.AwaitingEdits, Mentor);

        act.Should().Throw<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest)
            .WithMessage(RefusalMessage.UnknownPremoderationTransition);
    }
}
