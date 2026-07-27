using System;
using System.Collections.Generic;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Moderation.Moderators;

/// <summary>
/// Moderation team member with their zones of responsibility
/// </summary>
public class ModeratorOverview
{
    /// <summary>
    /// Moderator user info
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// Forum boards assigned to this moderator
    /// </summary>
    public IReadOnlyCollection<ModerationZone> Boards { get; set; } = [];

    /// <summary>
    /// Games curated by this user as premoderation mentor
    /// </summary>
    public IReadOnlyCollection<ModerationZone> CuratedGames { get; set; } = [];

    /// <summary>
    /// Blogs curated by this user as premoderation mentor
    /// </summary>
    public IReadOnlyCollection<ModerationZone> CuratedBlogs { get; set; } = [];
}

/// <summary>
/// A single zone of responsibility (forum board, game, or blog)
/// </summary>
public class ModerationZone
{
    /// <summary>
    /// Zone entity id
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Zone entity title
    /// </summary>
    public string Title { get; set; } = null!;
}
