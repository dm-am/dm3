using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Abstractions;
using DM.Domain.Game.Features.Posts;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Posts;

public class DiceRollerShould : UnitTestBase
{
    private readonly IRandomNumberGenerator _rng;
    private readonly DiceRoller _roller;

    public DiceRollerShould()
    {
        _rng = Mock<IRandomNumberGenerator>();

        var guidFactory = Mock<IGuidFactory>();
        guidFactory.Create().Returns(_ => Guid.NewGuid());

        _roller = new DiceRoller(_rng, guidFactory);
    }

    private void SetupRolls(params int[] values)
    {
        var queue = new Queue<int>(values);
        _rng.Generate(Arg.Any<int>()).Returns(_ => queue.Dequeue());
    }

    [Fact]
    public void RollRequestedNumberOfDice()
    {
        SetupRolls(3, 5, 2);
        var spec = new CreatePostDiceRoll { EdgesCount = 6, DiceCount = 3 };

        var roll = _roller.Roll(Guid.NewGuid(), DateTimeOffset.UtcNow, new[] { spec }).Single();

        roll.Results.Should().HaveCount(3);
        roll.Results.Select(r => r.Value).Should().Equal(3, 5, 2);
    }

    [Fact]
    public void IncludeBonusInTotal()
    {
        SetupRolls(4, 4);
        var spec = new CreatePostDiceRoll { EdgesCount = 6, DiceCount = 2, Bonus = 3 };

        var roll = _roller.Roll(Guid.NewGuid(), DateTimeOffset.UtcNow, new[] { spec }).Single();

        roll.Total.Should().Be(11); // 4 + 4 + bonus 3
    }

    [Fact]
    public void MarkNaturalMaxAndMinAsCritical()
    {
        SetupRolls(6, 1, 3);
        var spec = new CreatePostDiceRoll { EdgesCount = 6, DiceCount = 3 };

        var results = _roller.Roll(Guid.NewGuid(), DateTimeOffset.UtcNow, new[] { spec })
            .Single().Results.ToList();

        results[0].IsCritical.Should().BeTrue();  // natural max
        results[1].IsCritical.Should().BeTrue();  // natural min
        results[2].IsCritical.Should().BeFalse();
    }

    [Fact]
    public void NotExplodeByDefault()
    {
        SetupRolls(6); // rolled the maximum, but exploding is off
        var spec = new CreatePostDiceRoll { EdgesCount = 6, DiceCount = 1, ExplosionCount = null };

        var roll = _roller.Roll(Guid.NewGuid(), DateTimeOffset.UtcNow, new[] { spec }).Single();

        roll.Results.Should().HaveCount(1);
        roll.Results.Single().IsExploded.Should().BeFalse();
    }

    [Fact]
    public void ExplodeUpToTheRequestedCap()
    {
        // Every roll is the maximum, so it keeps exploding until the cap.
        SetupRolls(6, 6, 6, 6);
        var spec = new CreatePostDiceRoll { EdgesCount = 6, DiceCount = 1, ExplosionCount = 2 };

        var results = _roller.Roll(Guid.NewGuid(), DateTimeOffset.UtcNow, new[] { spec })
            .Single().Results.ToList();

        results.Should().HaveCount(3); // base roll + 2 explosions
        results[0].IsExploded.Should().BeTrue();
        results[1].IsExploded.Should().BeTrue();
        results[2].IsExploded.Should().BeFalse(); // cap reached, no further re-roll
    }

    [Fact]
    public void CarrySpecMetadataOntoTheRoll()
    {
        SetupRolls(2);
        var postId = Guid.NewGuid();
        var createdUtc = DateTimeOffset.UtcNow;
        var spec = new CreatePostDiceRoll
        {
            EdgesCount = 6,
            DiceCount = 1,
            Bonus = 1,
            IsHidden = true,
            Comment = "attack"
        };

        var roll = _roller.Roll(postId, createdUtc, new[] { spec }).Single();

        roll.PostId.Should().Be(postId);
        roll.CreatedUtc.Should().Be(createdUtc);
        roll.IsHidden.Should().BeTrue();
        roll.Comment.Should().Be("attack");
        roll.EdgesCount.Should().Be(6);
        roll.DiceCount.Should().Be(1);
    }

    [Fact]
    public void SkipInvalidSpecs()
    {
        // A d1 (edges < 2) is not a valid die and must be dropped, never rolled.
        var spec = new CreatePostDiceRoll { EdgesCount = 1, DiceCount = 1 };

        var rolls = _roller.Roll(Guid.NewGuid(), DateTimeOffset.UtcNow, new[] { spec });

        rolls.Should().BeEmpty();
        _rng.DidNotReceive().Generate(Arg.Any<int>());
    }
}
