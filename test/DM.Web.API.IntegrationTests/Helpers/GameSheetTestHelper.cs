using DbAttributeSpecification = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.AttributeSpecification;
using DbGameAttributeSchema = DM.Infrastructure.Persistence.Entities.Game.GameAttributeSchema;

namespace DM.Web.API.IntegrationTests.Helpers;

// A test inserting a game straight into the context owes the sheet the repository writes beside it.
internal static class GameSheetTestHelper
{
    public static DbGameAttributeSchema Sheet(
        Guid gameId, params DbAttributeSpecification[] specifications) => new()
        {
            GameId = gameId,
            Specifications = specifications.ToList(),
            Version = 0,
            UpdatedUtc = DateTimeOffset.UtcNow
        };

    public static IEnumerable<DbGameAttributeSchema> Sheets(IEnumerable<Guid> gameIds) =>
        gameIds.Select(gameId => Sheet(gameId));
}
