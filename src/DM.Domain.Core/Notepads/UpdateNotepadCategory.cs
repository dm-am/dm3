namespace DM.Domain.Core.Notepads;

/// <summary>
/// Update notepad category request
/// </summary>
public class UpdateNotepadCategory
{
    /// <summary>Category name</summary>
    public string? Name { get; set; }

    /// <summary>Sort order</summary>
    public int? SortOrder { get; set; }
}
