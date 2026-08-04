using FluentAssertions;
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
/// The warning is the entity the filter is for, and it is checked beside the ban so
/// the rule reads as a distinction rather than an exemption.
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
        Model().FindEntityType(typeof(DbBan))!.GetQueryFilter().Should().BeNull(
            "a lifted ban is the moderation history, and a filter that hides it hides the history");
    }

    [Fact]
    public void KeepNoSoftDeleteColumn()
    {
        Model().FindEntityType(typeof(DbBan))!.FindProperty("IsRemoved").Should().BeNull(
            "lifting is recorded by LiftedUtc, and a second column for one fact drifts from it");
    }

    [Fact]
    public void LeaveTheWarningSoftDeletable()
    {
        Model().FindEntityType(typeof(DbWarning))!.GetQueryFilter().Should().NotBeNull(
            "removing a warning is a removal, and that is what the global filter is for");
    }
}
