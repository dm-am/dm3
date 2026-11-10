using DM.Domain.Core.Enums;
using DbCharacter = DM.Infrastructure.Persistence.Entities.Game.Characters.Character;

namespace DM.Web.API.IntegrationTests.Helpers;

internal static class CharacterRows
{
    // An active player character, made two days ago, that lets nobody else edit its posts.
    public static DbCharacter InPlay(Guid id, Guid gameId, Guid authorId, string name) => new()
    {
        CharacterId = id,
        GameId = gameId,
        AuthorId = authorId,
        Name = name,
        Status = CharacterStatus.Active,
        IsNpc = false,
        AccessPolicy = CharacterAccessPolicy.NoAccess,
        CreatedUtc = DateTimeOffset.UtcNow.AddDays(-2),
        IsRemoved = false,
    };
}
