using System;
using DM.Domain.Core.Abstractions;

namespace DM.Infrastructure.Core;

/// <inheritdoc />
internal class RandomNumberGenerator : IRandomNumberGenerator
{
    private static readonly Lazy<Random> Random = new(() => new Random());

    /// <inheritdoc />
    public int Generate(int minValue, int maxValue) => Random.Value.Next(minValue, maxValue + 1);

    /// <inheritdoc />
    public int Generate(int maxValue) => Generate(1, maxValue);
}
