namespace DM.Domain.Core.Notepads;

/// <summary>
/// Update notepad entry request
/// </summary>
public class UpdateNotepadEntry
{
    public string? Title { get; set; }

    /// <summary>Entry content</summary>
    public string? Content { get; set; }

    public int? SortOrder { get; set; }
}
