namespace DM.Domain.Community.Authorization;

/// <summary>
/// List of user endorsement actions that require authorization
/// </summary>
public enum UserEndorsementIntention
{
    Create = 1,

    Edit = 2,

    Delete = 3
}
