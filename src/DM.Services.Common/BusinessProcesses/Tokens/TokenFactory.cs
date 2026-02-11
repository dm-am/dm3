using System;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.Common.BusinessProcesses.Tokens;

/// <inheritdoc />
public class TokenFactory : ITokenFactory
{
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public TokenFactory(
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public Token Create(Guid userId, TokenType type)
    {
        return new Token
        {
            TokenId = _guidFactory.Create(),
            UserId = userId,
            Type = type,
            CreatedUtc = _dateTimeProvider.Now,
            IsRemoved = false
        };
    }

    /// <inheritdoc />
    public Token Create(Guid userId, Guid entityId, TokenType type)
    {
        return new Token
        {
            TokenId = _guidFactory.Create(),
            UserId = userId,
            EntityId = entityId,
            Type = type,
            CreatedUtc = _dateTimeProvider.Now,
            IsRemoved = false
        };
    }
}
