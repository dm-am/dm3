using System;

namespace DM.Domain.Community.Features.UserEndorsements;

public class CreateUserEndorsementEntity
{
    public Guid EndorsementId { get; set; }

    /// <summary>
    /// Author user identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Target user identifier (the user being endorsed)
    /// </summary>
    public Guid TargetUserId { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public required string Text { get; set; }
}
