using System;
using DM.Domain.Core.Abstractions;

namespace DM.Testing;

public sealed class FixedCorrelationToken(Guid current) : ICorrelationTokenProvider
{
    public Guid Current { get; } = current;
}
