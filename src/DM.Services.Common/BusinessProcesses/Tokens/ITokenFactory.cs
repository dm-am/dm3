using System;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.Common.BusinessProcesses.Tokens;

/// <summary>
/// Factory for creating authentication and verification tokens
/// </summary>
public interface ITokenFactory
{
    /// <summary>
    /// Create a new token for a user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="type">Token type</param>
    /// <returns>New token</returns>
    Token Create(Guid userId, TokenType type);

    /// <summary>
    /// Create a new token for a user with entity reference
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="entityId">Related entity identifier (game, blog, etc.)</param>
    /// <param name="type">Token type</param>
    /// <returns>New token</returns>
    Token Create(Guid userId, Guid entityId, TokenType type);
}
