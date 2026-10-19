using DbAttributeSpecification = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.AttributeSpecification;
using DbGameAttributeSchema = DM.Infrastructure.Persistence.Entities.Game.GameAttributeSchema;

namespace DM.Web.API.IntegrationTests.Helpers;

/// <summary>
/// The character sheet every writer of a game row owes it.
/// </summary>
/// <remarks>A test inserting a game straight into the context owes the sheet the repository writes beside it.</remarks>
internal static class GameSheetTestHelper
{
    /// <summary>
    /// The sheet of a fresh game: the specifications given, at the version a
    /// creation leaves behind.
    /// </summary>
    public static DbGameAttributeSchema Sheet(
        Guid gameId, params DbAttributeSpecification[] specifications) => new()
        {
            GameId = gameId,
            Specifications = specifications.ToList(),
            Version = 0,
            UpdatedUtc = DateTimeOffset.UtcNow
        };

    /// <summary>
    /// An empty sheet for each of several games.
    /// </summary>
    public static IEnumerable<DbGameAttributeSchema> Sheets(IEnumerable<Guid> gameIds) =>
        gameIds.Select(gameId => Sheet(gameId));
}
