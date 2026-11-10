using System;
using DM.Domain.Core.Abstractions;

namespace DM.Testing;

public sealed class FixedClock(DateTimeOffset now) : IDateTimeProvider
{
    public DateTimeOffset Now { get; } = now;
}
