using DM.Testing;
using DM.Web.API.Features.Game.ChatRooms;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Web.API.Tests.Features.Game;

public class UpdateChatRoomValidatorShould : UnitTestBase
{
    private readonly UpdateChatRoomValidator validator = new();

    [Fact]
    public void PassForValidInput()
    {
        var input = new UpdateChatRoom { Title = "Party chat" };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void PassWhenTitleIsOmitted()
    {
        var input = new UpdateChatRoom { Title = null };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenTitleExceedsMaxLength()
    {
        var input = new UpdateChatRoom { Title = new string('a', 101) };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage("Title cannot exceed 100 characters");
    }
}
