using System;
using DM.Services.Core.Dto;

namespace DM.Services.Game.Dto.Output;

/// <summary>
/// DTO model for game post
/// </summary>
public class Post
{
    /// <summary>
    /// Identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Room identifier
    /// </summary>
    public Guid RoomId { get; set; }

    /// <summary>
    /// Post author
    /// </summary>
    public GeneralUser Author { get; set; } = null!;

    /// <summary>
    /// Post last update author
    /// </summary>
    public GeneralUser ModifiedBy { get; set; } = null!;

    /// <summary>
    /// Short character information
    /// </summary>
    public CharacterShort Character { get; set; } = null!;

    /// <summary>
    /// Creating moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Last modification moment (UTC)
    /// </summary>
    public DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>
    /// Text
    /// </summary>
    public string Text { get; set; } = null!;

    /// <summary>
    /// Commentary
    /// </summary>
    public string Comment { get; set; } = null!;

    /// <summary>
    /// Message to master or master note
    /// </summary>
    public string MasterMessage { get; set; } = null!;
}