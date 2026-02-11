using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.DataAccess.BusinessObjects.Users;
using DM.Services.DataAccess.RelationalStorage;

namespace DM.Services.Game.BusinessProcesses.Games.Invitations;

/// <summary>
/// Repository for invitation operations
/// </summary>
public interface IInvitationRepository
{
    /// <summary>
    /// Find all pending invitations for a game by type
    /// </summary>
    Task<IEnumerable<Guid>> FindInvitations(Guid gameId, TokenType type);

    /// <summary>
    /// Find pending invitation for a specific user in a game
    /// </summary>
    Task<IEnumerable<Guid>> FindInvitations(Guid gameId, Guid userId, TokenType type);

    /// <summary>
    /// Find game by token and user
    /// </summary>
    Task<Guid?> FindGameByToken(Guid tokenId, Guid userId, TokenType type);

    /// <summary>
    /// Invalidate existing invitations and create a new one
    /// </summary>
    Task InvalidateAndCreate(IEnumerable<IUpdateBuilder<Token>> updates, Token token);

    /// <summary>
    /// Update token
    /// </summary>
    Task Update(IUpdateBuilder<Token> update);

    /// <summary>
    /// Get invitation details
    /// </summary>
    Task<InvitationInfo?> GetInvitation(Guid tokenId);

    /// <summary>
    /// Get all pending invitations for a game
    /// </summary>
    Task<IEnumerable<InvitationInfo>> GetPendingInvitations(Guid gameId);

    /// <summary>
    /// Get all pending invitations for a user
    /// </summary>
    Task<IEnumerable<InvitationInfo>> GetUserPendingInvitations(Guid userId);
}

/// <summary>
/// Invitation information
/// </summary>
public class InvitationInfo
{
    /// <summary>
    /// Token ID
    /// </summary>
    public Guid TokenId { get; set; }

    /// <summary>
    /// Game ID
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Game title
    /// </summary>
    public string GameTitle { get; set; } = null!;

    /// <summary>
    /// Invited user ID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Invited user login
    /// </summary>
    public string UserLogin { get; set; } = null!;

    /// <summary>
    /// Inviter (master) login
    /// </summary>
    public string InviterLogin { get; set; } = null!;

    /// <summary>
    /// Invitation type
    /// </summary>
    public TokenType Type { get; set; }

    /// <summary>
    /// Creation date
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
}
