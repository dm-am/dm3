using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;

namespace DM.Domain.Account.Features.Deactivation;

/// <inheritdoc />
internal class AccountRemover : IAccountRemover
{
    private readonly IDeactivationRepository _repository;
    private readonly IAuthenticationService _authenticationService;
    private readonly IEventProducer _eventProducer;

    public AccountRemover(
        IDeactivationRepository repository,
        IAuthenticationService authenticationService,
        IEventProducer eventProducer)
    {
        _repository = repository;
        _authenticationService = authenticationService;
        _eventProducer = eventProducer;
    }

    /// <inheritdoc />
    public async Task Remove(Guid userId, CancellationToken cancellationToken = default)
    {
        await _repository.DeactivateUser(userId, cancellationToken);
        await _authenticationService.LogoutAll(userId);
        await _eventProducer.SendAsync(EventType.AccountDeactivated, userId);
    }
}
