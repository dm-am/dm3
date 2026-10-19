using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Account;
using Microsoft.EntityFrameworkCore;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Tools.Seeder.Seeding;

internal sealed partial class DataSeeder
{
    /// <summary>
    /// The invitations waiting for an answer: into the first game TestUser masters, into the first
    /// blog he writes, and one to him, so the settings of both and his account each list some.
    /// </summary>
    /// <remarks>
    /// An invitation is a token, and the token sweep deletes every token a week after it was issued by
    /// the server's clock (AccountRetentionPolicy.TokenRetention), however long the invitation itself is
    /// valid. These are issued hours before the epoch, so they live only on a stand seeded on the live
    /// clock and looked at within the week. A stand seeded on a pinned past epoch (scripts/visual/rebuild.mjs)
    /// gets them expired already (InvitationPolicy), and the first sweep deletes them.
    /// The invitees are staff and a newbie, the names a roster draws by role.
    /// </remarks>
    private async Task SeedPendingInvitations(List<DbUser> users, ComprehensiveSeedResult result)
    {
        var member = users.FirstOrDefault(u => u.Username == "TestUser");
        if (member == null || await _dbContext.Tokens.AnyAsync(t => InvitationTypes.Contains(t.Type)))
        {
            result.Details.Add("Invitations skipped: no TestUser, or invitations already exist");
            return;
        }

        var invitations = new List<Token>();
        invitations.AddRange(await GameInvitationsOf(member, users));
        invitations.AddRange(await BlogInvitationsOf(member, users));
        invitations.AddRange(await InvitationsTo(member));

        _dbContext.Tokens.AddRange(invitations);
        await _dbContext.SaveChangesAsync();
        result.Details.Add($"Invitations seeded: {invitations.Count}");
    }

    private static readonly TokenType[] InvitationTypes =
    [
        TokenType.GameAssistantInvitation, TokenType.GamePlayerInvitation, TokenType.GameReaderInvitation,
        TokenType.BlogAssistantInvitation, TokenType.BlogReaderInvitation
    ];

    /// <summary>
    /// Into the first active game of the member, by serial number: the administrator as an assistant,
    /// the mentor as a reader, a newbie as a player, each skipped when already in the game or reading it.
    /// </summary>
    private async Task<IEnumerable<Token>> GameInvitationsOf(DbUser master, List<DbUser> users)
    {
        var game = await _dbContext.Games
            .Where(g => g.MasterId == master.UserId && g.Status == ModuleStatus.Active)
            .OrderBy(g => g.SerialNumber)
            .Select(g => new { g.GameId })
            .FirstOrDefaultAsync();
        if (game == null) return [];

        var taken = await _dbContext.Characters
            .Where(c => c.GameId == game.GameId && c.AuthorId != null)
            .Select(c => c.AuthorId!.Value)
            .Concat(_dbContext.GameAssistants.Where(a => a.GameId == game.GameId).Select(a => a.UserId))
            .Concat(_dbContext.Subscriptions
                .Where(s => s.TargetType == SubscriptionTargetType.Game && s.TargetId == game.GameId)
                .Select(s => s.SubscriberId))
            .ToListAsync();

        var invitees = new (DbUser? User, TokenType Type)[]
        {
            (users.FirstOrDefault(u => u.Username == PrimaryLogin), TokenType.GameAssistantInvitation),
            (users.FirstOrDefault(u => u.Role == UserRole.Mentor), TokenType.GameReaderInvitation),
            (users.FirstOrDefault(u => u.Username == "Player Four"), TokenType.GamePlayerInvitation),
        };

        return invitees
            .Where(i => i.User != null && i.User.UserId != master.UserId && !taken.Contains(i.User.UserId))
            .Select((i, at) => Invitation(i.Type, i.User!, game.GameId, master, TimeSpan.FromHours(2 + at)));
    }

    /// <summary>
    /// Into the member's first blog, by serial number: the administrator and the mentor as readers,
    /// each skipped when already an assistant of it.
    /// </summary>
    private async Task<IEnumerable<Token>> BlogInvitationsOf(DbUser author, List<DbUser> users)
    {
        var blog = await _dbContext.Blogs
            .Where(b => b.AuthorId == author.UserId)
            .OrderBy(b => b.SerialNumber)
            .Select(b => new { b.BlogId })
            .FirstOrDefaultAsync();
        if (blog == null) return [];

        var assistants = await _dbContext.BlogAssistants
            .Where(a => a.BlogId == blog.BlogId)
            .Select(a => a.UserId)
            .ToListAsync();

        // No creator, as the blog writes them: the author is the inviter.
        return new[]
            {
                users.FirstOrDefault(u => u.Username == PrimaryLogin),
                users.FirstOrDefault(u => u.Role == UserRole.Mentor),
            }
            .Where(u => u != null && !assistants.Contains(u.UserId))
            .Select((u, at) => Invitation(
                TokenType.BlogReaderInvitation, u!, blog.BlogId, null, TimeSpan.FromHours(3 + at)));
    }

    /// <summary>
    /// To the member: an assistant's seat in the first active blog of the administrator, skipped when
    /// he holds it already.
    /// </summary>
    private async Task<IEnumerable<Token>> InvitationsTo(DbUser member)
    {
        var blog = await _dbContext.Blogs
            .Where(b => b.Author.Username == PrimaryLogin && b.Status == ModuleStatus.Active &&
                        !_dbContext.BlogAssistants.Any(a => a.BlogId == b.BlogId && a.UserId == member.UserId))
            .OrderBy(b => b.SerialNumber)
            .Select(b => new { b.BlogId })
            .FirstOrDefaultAsync();

        return blog == null
            ? []
            : [Invitation(TokenType.BlogAssistantInvitation, member, blog.BlogId, null, TimeSpan.FromHours(5))];
    }

    /// <summary>A live invitation of one person into one game or blog, issued the given time ago.</summary>
    private Token Invitation(TokenType type, DbUser invitee, Guid entityId, DbUser? creator, TimeSpan age) => new()
    {
        TokenId = _guidFactory.Create(),
        UserId = invitee.UserId,
        EntityId = entityId,
        Type = type,
        CreatorId = creator?.UserId,
        CreatedUtc = _now - age,
        IsRemoved = false
    };
}
