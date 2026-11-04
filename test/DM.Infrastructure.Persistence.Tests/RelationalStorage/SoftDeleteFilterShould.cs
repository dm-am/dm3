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
/// OnModelCreating applies the filter in a loop over IRemovable; an IsRemoved column on an entity without
/// the interface would read as present to every reader.
/// </remarks>
public class SoftDeleteFilterShould
{
    /// <summary>
    /// Entities that opt out of the global filter: the unread-marker upsert revives tombstones, so it spells
    /// its own IsRemoved predicate in every read.
    /// </summary>
    private static readonly string[] OwnTheirPredicates =
    [
        nameof(Entities.Shared.UnreadCounter),
    ];

    [Fact]
    public void ApplyToEverySetThatDeclaresTheContract()
    {
        using var context = InMemoryStore.Open();

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
    /// The opt-out list keeps naming entities that exist and really are unfiltered.
    /// </summary>
    [Fact]
    public void KeepTheOptOutListHonest()
    {
        using var context = InMemoryStore.Open();

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
