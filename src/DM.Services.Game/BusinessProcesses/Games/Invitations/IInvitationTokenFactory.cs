using System;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.Game.BusinessProcesses.Games.Invitations;

/// <summary>
/// Factory for creating invitation tokens
/// </summary>
public interface IInvitationTokenFactory
{
    /// <summary>
    /// Create a new invitation token
    /// </summary>
    /// <param name="userId">User being invited</param>
    /// <param name="gameId">Game to invite to</param>
    /// <param name="type">Type of invitation</param>
    /// <returns>New token</returns>
    Token Create(Guid userId, Guid gameId, TokenType type);
}
