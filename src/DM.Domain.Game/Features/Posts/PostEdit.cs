using System;
using DM.Domain.Core.Dto;

namespace DM.Domain.Game.Features.Posts;

/// <summary>
/// Domain model for post edit history entry
/// </summary>
public class PostEdit
{
    /// <summary>
    /// Edit record identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Edit timestamp (UTC)
    /// </summary>
    public DateTimeOffset EditedUtc { get; set; }

    /// <summary>
    /// Editor user
    /// </summary>
    public GeneralUser Editor { get; set; } = null!;
}
