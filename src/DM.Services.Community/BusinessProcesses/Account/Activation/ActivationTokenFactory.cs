using System;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.Community.BusinessProcesses.Account.Activation;

/// <inheritdoc />
internal class ActivationTokenFactory : IActivationTokenFactory
{
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public ActivationTokenFactory(
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public Token Create(Guid userId)
    {
        return new Token
        {
            TokenId = _guidFactory.Create(),
            UserId = userId,
            Type = TokenType.Activation,
            CreateDate = _dateTimeProvider.Now,
            IsRemoved = false
        };
    }
}