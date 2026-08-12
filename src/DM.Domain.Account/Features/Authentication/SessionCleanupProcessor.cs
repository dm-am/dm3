using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;

namespace DM.Domain.Account.Features.Authentication;

/// <inheritdoc />
internal class SessionCleanupProcessor : ISessionCleanupProcessor
{
    private readonly IAuthenticationRepository _repository;
    private readonly IDateTimeProvider _dateTimeProvider;

    public SessionCleanupProcessor(
        IAuthenticationRepository repository,
        IDateTimeProvider dateTimeProvider)
    {
        _repository = repository;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public Task<SessionPurgeResult> PurgeExpiredAsync(CancellationToken cancellationToken = default) =>
        _repository.PurgeExpiredSessions(_dateTimeProvider.Now, cancellationToken);
}
