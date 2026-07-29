namespace DM.Domain.Core.Notepads;

/// <summary>
/// Update notepad entry request
/// </summary>
public class UpdateNotepadEntry
{
    /// <summary>Entry title</summary>
    public string? Title { get; set; }

    /// <summary>Entry content</summary>
    public string? Content { get; set; }

    /// <summary>Sort order</summary>
    public int? SortOrder { get; set; }
}
