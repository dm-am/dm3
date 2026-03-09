using System;

namespace DM.Domain.Core.Dto;

/// <summary>
/// Entity that references a user as a target.
/// Used for Warning, Ban, ProfileNote, ModNote, Invitation.
/// </summary>
public interface IReferencesUser
{
    /// <summary>
    /// ID of the user this entity refers to.
    /// For Warning/Ban - the user who received the warning/ban.
    /// For ProfileNote/ModNote - the user the note is about.
    /// For Invitation - the user being invited.
    /// </summary>
    Guid TargetUserId { get; }
}
