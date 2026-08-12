using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Core.Abstractions;

namespace DM.Domain.Account.Features.Registration;

/// <inheritdoc />
internal class PendingRegistrationCleanupProcessor : IPendingRegistrationCleanupProcessor
{
    private readonly IRegistrationRepository _repository;
    private readonly IDateTimeProvider _dateTimeProvider;

    public PendingRegistrationCleanupProcessor(
        IRegistrationRepository repository,
        IDateTimeProvider dateTimeProvider)
    {
        _repository = repository;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public Task<int> DeleteExpiredAsync(CancellationToken cancellationToken = default) =>
        _repository.DeletePendingStartedBefore(
            _dateTimeProvider.Now - AccountRetentionPolicy.PendingRegistrationLifetime, cancellationToken);
}
