using DM.Testing;
using AwesomeAssertions;
using Xunit;
using DbBan = DM.Infrastructure.Persistence.Entities.Moderation.Ban;
using DbWarning = DM.Infrastructure.Persistence.Entities.Moderation.Warning;

namespace DM.Infrastructure.Persistence.Tests.RelationalStorage;

/// <summary>
/// A ban row is never hidden by the soft-delete filter.
/// </summary>
/// <remarks>
/// A lift is LiftedUtc, spelled at each call site that needs it; the identity projection excluding a
/// lifted ban is an authorization rule. A warning is never withdrawn either: only its points move.
/// </remarks>
public class BanVisibilityShould
{

    [Fact]
    public void CarryNoQueryFilterOfItsOwn()
    {
        ProbeContext.DesignTimeModel().FindEntityType(typeof(DbBan))!.GetDeclaredQueryFilters().Should().BeEmpty(
            "a lifted ban is the moderation history, and a filter that hides it hides the history");
    }

    [Fact]
    public void KeepNoSoftDeleteColumn()
    {
        ProbeContext.DesignTimeModel().FindEntityType(typeof(DbBan))!.FindProperty("IsRemoved").Should().BeNull(
            "lifting is recorded by LiftedUtc, and a second column for one fact drifts from it");
    }

    [Fact]
    public void LeaveTheWarningUnfilteredToo()
    {
        ProbeContext.DesignTimeModel().FindEntityType(typeof(DbWarning))!.GetDeclaredQueryFilters().Should().BeEmpty(
            "a warning is the record of a decision and stays in the log; a filter over it " +
            "hides the log the rules page links to");
    }

    [Fact]
    public void KeepNoSoftDeleteColumnOnTheWarningEither()
    {
        ProbeContext.DesignTimeModel().FindEntityType(typeof(DbWarning))!.FindProperty("IsRemoved").Should().BeNull(
            "the points are what changes, and a removal flag beside them is a second way " +
            "to undo a warning that no screen offers");
    }
}
