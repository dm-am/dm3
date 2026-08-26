using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Account;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Notifiers.Game;

/// <summary>
/// The shared half of the three game invitations. Each answers its own event
/// about its own kind of token and tells the invited person; the token is read
/// the same way for all three, and the notification each hands back is written
/// where the notification rules can read it — in the generator's own file.
/// </summary>
internal abstract class GameInvitationNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    protected GameInvitationNotificationGenerator(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Kind of token the invitation is stored under.
    /// </summary>
    protected abstract TokenType TokenType { get; }

    /// <summary>
    /// The invitation the event is about, or null when there is nothing to tell
    /// anybody about: the events carry no idempotency key and are redelivered
    /// after the token or the game has gone.
    /// </summary>
    /// <param name="tokenId">Token identifier the event carries</param>
    /// <returns>The invitation, or null</returns>
    protected async Task<Invitation?> Subject(Guid tokenId)
    {
        var tokenType = TokenType;
        var data = await _dbContext.Tokens
            .Where(t => t.TokenId == tokenId && t.Type == tokenType)
            .Select(t => new
            {
                t.UserId,
                t.EntityId,
                t.CreatorId,
                GameTitle = t.Game!.Title,
                MasterId = t.Game.MasterId,
                InviterUsername = t.Game.Master!.Username
            })
            .FirstOrDefaultAsync();

        return data is { EntityId: not null }
            ? new Invitation(
                data.UserId,
                data.EntityId.Value,
                data.CreatorId,
                data.GameTitle,
                data.MasterId,
                data.InviterUsername)
            : null;
    }

    /// <summary>
    /// One invitation: who was invited, to which game, and by whom.
    /// </summary>
    /// <remarks>
    /// The inviter is the token's creator, master or assistant. Falling back to
    /// the master keeps the filter on the person InviterUsername names when a
    /// token carries no creator, the way BlogInvitationRepository resolves its
    /// inviter.
    /// </remarks>
    protected sealed record Invitation(
        Guid UserId,
        Guid GameId,
        Guid? CreatorId,
        string GameTitle,
        Guid MasterId,
        string InviterUsername);
}
