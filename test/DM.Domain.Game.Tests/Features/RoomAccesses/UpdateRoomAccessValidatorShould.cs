using System;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.RoomAccesses;
using DM.Testing;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Game.Tests.Features.RoomAccesses;

public class UpdateRoomAccessValidatorShould : UnitTestBase
{
    private readonly UpdateRoomAccessValidator validator = new();

    [Theory]
    [InlineData(RoomAccessPolicy.ReadOnly)]
    [InlineData(RoomAccessPolicy.Full)]
    public void PassForValidInput(RoomAccessPolicy policy)
    {
        var input = new UpdateRoomAccess
        {
            AccessId = Guid.NewGuid(),
            Policy = policy
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenAccessIdIsEmpty()
    {
        var input = new UpdateRoomAccess
        {
            AccessId = Guid.Empty,
            Policy = RoomAccessPolicy.Full
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(c => c.AccessId)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenPolicyIsNotInEnum()
    {
        var input = new UpdateRoomAccess
        {
            AccessId = Guid.NewGuid(),
            Policy = (RoomAccessPolicy)30000
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(c => c.Policy)
            .WithErrorMessage(ValidationError.Invalid);
    }

    [Fact]
    public void FailWhenPolicyIsNoAccess()
    {
        var input = new UpdateRoomAccess
        {
            AccessId = Guid.NewGuid(),
            Policy = RoomAccessPolicy.NoAccess
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(c => c.Policy)
            .WithErrorMessage(ValidationError.Invalid);
    }
}
