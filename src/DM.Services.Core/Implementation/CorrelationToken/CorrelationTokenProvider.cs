using System;
using Serilog.Context;

namespace DM.Services.Core.Implementation.CorrelationToken;

/// <summary>
/// Correlation token storage
/// </summary>
internal class CorrelationTokenProvider : ICorrelationTokenProvider, ICorrelationTokenSetter
{
    private Lazy<Guid> _token;

    /// <param name="guidFactory"></param>
    /// <inheritdoc />
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