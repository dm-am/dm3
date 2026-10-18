using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;
using DbBan = DM.Infrastructure.Persistence.Entities.Moderation.Ban;
using DbWarning = DM.Infrastructure.Persistence.Entities.Moderation.Warning;

namespace DM.Infrastructure.Persistence.Tests.RelationalStorage;

/// <summary>
/// A ban row is never hidden by the soft-delete filter.
/// </summary>
/// <remarks>
/// Lifting a ban used to soft-delete it, and Bans inherited the global !IsRemoved
/// filter through the marker interface: the ban left the user's history and the
/// moderation history in the same instant, the columns recording who lifted it and
/// why could not be read back by any query, and IsLifted - derived from that same
/// flag - was false on every row a query could still return. The lift is now
/// LiftedUtc and is spelled out at each call site that needs it, including the
/// identity projection, where excluding a lifted ban is an authorization rule and
/// not tidying.
///
/// The warning is checked beside the ban because it went the same way and for the
/// same reason: it used to be soft-deleted when a moderator undid it, which took
/// the record out of the open log, out of the moderation list and out of the user's
/// history at once. A warning is never withdrawn now - only its points move, and
/// who moved them is written beside them.
/// </remarks>
public class BanVisibilityShould
{
    private static IModel Model()
    {
        // Model metadata only: the connection is never opened.
        var options = new DbContextOptionsBuilder<DmDbContext>()
            .UseNpgsql("Host=localhost;Database=ban-visibility-probe;Username=probe;Password=probe")
            .Options;
        using var context = new DmDbContext(options);
        return context.GetService<IDesignTimeModel>().Model;
    }

    [Fact]
    public void CarryNoQueryFilterOfItsOwn()
    {
        Model().FindEntityType(typeof(DbBan))!.GetDeclaredQueryFilters().Should().BeEmpty(
            "a lifted ban is the moderation history, and a filter that hides it hides the history");
    }

    [Fact]
    public void KeepNoSoftDeleteColumn()
    {
        Model().FindEntityType(typeof(DbBan))!.FindProperty("IsRemoved").Should().BeNull(
            "lifting is recorded by LiftedUtc, and a second column for one fact drifts from it");
    }

    [Fact]
    public void LeaveTheWarningUnfilteredToo()
    {
        Model().FindEntityType(typeof(DbWarning))!.GetDeclaredQueryFilters().Should().BeEmpty(
            "a warning is the record of a decision and stays in the log; a filter over it " +
            "hides the log the rules page links to");
    }

    [Fact]
    public void KeepNoSoftDeleteColumnOnTheWarningEither()
    {
        Model().FindEntityType(typeof(DbWarning))!.FindProperty("IsRemoved").Should().BeNull(
            "the points are what changes, and a removal flag beside them is a second way " +
            "to undo a warning that no screen offers");
    }
}
