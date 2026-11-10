using System;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Rooms;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Rooms;

public class UpdateRoomValidatorShould
{
    private readonly UpdateRoomValidator validator = new();

    [Theory]
    [InlineData(1)]
    [InlineData(RoomFieldLimits.TitleMaxLength)]
    public void PassATitleFromOneCharacterToTheBound(int length)
    {
        var input = new UpdateRoom
        {
            RoomId = Guid.NewGuid(),
            Title = new string('a', length)
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
    public void FailWhenTypeIsNotInEnum()
    {
        var input = new UpdateRoom
        {
            RoomId = Guid.NewGuid(),
            Type = (RoomType)30000
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(r => r.Type).WithErrorMessage(ValidationError.Invalid);
    }

    [Fact]
    public void FailWhenAccessTypeIsNotInEnum()
    {
        var input = new UpdateRoom
        {
            RoomId = Guid.NewGuid(),
            AccessType = (RoomAccessType)30000
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(r => r.AccessType).WithErrorMessage(ValidationError.Invalid);
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
            Title = new string('a', RoomFieldLimits.TitleMaxLength + 1)
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(r => r.Title)
            .WithErrorMessage(ValidationError.Long);
    }
}
