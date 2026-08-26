using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Core.Abstractions;
using Microsoft.Extensions.Options;

namespace DM.Domain.Account.Features.TwoFactor;

/// <inheritdoc />
internal class TwoFactorCleanupProcessor : ITwoFactorCleanupProcessor
{
    private readonly ITwoFactorRepository _repository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly TwoFactorConfiguration _config;

    public TwoFactorCleanupProcessor(
        ITwoFactorRepository repository,
        IDateTimeProvider dateTimeProvider,
        IOptions<TwoFactorConfiguration> config)
    {
        _repository = repository;
        _dateTimeProvider = dateTimeProvider;
        _config = config.Value;
    }

    /// <inheritdoc />
    public Task<int> DeleteAbandonedAsync(CancellationToken cancellationToken = default) =>
        _repository.DeleteAbandonedSetups(
            _dateTimeProvider.Now - TimeSpan.FromMinutes(_config.SetupWindowMinutes),
            cancellationToken);
}
