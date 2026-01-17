using System;
using System.Collections.Generic;
using DM.Web.API.BbRendering;
using DM.Web.API.Dto.Users;

namespace DM.Web.API.Dto.Messaging;

/// <summary>
/// API DTO for private message
/// </summary>
public class Message
{
    /// <summary>
    /// Identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Creating moment
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Last modification moment
    /// </summary>
    public DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>
    /// Message author
    /// </summary>
    public User Author { get; set; }

    /// <summary>
    /// Message content
    /// </summary>
    public CommonBbText Text { get; set; }

    /// <summary>
    /// Message is deleted (soft delete)
    /// </summary>
    public bool IsRemoved { get; set; }

    /// <summary>
    /// Users who liked this message
    /// </summary>
    public IEnumerable<User> Likes { get; set; }
}