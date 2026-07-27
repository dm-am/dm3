using System;
using System.Threading.Tasks;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Posts;
using DM.Testing;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Posts;

public class CreatePostValidatorShould : UnitTestBase
{
    private readonly CreatePostValidator validator;

    public CreatePostValidatorShould()
    {
        validator = new CreatePostValidator();
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
