using FluentValidation;

namespace DM.Web.API.Features.Game.ChatRooms;

/// <summary>
/// Validator for CreateChatRoom requests
/// </summary>
public class CreateChatRoomValidator : AbstractValidator<CreateChatRoom>
{
    /// <inheritdoc />
    public CreateChatRoomValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(100).WithMessage("Title cannot exceed 100 characters");
    }
}
