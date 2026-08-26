using System;
using System.Threading.Tasks;
using DM.Domain.Core.Content;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Posts;
using DM.Testing;
using FluentValidation.TestHelper;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Posts;

public class CreatePostValidatorShould : UnitTestBase
{
    private readonly IBbCodeNestingLimit nestingLimit = Mock<IBbCodeNestingLimit>();
    private readonly CreatePostValidator validator;

    public CreatePostValidatorShould()
    {
        nestingLimit.IsWithinLimit(Arg.Any<string>()).Returns(true);
        validator = new CreatePostValidator(nestingLimit);
    }

    /// <summary>
    /// Text the renderer will refuse is refused here, or it is stored and shows
    /// as nothing to everyone but its author.
    /// </summary>
    [Fact]
    public async Task RefuseTextTheRendererWillNotRender()
    {
        nestingLimit.IsWithinLimit(Arg.Any<string>()).Returns(false);
        var input = new CreatePost
        {
            RoomId = Guid.NewGuid(),
            GameText = "nested far too deep"
        };

        var result = await validator.TestValidateAsync(input);

        result.ShouldHaveValidationErrorFor(p => p.GameText)
            .WithErrorMessage(ValidationError.Invalid);
    }

    /// <summary>
    /// A body past the length limit is refused, and refused by length rather
    /// than by some other rule tripping over it.
    /// </summary>
    /// <remarks>
    /// The post form had no limit at all, so the parse and the render of every
    /// open of the page were bounded by the request size and nothing else.
    /// </remarks>
    [Fact]
    public async Task RefuseTextLongerThanTheLimit()
    {
        var input = new CreatePost
        {
            RoomId = Guid.NewGuid(),
            GameText = new string('x', BodyTextLimits.MaxLength + 1)
        };

        var result = await validator.TestValidateAsync(input);

        result.ShouldHaveValidationErrorFor(p => p.GameText)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public async Task PassForTextExactlyAtTheLimit()
    {
        var input = new CreatePost
        {
            RoomId = Guid.NewGuid(),
            GameText = new string('x', BodyTextLimits.MaxLength)
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task PassForValidInput()
    {
        var input = new CreatePost
        {
            RoomId = Guid.NewGuid(),
            GameText = "This is a valid post text"
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task FailWhenRoomIdIsEmpty()
    {
        var input = new CreatePost
        {
            RoomId = Guid.Empty,
            GameText = "Valid text"
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.RoomId)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public async Task FailWhenTextIsEmpty()
    {
        var input = new CreatePost
        {
            RoomId = Guid.NewGuid(),
            GameText = ""
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.GameText)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public async Task FailWhenTextIsNull()
    {
        var input = new CreatePost
        {
            RoomId = Guid.NewGuid(),
            GameText = null!
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.GameText);
    }

    [Fact]
    public async Task FailWhenTextIsWhitespace()
    {
        var input = new CreatePost
        {
            RoomId = Guid.NewGuid(),
            GameText = "   "
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.GameText);
    }

    [Fact]
    public async Task PassWithOptionalFieldsNull()
    {
        var input = new CreatePost
        {
            RoomId = Guid.NewGuid(),
            GameText = "Valid text",
            CharacterId = null,
            MetagameText = null
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task PassForValidDiceRoll()
    {
        var input = new CreatePost
        {
            RoomId = Guid.NewGuid(),
            GameText = "Valid text",
            DiceRolls = new[]
            {
                new CreatePostDiceRoll { EdgesCount = 20, DiceCount = 2, ExplosionCount = 3 }
            }
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task FailWhenDieHasFewerThanTwoEdges()
    {
        var input = new CreatePost
        {
            RoomId = Guid.NewGuid(),
            GameText = "Valid text",
            DiceRolls = new[] { new CreatePostDiceRoll { EdgesCount = 1, DiceCount = 1 } }
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor("DiceRolls[0].EdgesCount");
    }

    [Fact]
    public async Task FailWhenDiceCountIsNotPositive()
    {
        var input = new CreatePost
        {
            RoomId = Guid.NewGuid(),
            GameText = "Valid text",
            DiceRolls = new[] { new CreatePostDiceRoll { EdgesCount = 6, DiceCount = 0 } }
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor("DiceRolls[0].DiceCount");
    }
}
