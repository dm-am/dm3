using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Notifiers.Game;

/// <summary>
/// The shared half of the three game invitations.
/// </summary>
/// <remarks>
/// Each generator writes its notification in its own file, where the notification rules read it.
/// </remarks>
internal abstract class GameInvitationNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    protected GameInvitationNotificationGenerator(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Kind of token the invitation is stored under.
    /// </summary>
    protected abstract TokenType TokenType { get; }

    /// <summary>
    /// The invitation the event is about, or null once the token or the game has gone.
    /// </summary>
    /// <param name="tokenId">Token identifier the event carries</param>
    /// <returns>The invitation, or null</returns>
    protected async Task<Invitation?> Subject(Guid tokenId)
    {
        var tokenType = TokenType;
        var tokenData = await _dbContext.Tokens
            .Where(t => t.TokenId == tokenId && t.Type == tokenType)
            .Select(t => new
            {
                t.UserId,
                t.EntityId,
                t.CreatorId,
                GameTitle = t.Game!.Title,
                t.Game.MasterId,
                InviterUsername = t.Game.Master!.Username
            })
            .FirstOrDefaultAsync();

        return tokenData is { EntityId: not null }
            ? new Invitation(
                tokenData.UserId,
                tokenData.EntityId.Value,
                tokenData.CreatorId,
                tokenData.GameTitle,
                tokenData.MasterId,
                tokenData.InviterUsername)
            : null;
    }

    /// <summary>
    /// One invitation: who was invited, to which game, and by whom.
    /// </summary>
    /// <remarks>
    /// Falls back to the master for a token without a creator, as BlogInvitationRepository does.
    /// </remarks>
    protected sealed record Invitation(
        Guid UserId,
        Guid GameId,
        Guid? CreatorId,
        string GameTitle,
        Guid MasterId,
        string InviterUsername);
}
