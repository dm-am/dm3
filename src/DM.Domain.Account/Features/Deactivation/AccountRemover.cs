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
    private readonly ISessionService _sessionService;
    private readonly IEventProducer _eventProducer;

    public AccountRemover(
        IDeactivationRepository repository,
        ISessionService sessionService,
        IEventProducer eventProducer)
    {
        _repository = repository;
        _sessionService = sessionService;
        _eventProducer = eventProducer;
    }

    /// <inheritdoc />
    public async Task Remove(Guid userId, CancellationToken cancellationToken = default)
    {
        await _repository.DeactivateUser(userId, cancellationToken);
        await _sessionService.LogoutAll(userId);
        await _eventProducer.SendAsync(EventType.AccountDeactivated, userId);
    }
}
