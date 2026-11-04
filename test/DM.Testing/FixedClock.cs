using System;
using DM.Domain.Core.Abstractions;

namespace DM.Testing;

/// <summary>A clock that answers the moment it was given.</summary>
public sealed class FixedClock(DateTimeOffset now) : IDateTimeProvider
{
    public DateTimeOffset Now { get; } = now;
}
