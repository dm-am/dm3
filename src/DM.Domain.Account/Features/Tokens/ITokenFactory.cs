using System;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Tokens;

namespace DM.Domain.Account.Features.Tokens;

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
    /// <returns>New token DTO</returns>
    CreateToken Create(Guid userId, TokenType type);
}
