using System;
using DM.Domain.Core.Abstractions;
using Serilog.Context;

namespace DM.Infrastructure.Core.Correlation;

/// <summary>
/// Correlation token storage
/// </summary>
internal class CorrelationTokenProvider : ICorrelationTokenProvider, ICorrelationTokenSetter
{
    private Lazy<Guid> _token;

    public CorrelationTokenProvider(IGuidFactory guidFactory)
    {
        _token = new Lazy<Guid>(guidFactory.Create);
    }

    /// <inheritdoc cref="ICorrelationTokenProvider" />
    public Guid Current
    {
        get => _token.Value;
        set
        {
            if (_token.IsValueCreated)
            {
                return;
            }

            LogContext.PushProperty("CorrelationToken", value);
            _token = new Lazy<Guid>(() => value);
        }
    }
}
