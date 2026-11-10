using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Characters;
using DM.Domain.Game.Features.Posts;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Game;

internal partial class PostRepository
{
    /// <summary>
    /// Batch-loads avatars of all characters on the page from the Uploads table
    /// and fills <see cref="CharacterShort.Picture"/> (3 URLs: original / medium /
    /// small). One query instead of N correlated subqueries.
    /// </summary>
    private async Task EnrichWithCharacterPictures(IEnumerable<Post> posts, CancellationToken ct = default)
    {
        var characterIds = posts
            .Where(p => p.Character != null && p.Character.Id != Guid.Empty)
            .Select(p => p.Character!.Id)
            .Distinct()
            .ToList();

        if (characterIds.Count == 0) return;

        // Newest per character rather than a dictionary keyed on the target.
        // Nothing in the schema stops a second live row from pointing at the same
        // character, and ToDictionaryAsync answered a duplicate key by throwing —
        // which turned every read of the room into a 500 for everyone in it, with
        // no way back that did not involve editing rows by hand. The upload path
        // retires the previous portrait now, so a duplicate should not arise; the
        // read no longer depends on that being true.
        var rows = await _dbContext.Uploads
            .Where(u => u.TargetCharacterId != null
                && characterIds.Contains(u.TargetCharacterId.Value)
                && u.Type == UploadType.CharacterAvatar
                && !u.IsRemoved)
            .OrderByDescending(u => u.CreatedUtc)
            .ThenByDescending(u => u.UploadId)
            .Select(u => new
            {
                CharacterId = u.TargetCharacterId!.Value,
                Picture = Shared.Users.AvatarProjections.From(u),
            })
            .ToListAsync(ct);

        var pictures = rows
            .GroupBy(x => x.CharacterId)
            .ToDictionary(g => g.Key, g => g.First().Picture);

        foreach (var post in posts)
        {
            if (post.Character != null && pictures.TryGetValue(post.Character.Id, out var picture))
            {
                post.Character.Picture = picture;
            }
        }
    }

    /// <summary>
    /// The author's standing in the game the post belongs to, for the badge the
    /// rated feed puts on a post nobody's character signed.
    /// </summary>
    /// <remarks>
    /// A post with a character is a character's post and carries no badge — the
    /// name shown is the character's. Read off the post itself now that the
    /// projection fills the master and the assistants; it used to be two more
    /// members of the hand-written row.
    /// </remarks>
    private static string? AuthorGameRoleOf(Post post)
    {
        if (post.Character != null) return null;
        if (AnonymousIdentity.Is(post.AuthorUserId)) return null;
        if (post.AuthorUserId == post.GameMasterUserId) return "DungeonMaster";
        return post.GameAssistantUserIds.Contains(post.AuthorUserId) ? "Assistant" : null;
    }
}
