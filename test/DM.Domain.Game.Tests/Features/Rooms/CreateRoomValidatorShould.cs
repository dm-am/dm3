using System;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Rooms;
using DM.Testing;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Rooms;

public class CreateRoomValidatorShould : UnitTestBase
{
    private readonly CreateRoomValidator validator = new();

    [Fact]
    public void PassForValidInput()
    {
        var input = new CreateRoom
        {
            GameId = Guid.NewGuid(),
            Title = "Tavern"
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenEnumFieldsAreNotInEnum()
    {
        var input = new CreateRoom
        {
            GameId = Guid.NewGuid(),
            Title = "Tavern",
            Type = (RoomType)30000,
            AccessType = (RoomAccessType)30000
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(c => c.Type).WithErrorMessage(ValidationError.Invalid);
        result.ShouldHaveValidationErrorFor(c => c.AccessType).WithErrorMessage(ValidationError.Invalid);
    }

    [Fact]
    public void FailWhenGameIdIsEmpty()
    {
        var input = new CreateRoom
        {
            GameId = Guid.Empty,
            Title = "Tavern"
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(c => c.GameId)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTitleIsEmpty()
    {
        var input = new CreateRoom
        {
            GameId = Guid.NewGuid(),
            Title = ""
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(c => c.Title)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTitleExceedsMaxLength()
    {
        var input = new CreateRoom
        {
            GameId = Guid.NewGuid(),
            Title = new string('a', 101)
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(c => c.Title)
            .WithErrorMessage(ValidationError.Long);
    }
}
