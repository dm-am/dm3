using System;
using System.Linq;
using DM.Infrastructure.Persistence.Entities.Contracts;
using FluentAssertions;
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
        removable.Where(entity => entity.GetQueryFilter() == null)
            .Select(entity => entity.ClrType.Name)
            .Should().BeEmpty("an unfiltered set answers with rows the site treats as deleted");
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
