using System;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Tokens;

namespace DM.Domain.Account.Features.Tokens;

/// <inheritdoc />
internal class TokenFactory : ITokenFactory
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
    public CreateToken Create(Guid userId, TokenType type)
    {
        return new CreateToken
        {
            TokenId = _guidFactory.Create(),
            UserId = userId,
            Type = type,
            CreatedUtc = _dateTimeProvider.Now
        };
    }

    /// <inheritdoc />
    public CreateToken Create(Guid userId, Guid entityId, TokenType type)
    {
        return new CreateToken
        {
            TokenId = _guidFactory.Create(),
            UserId = userId,
            EntityId = entityId,
            Type = type,
            CreatedUtc = _dateTimeProvider.Now
        };
    }
}
