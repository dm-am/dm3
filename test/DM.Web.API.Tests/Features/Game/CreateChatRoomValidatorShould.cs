using DM.Testing;
using DM.Web.API.Features.Game.ChatRooms;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Web.API.Tests.Features.Game;

public class CreateChatRoomValidatorShould : UnitTestBase
{
    private readonly CreateChatRoomValidator validator = new();

    [Fact]
    public void PassForValidInput()
    {
        var input = new CreateChatRoom { Title = "Party chat" };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenTitleIsEmpty()
    {
        var input = new CreateChatRoom { Title = "" };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage("Title is required");
    }

    [Fact]
    public void FailWhenTitleExceedsMaxLength()
    {
        var input = new CreateChatRoom { Title = new string('a', 101) };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage("Title cannot exceed 100 characters");
    }
}
