using System;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Blog.Invitations;

/// <summary>
/// Blog invitation DTO
/// </summary>
public class BlogInvitation : InvitationRecord
{
    /// <summary>
    /// Blog ID
    /// </summary>
    public Guid BlogId { get; set; }

    /// <summary>
    /// Blog title
    /// </summary>
    public string BlogTitle { get; set; } = string.Empty;
}
