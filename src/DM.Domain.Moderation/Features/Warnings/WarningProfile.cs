using DM.Domain.Core.Dto;

namespace DM.Domain.Moderation.Features.Warnings;

/// <summary>
/// What a profile showed everybody when a warning was issued on it: the avatar,
/// the status and "О себе"
/// </summary>
/// <remarks>
/// The personal fields of the profile (the name, the place, the contacts, the
/// birthday) are not part of it, and neither is the login: they are the
/// person's to erase later, and the open log would keep them for good.
/// </remarks>
public class WarningProfile
{
    /// <summary>
    /// The status, null when the profile had none
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// "О себе" as written, null when the profile had none
    /// </summary>
    public string? Info { get; set; }

    /// <summary>
    /// The avatar the profile carried, null when it had none or when moderation
    /// has since deleted the picture
    /// </summary>
    public AvatarPicture? Picture { get; set; }
}
