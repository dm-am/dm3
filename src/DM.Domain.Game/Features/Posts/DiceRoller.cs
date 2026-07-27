using System;
using System.Collections.Generic;
using DM.Domain.Core.Abstractions;

namespace DM.Domain.Game.Features.Posts;

/// <inheritdoc />
internal class DiceRoller : IDiceRoller
{
    /// <summary>
    /// Hard safety cap on explosions per die. Prevents a pathological spec
    /// (e.g. a d2 that keeps rolling its maximum) from looping unbounded;
    /// requested explosion caps are additionally clamped to this value.
    /// </summary>
    private const int MaxExplosionsPerDie = 100;

    private readonly IRandomNumberGenerator _rng;
    private readonly IGuidFactory _guidFactory;

    public DiceRoller(IRandomNumberGenerator rng, IGuidFactory guidFactory)
    {
        _rng = rng;
        _guidFactory = guidFactory;
    }

    /// <inheritdoc />
    public IReadOnlyList<DiceRoll> Roll(Guid postId, DateTimeOffset createdUtc, IEnumerable<CreatePostDiceRoll> specs)
    {
        var rolls = new List<DiceRoll>();

        foreach (var spec in specs)
        {
            var edges = spec.EdgesCount;
            var diceCount = spec.DiceCount;
            // Skip invalid specs defensively — the validator rejects them, but
            // a d1 (or non-positive die count) must never reach the loop below.
            if (edges < 2 || diceCount < 1) continue;

            var explosionCap = ResolveExplosionCap(spec.ExplosionCount);
            var results = new List<DiceRollResult>();

            for (var die = 0; die < diceCount; die++)
            {
                var explosions = 0;
                while (true)
                {
                    var value = _rng.Generate(edges); // 1..edges inclusive
                    var isMax = value == edges;
                    var isMin = value == 1;
                    var willExplode = isMax && explosions < explosionCap;

                    results.Add(new DiceRollResult
                    {
                        Value = value,
                        IsCritical = isMax || isMin,
                        IsExploded = willExplode
                    });

                    if (!willExplode) break;
                    explosions++;
                }
            }

            rolls.Add(new DiceRoll
            {
                Id = _guidFactory.Create(),
                PostId = postId,
                CreatedUtc = createdUtc,
                IsAdditional = false,
                IsHidden = spec.IsHidden,
                IsFair = false,
                DiceCount = diceCount,
                EdgesCount = edges,
                // Store the resolved cap (0 = no explosion) rather than the raw
                // request so the read model never sees the domain's "null =
                // unlimited" meaning for a roll that was actually bounded.
                ExplosionCount = explosionCap,
                Bonus = spec.Bonus,
                Comment = spec.Comment ?? string.Empty,
                Results = results
            });
        }

        return rolls;
    }

    private static int ResolveExplosionCap(int? requested)
    {
        if (!requested.HasValue || requested.Value <= 0) return 0;
        return Math.Min(requested.Value, MaxExplosionsPerDie);
    }
}
