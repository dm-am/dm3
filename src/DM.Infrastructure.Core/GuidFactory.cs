using System;
using DM.Domain.Core.Abstractions;

namespace DM.Infrastructure.Core;

/// <inheritdoc />
internal class GuidFactory : IGuidFactory
{
    /// <inheritdoc />
    public Guid Create() => Guid.NewGuid();
}
