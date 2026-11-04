using System;
using DM.Domain.Core.Abstractions;

namespace DM.Testing;

/// <summary>The correlation token of a request, fixed for the test.</summary>
public sealed class FixedCorrelationToken(Guid current) : ICorrelationTokenProvider
{
    public Guid Current { get; } = current;
}
