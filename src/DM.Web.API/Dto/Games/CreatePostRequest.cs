using System;

namespace DM.Web.API.Dto.Games;

/// <summary>
/// Request model for creating a new post
/// </summary>
public class CreatePostRequest
{
    /// <summary>
    /// Character identifier (optional for master posts)
    /// </summary>
    public Guid? CharacterId { get; set; }

    /// <summary>
    /// Post text content
    /// </summary>
    public string Text { get; set; } = null!;

    /// <summary>
    /// Additional commentary text
    /// </summary>
    public string? Commentary { get; set; }

    /// <summary>
    /// Private message to master
    /// </summary>
    public string? MasterMessage { get; set; }
}
