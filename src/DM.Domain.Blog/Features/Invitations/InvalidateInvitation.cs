using System;

namespace DM.Domain.Blog.Features.Invitations;

/// <summary>
/// DTO for invalidating a blog invitation
/// </summary>
public class InvalidateInvitation
{
    /// <summary>
    /// Token identifier to invalidate
    /// </summary>
    public Guid TokenId { get; set; }
}
