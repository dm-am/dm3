using System;
using System.Collections.Generic;
using DM.Domain.Core.Abstractions;

namespace DM.Domain.Game.Features.Posts;

/// <inheritdoc />
internal class DiceRoller : IDiceRoller
{
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

                    if (!willExplode)
                    {
                        break;
                    }

                    explosions++;
                }
            }

            rolls.Add(new DiceRoll
            {
                Id = _guidFactory.Create(),
                PostId = postId,
                CreatedUtc = createdUtc,
                IsHidden = spec.IsHidden,
                DiceCount = diceCount,
                EdgesCount = edges,
                // The resolved cap, so a bounded roll never reads as the domain's "null is unlimited".
                ExplosionCount = explosionCap,
                Bonus = spec.Bonus,
                Comment = spec.Comment ?? string.Empty,
                Results = results
            });
        }

        return rolls;
    }

    private static int ResolveExplosionCap(int? requested) => requested is > 0 ? requested.Value : 0;
}
