using FluentValidation;

namespace DM.Web.API.Features.Game.ChatRooms;

/// <summary>
/// Validator for UpdateChatRoom requests
/// </summary>
public class UpdateChatRoomValidator : AbstractValidator<UpdateChatRoom>
{
    /// <inheritdoc />
    public UpdateChatRoomValidator()
    {
        RuleFor(x => x.Title)
            .MaximumLength(100).WithMessage("Title cannot exceed 100 characters")
            .When(x => x.Title != null);
    }
}
