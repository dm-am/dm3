using System;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.Gaming.BusinessProcesses.Games.Invitations;

/// <inheritdoc />
internal class InvitationTokenFactory : IInvitationTokenFactory
{
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public InvitationTokenFactory(
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public Token Create(Guid userId, Guid gameId, TokenType type)
    {
        return new Token
        {
            TokenId = _guidFactory.Create(),
            UserId = userId,
            EntityId = gameId,
            Type = type,
            CreatedUtc = _dateTimeProvider.Now,
            IsRemoved = false
        };
    }
}
