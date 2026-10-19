using System;

namespace DM.Domain.Moderation.Features.Warnings;

/// <summary>
/// The profile of the warned user as the warning takes it: read at issue time
/// and stored once
/// </summary>
public class ProfileCapture
{
    /// <summary>
    /// The status, null when there is none
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// "О себе", null when there is none
    /// </summary>
    public string? Info { get; set; }

    /// <summary>
    /// The upload the avatar is, null when there is no avatar
    /// </summary>
    public Guid? AvatarUploadId { get; set; }
}
