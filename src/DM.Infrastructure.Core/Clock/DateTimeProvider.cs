using System;
using DM.Domain.Core.Abstractions;

namespace DM.Infrastructure.Core.Clock;

/// <inheritdoc />
internal class DateTimeProvider : IDateTimeProvider
{
    /// <inheritdoc />
    public DateTimeOffset Now => DateTimeOffset.UtcNow;
}
