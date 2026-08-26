using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests;

/// <summary>
/// Every entity that owns a unit of storage carries [Table]; a nested value that
/// owns none — a jsonb payload, an owned column shape — carries nothing, and
/// that absence is the second, deliberate state. Since W1.1 the one store is
/// PostgreSQL (INV-1): the Mongo collection marker does not exist any more, so
/// carrying it is a compile error rather than a rule.
/// See docs/conventions/DATA_STORAGE.md.
/// </summary>
public class EntityStorageMarkerShould
{
    /// <summary>
    /// Values embedded in a jsonb column: no storage identity of their own,
    /// hence no marker. Adding to this list is a decision, which is why it is a
    /// list and not a heuristic.
    /// </summary>
    /// <remarks>
    /// Full type names, because a short one hands the exemption to whatever
    /// future class happens to reuse it — the exemption has to name a type, not
    /// a word.
    /// </remarks>
    private static readonly HashSet<string> EmbeddedValues =
    [
        // Inside the UserSettings channel-preference columns
        "DM.Infrastructure.Persistence.Entities.Account.Settings.NotificationChannelPreference",
        // Inside the attribute schema's Specifications column
        "DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.AttributeSpecification",
        "DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.BbCodeAttributeConstraints",
        "DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.ListAttributeConstraints",
        "DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.ListAttributeValue",
        "DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.NumberAttributeConstraints",
        "DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.StringAttributeConstraints",
        // Inside the dice roll's Result column
        "DM.Infrastructure.Persistence.Entities.Game.Posts.RollResult",
    ];

    [Fact]
    public void NameTheStoreItLivesIn()
    {
        var unmarked = typeof(DmDbContext).Assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Namespace != null)
            .Where(t => t.Namespace!.Contains(".Entities"))
            .Where(t => !EmbeddedValues.Contains(t.FullName!))
            .Where(t => t.GetCustomAttribute<TableAttribute>() == null)
            .Select(t => t.FullName!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        unmarked.Should().BeEmpty(
            "an entity with its own unit of storage says which table that is");
    }

    /// <summary>
    /// The exemption has to keep naming a type that exists, or a rename turns it
    /// into a hole nobody sees.
    /// </summary>
    [Fact]
    public void KeepTheListOfEmbeddedValuesHonest()
    {
        var known = typeof(DmDbContext).Assembly
            .GetTypes()
            .Select(t => t.FullName!)
            .ToHashSet(StringComparer.Ordinal);

        EmbeddedValues.Except(known, StringComparer.Ordinal).Should().BeEmpty(
            "an exemption for a type that no longer exists guards nothing and outlives its reason");
    }
}
