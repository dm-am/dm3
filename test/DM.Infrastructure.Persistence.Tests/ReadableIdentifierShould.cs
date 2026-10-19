using System.Collections.Generic;
using System.Linq;
using DM.Testing;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests;

/// <summary>
/// A readable page address is unique and indexed, for every entity that has one.
/// </summary>
/// <remarks>Every link resolves a page by PublicId with equality; stated over the model, so a new entity inherits it.</remarks>
public class ReadableIdentifierShould
{
    private const string ReadableId = "PublicId";

    [Fact]
    public void BeUniqueAndIndexedWhereverItExists()
    {
        foreach (var entityType in TypesWithReadableId())
        {
            var index = entityType.GetIndexes().SingleOrDefault(i =>
                i.Properties.Count == 1 && i.Properties[0].Name == ReadableId);

            index.Should().NotBeNull(
                "{0} is addressed by {1} and the lookup is an equality on it; without an index " +
                "every page open scans the table", entityType.ShortName(), ReadableId);
            index!.IsUnique.Should().BeTrue(
                "two rows of {0} sharing a {1} would not fail - the page would silently open " +
                "whichever of them the plan reached first", entityType.ShortName(), ReadableId);
        }
    }

    private static IReadOnlyCollection<IEntityType> TypesWithReadableId() => ProbeContext.DesignTimeModel()
        .GetEntityTypes()
        .Where(entityType => entityType.FindProperty(ReadableId) != null)
        .ToList();
}
