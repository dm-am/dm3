using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Testing.Dsl;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Account.Tests.Features.TwoFactor;

/// <summary>
/// Which ranks owe a second factor, and what happens to one that does not have it.
/// </summary>
/// <remarks>
/// INV-9: the factor never refuses a login. What it withholds from an account
/// that owes it is the rank, and the only person able to give the rank back is
/// the owner, from an ordinary session.
///
/// INV-10: the answer is computed in one place and folded into the role every
/// resolver reads, so no surface has to remember it. INV-11: the recorded role
/// survives the fold, because a moderator written into a journal as an ordinary
/// user is an entry nothing can be reconstructed from.
/// </remarks>
public class SecondFactorRequirementShould
{
    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.SeniorModerator)]
    public void CoverTheTwoRanksThePlanNames(UserRole role) =>
        TwoFactorRequirement.AppliesTo(role).Should().BeTrue();

    [Theory]
    [InlineData(UserRole.Guest)]
    [InlineData(UserRole.RegularUser)]
    [InlineData(UserRole.Mentor)]
    [InlineData(UserRole.Moderator)]
    // The actor never signs in, so a requirement on it would guard nothing and
    // would demote the author of every automatic action.
    [InlineData(UserRole.System)]
    public void CoverNoOtherRank(UserRole role) =>
        TwoFactorRequirement.AppliesTo(role).Should().BeFalse();

    /// <summary>
    /// AC-19: the rank goes, and it goes all the way down to an ordinary user.
    /// </summary>
    /// <remarks>
    /// Not down to a moderator: leaving a compromised administrator the edit and
    /// delete of other people's content leaves most of the damage the factor is
    /// bought against.
    /// </remarks>
    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.SeniorModerator)]
    public void WithholdTheRankOfAnAccountWithoutAFactor(UserRole role)
    {
        var user = Create.User().WithRole(role).Please();

        user.ApplySecondFactorRequirement(secondFactorConfirmed: false);

        user.Role.Should().Be(UserRole.RegularUser);
        user.PrivilegeWithheld.Should().BeTrue();
    }

    /// <summary>AC-21: the recorded role is what display and the journal read.</summary>
    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.SeniorModerator)]
    public void KeepTheRecordedRoleThroughTheWithholding(UserRole role)
    {
        var user = Create.User().WithRole(role).Please();

        user.ApplySecondFactorRequirement(secondFactorConfirmed: false);

        user.RecordedRole.Should().Be(role);
    }

    /// <summary>AC-20: the rank comes back with the factor, with no new sign-in.</summary>
    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.SeniorModerator)]
    public void LeaveTheRankAloneOnceTheFactorIsThere(UserRole role)
    {
        var user = Create.User().WithRole(role).Please();

        user.ApplySecondFactorRequirement(secondFactorConfirmed: true);

        user.Role.Should().Be(role);
        user.RecordedRole.Should().Be(role);
        user.PrivilegeWithheld.Should().BeFalse();
    }

    [Theory]
    [InlineData(UserRole.RegularUser)]
    [InlineData(UserRole.Mentor)]
    [InlineData(UserRole.Moderator)]
    public void LeaveARankThatOwesNothingAlone(UserRole role)
    {
        var user = Create.User().WithRole(role).Please();

        user.ApplySecondFactorRequirement(secondFactorConfirmed: false);

        user.Role.Should().Be(role);
        user.PrivilegeWithheld.Should().BeFalse();
    }

    /// <summary>
    /// A user built by a path that never folds still answers honestly.
    /// </summary>
    [Fact]
    public void AnswerWithTheRoleItselfBeforeAnythingIsFolded()
    {
        var user = Create.User().WithRole(UserRole.Admin).Please();

        user.RecordedRole.Should().Be(UserRole.Admin);
        user.PrivilegeWithheld.Should().BeFalse();
    }
}
