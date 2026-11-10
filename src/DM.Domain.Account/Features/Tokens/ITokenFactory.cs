using System;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Tokens;

namespace DM.Domain.Account.Features.Tokens;

/// <summary>
/// Factory for creating authentication and verification tokens
/// </summary>
public interface ITokenFactory
{
    CreateToken Create(Guid userId, TokenType type);
}
