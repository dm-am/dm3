using System;

namespace DM.Domain.Moderation.Features.ProfileNotes;

public class UpdateModeratedProfileNoteEntity
{
    public Guid Id { get; set; }

    public string? Text { get; set; }

    public DateTimeOffset ModifiedUtc { get; set; }
}
