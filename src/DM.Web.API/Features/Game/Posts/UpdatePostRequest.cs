namespace DM.Web.API.Features.Game.Posts;

/// <summary>
/// API DTO for editing an existing post
/// </summary>
/// <remarks>
/// Two texts, and nothing else. The endpoint used to take the Post response
/// DTO, so the contract asked for a room, a character, an author, an edit
/// history, dice results and a rating to change a line of text — none of them
/// required, none of them read, and no way for a reader of the document to tell
/// which of the twelve fields mattered. Rerolling dice, reassigning the author
/// and moving a post between rooms are not edits and never were: only the two
/// texts have ever reached UpdatePost.
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
