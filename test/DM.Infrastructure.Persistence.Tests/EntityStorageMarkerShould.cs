using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests;

/// <summary>
/// Every entity that owns a unit of storage carries [Table]; a nested value that owns none, a jsonb payload
/// or an owned column shape, carries nothing. See docs/conventions/DATA_STORAGE.md.
/// </summary>
public class EntityStorageMarkerShould
{
    /// <summary>
    /// Values embedded in a jsonb column, by full type name: adding here is a decision.
    /// </summary>
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
