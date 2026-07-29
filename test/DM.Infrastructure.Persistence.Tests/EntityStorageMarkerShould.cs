using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using DM.Infrastructure.Persistence.MongoIntegration;
using FluentAssertions;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests;

/// <summary>
/// Postgres tables and Mongo documents are the same kind of C# class in the same
/// folder, so which store an entity lives in is invisible at the declaration
/// unless it says so. Every entity that owns a unit of storage carries either
/// [Table] or [MongoCollectionName]; a nested value that owns none carries
/// neither, and that absence is the third, deliberate state.
/// See docs/conventions/DATA_STORAGE.md.
/// </summary>
public class EntityStorageMarkerShould
{
    /// <summary>
    /// Values embedded in a document or an owned column: no storage identity of
    /// their own, hence no marker. Adding to this list is a decision, which is
    /// why it is a list and not a heuristic.
    /// </summary>
    private static readonly HashSet<string> EmbeddedValues =
    [
        // Inside the Mongo user-settings document
        "NotificationChannelPreference",
        "PagingSettings",
        // Inside the attribute schema
        "AttributeSpecification",
        "BbCodeAttributeConstraints",
        "ListAttributeConstraints",
        "ListAttributeValue",
        "NumberAttributeConstraints",
        "StringAttributeConstraints",
        // Inside the UserSessions document
        "Session",
        // Inside the Polls document
        "PollOption",
        // Inside the Dice document
        "RollResult",
    ];

    [Fact]
    public void NameTheStoreItLivesIn()
    {
        var unmarked = typeof(DmDbContext).Assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Namespace != null)
            .Where(t => t.Namespace!.Contains(".Entities"))
            .Where(t => !EmbeddedValues.Contains(t.Name))
            .Where(t => t.GetCustomAttribute<TableAttribute>() == null &&
                        t.GetCustomAttribute<MongoCollectionNameAttribute>() == null)
            .Select(t => t.FullName!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        unmarked.Should().BeEmpty(
            "an entity with its own unit of storage says which store that is");
    }
}
