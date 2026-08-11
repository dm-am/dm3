using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Core.Abstractions;

namespace DM.Domain.Account.Features.Tokens;

/// <inheritdoc />
internal class TokenCleanupProcessor : ITokenCleanupProcessor
{
    private readonly ITokenMaintenanceRepository _repository;
    private readonly IDateTimeProvider _dateTimeProvider;

    public TokenCleanupProcessor(
        ITokenMaintenanceRepository repository,
        IDateTimeProvider dateTimeProvider)
    {
        _repository = repository;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public Task<int> DeleteStaleAsync(CancellationToken cancellationToken = default) =>
        _repository.DeleteWithdrawnOrIssuedBefore(
            _dateTimeProvider.Now - AccountRetentionPolicy.TokenRetention, cancellationToken);
}
