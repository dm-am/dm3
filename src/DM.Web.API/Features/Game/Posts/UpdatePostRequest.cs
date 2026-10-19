namespace DM.Web.API.Features.Game.Posts;

/// <summary>
/// API DTO for editing an existing post
/// </summary>
/// <remarks>
/// Two texts, and nothing else: rerolling dice, reassigning the author and
/// moving a post between rooms are not edits.
/// </remarks>
public class UpdatePostRequest
{
    /// <summary>
    /// Game text (in-character content). An omitted value keeps the current one.
    /// </summary>
    public string? GameText { get; set; }

    /// <summary>
    /// Metagame text (OOC commentary). An omitted value keeps the current one,
    /// an empty string clears it.
    /// </summary>
    public string? MetagameText { get; set; }
}
