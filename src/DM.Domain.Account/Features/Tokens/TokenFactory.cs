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
        // Two values, not one. TokenId names the row; Secret is what the letter
        // carries, and the row keeps only its hash — see ConfirmationSecret for
        // why a confirmation link is stored the way a password is.
        var tokenId = _guidFactory.Create();
        var secret = _guidFactory.Create();
        return new CreateToken
        {
            TokenId = tokenId,
            Secret = secret,
            SecretHash = ConfirmationSecret.Hash(secret),
            UserId = userId,
            Type = type,
            CreatedUtc = _dateTimeProvider.Now
        };
    }
}
