using System;
using System.Linq;
using DM.Infrastructure.Persistence.Entities.Contracts;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests.RelationalStorage;

/// <summary>
/// A soft-deleted row is invisible to every query, and that holds for the model
/// rather than for a repository.
/// </summary>
/// <remarks>
/// OnModelCreating hangs the "not removed" filter in a loop over IRemovable, so
/// no repository can drop it and no repository has to remember it. Two things
/// can still break the rule and neither is visible at the call site: deleting
/// the loop, and adding an IsRemoved column to an entity that does not declare
/// the interface - a row that then reads as deleted to the writer and as present
/// to every reader.
/// </remarks>
public class SoftDeleteFilterShould
{
    /// <summary>
    /// Entities that opt out of the global filter on purpose: their write paths
    /// have to see tombstone rows. An upsert over a tombstoned unread marker
    /// deliberately revives it for a re-added participant, and a game resolves
    /// its removed attribute schema through its own reference. Each of their
    /// reads spells its own IsRemoved predicate instead — see the entity
    /// remarks. Adding here is a decision, not a convenience.
    /// </summary>
    private static readonly string[] OwnTheirPredicates =
    [
        nameof(Entities.Shared.UnreadCounter),
        "AttributeSchema",
    ];

    private static DmDbContext Context() => new(new DbContextOptionsBuilder<DmDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    [Fact]
    public void ApplyToEverySetThatDeclaresTheContract()
    {
        using var context = Context();

        var removable = context.Model.GetEntityTypes()
            .Where(entity => typeof(IRemovable).IsAssignableFrom(entity.ClrType))
            .ToArray();

        removable.Should().NotBeEmpty("the model has soft-deletable entities");
        removable.Where(entity => entity.GetDeclaredQueryFilters().Count == 0)
            .Select(entity => entity.ClrType.Name)
            .Where(name => !OwnTheirPredicates.Contains(name, StringComparer.Ordinal))
            .Should().BeEmpty("an unfiltered set answers with rows the site treats as deleted");
    }

    /// <summary>
    /// The opt-out list has to keep naming entities that exist and that really
    /// are unfiltered, or a rename leaves a hole and a re-added filter leaves a
    /// stale exemption.
    /// </summary>
    [Fact]
    public void KeepTheOptOutListHonest()
    {
        using var context = Context();

        var unfiltered = context.Model.GetEntityTypes()
            .Where(entity => typeof(IRemovable).IsAssignableFrom(entity.ClrType))
            .Where(entity => entity.GetDeclaredQueryFilters().Count == 0)
            .Select(entity => entity.ClrType.Name)
            .ToArray();

        OwnTheirPredicates.Except(unfiltered, StringComparer.Ordinal).Should().BeEmpty(
            "an exemption for an entity that is filtered after all, or gone, outlives its reason");
    }

    [Fact]
    public void BeTheOnlyWayAnEntityCarriesARemovedFlag()
    {
        var bare = typeof(DmDbContext).Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, Namespace: not null })
            .Where(type => type.Namespace!.Contains(".Entities"))
            .Where(type => type.GetProperty(nameof(IRemovable.IsRemoved)) != null)
            .Where(type => !typeof(IRemovable).IsAssignableFrom(type))
            .Select(type => type.FullName!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        bare.Should().BeEmpty(
            "the filter is hung off the interface, so a hand-rolled IsRemoved is a column " +
            "nothing filters on");
    }
}
