using System;
using DM.Domain.Core.Enums;
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FailWhenAnEnumFieldIsNotInEnum(bool onType)
    {
        var input = new UpdateRoom
        {
            RoomId = Guid.NewGuid(),
            Type = onType ? (RoomType)30000 : null,
            AccessType = onType ? null : (RoomAccessType)30000
        };

        var result = validator.TestValidate(input);
        if (onType)
        {
            result.ShouldHaveValidationErrorFor(r => r.Type).WithErrorMessage(ValidationError.Invalid);
        }
        else
        {
            result.ShouldHaveValidationErrorFor(r => r.AccessType).WithErrorMessage(ValidationError.Invalid);
        }
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
