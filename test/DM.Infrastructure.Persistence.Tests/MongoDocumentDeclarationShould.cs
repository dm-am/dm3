using System;
using System.Linq;
using System.Reflection;
using DM.Infrastructure.Persistence.MongoIntegration;
using FluentAssertions;
using MongoDB.Bson.Serialization;
using Xunit;
using DbDiceRoll = DM.Infrastructure.Persistence.Entities.Game.Posts.DiceRoll;

namespace DM.Infrastructure.Persistence.Tests;

/// <summary>
/// A Mongo document is declared as an ordinary C# class in the same folder as the
/// Postgres tables, so nothing at the declaration stops a relational habit from
/// creeping in. Both habits below were present and both were silent: an annotation
/// the driver never reads, and a property named after the stored element instead
/// of mapped to it.
/// </summary>
public class MongoDocumentDeclarationShould
{
    /// <summary>
    /// The driver reads nothing from System.ComponentModel.DataAnnotations, so an
    /// annotation on a document states a rule that nothing enforces. [Key] on
    /// UserSettings.UserId claimed the property was the document's _id; it never
    /// was, and the one-document-per-user rule it implied comes from a unique
    /// index asserted at startup instead.
    /// </summary>
    [Fact]
    public void CarryNoRelationalAnnotations()
    {
        var offenders = typeof(DmDbContext).Assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => t.GetCustomAttribute<MongoCollectionNameAttribute>() != null)
            .SelectMany(t => t.GetProperties()
                .SelectMany(p => p.GetCustomAttributes()
                    .Select(a => (Target: $"{t.FullName}.{p.Name}", Attribute: a.GetType())))
                .Concat(t.GetCustomAttributes()
                    .Select(a => (Target: t.FullName!, Attribute: a.GetType()))))
            .Where(x => x.Attribute.Namespace != null &&
                        x.Attribute.Namespace.StartsWith(
                            "System.ComponentModel.DataAnnotations", StringComparison.Ordinal))
            .Select(x => $"{x.Target}: [{x.Attribute.Name}]")
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        offenders.Should().BeEmpty(
            "the Mongo driver reads none of these, so the rule they state holds nowhere");
    }

    /// <summary>
    /// The stored element is lowercase, the property is not, and the mapping is the
    /// only thing holding the two together. Drop it and every existing document
    /// reads back with an empty comment, with no error raised anywhere.
    /// </summary>
    [Fact]
    public void MapDiceCommentToItsStoredElementName()
    {
        var classMap = BsonClassMap.LookupClassMap(typeof(DbDiceRoll));

        classMap.GetMemberMap(nameof(DbDiceRoll.Comment)).ElementName
            .Should().Be("comment");
    }
}
