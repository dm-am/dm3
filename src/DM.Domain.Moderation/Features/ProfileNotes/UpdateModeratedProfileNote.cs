using System;

namespace DM.Domain.Moderation.Features.ProfileNotes;

public class UpdateModeratedProfileNote
{
    public Guid Id { get; set; }

    public string Text { get; set; } = string.Empty;
}
