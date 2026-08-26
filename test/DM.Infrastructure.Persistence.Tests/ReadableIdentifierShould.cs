using System.Collections.Generic;
using System.Linq;
using DM.Infrastructure.Persistence.Entities.Blog;
using DM.Infrastructure.Persistence.Entities.Game;
using DM.Infrastructure.Persistence.Entities.Messaging;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests;

/// <summary>
/// A readable page address is unique and indexed, for every entity that has one.
/// </summary>
/// <remarks>
/// PublicId is what every link on the site carries and what the page is resolved by, with
/// equality. Chats had the constraint and the reason written beside it; games and blogs had
/// neither, so the busiest lookup on the site answered from a sequential scan, and a repeated
/// address would have returned whichever row the plan reached first instead of failing.
///
/// Stated over the model rather than over the three known types: the next entity to get a
/// readable address inherits the rule instead of the omission.
/// </remarks>
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

    /// <summary>
    /// A reader that reads nothing passes everything.
    /// </summary>
    [Fact]
    public void FindEveryEntityThatCarriesOne()
    {
        var names = TypesWithReadableId().Select(t => t.ClrType).ToList();

        names.Should().Contain(new[] { typeof(Game), typeof(Blog), typeof(Chat) },
            "games, blogs and chats are the three entities addressed by a readable id today; a " +
            "walk that misses one of them is walking the wrong model");
    }

    private static IReadOnlyCollection<IEntityType> TypesWithReadableId()
    {
        // Model metadata only: the connection is never opened.
        var options = new DbContextOptionsBuilder<DmDbContext>()
            .UseNpgsql("Host=localhost;Database=public-id-probe;Username=probe;Password=probe")
            .Options;
        using var context = new DmDbContext(options);

        return context.GetService<IDesignTimeModel>().Model
            .GetEntityTypes()
            .Where(entityType => entityType.FindProperty(ReadableId) != null)
            .ToList();
    }
}
