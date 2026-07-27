using System;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Rooms;
using DM.Testing;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Rooms;

public class UpdateRoomValidatorShould : UnitTestBase
{
    private readonly UpdateRoomValidator validator = new();

    [Fact]
    public void PassForValidInput()
    {
        var input = new UpdateRoom
        {
            RoomId = Guid.NewGuid(),
            Title = "Tavern"
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void PassWhenTitleIsOmitted()
    {
        var input = new UpdateRoom
        {
            RoomId = Guid.NewGuid(),
            Title = null
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenRoomIdIsEmpty()
    {
        var input = new UpdateRoom
        {
            RoomId = Guid.Empty
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(r => r.RoomId)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTitleIsEmpty()
    {
        var input = new UpdateRoom
        {
            RoomId = Guid.NewGuid(),
            Title = ""
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(r => r.Title)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTitleExceedsMaxLength()
    {
        var input = new UpdateRoom
        {
            RoomId = Guid.NewGuid(),
            Title = new string('a', 101)
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(r => r.Title)
            .WithErrorMessage(ValidationError.Long);
    }
}
