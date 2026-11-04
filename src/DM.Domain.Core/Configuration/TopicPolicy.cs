namespace DM.Domain.Core.Configuration;

/// <summary>
/// Product rules a forum topic obeys wherever it is written down.
/// </summary>
/// <remarks>
/// The validators, the request contract and the client cannot read each other, so
/// an architecture test compares all four with this number.
/// </remarks>
public static class TopicPolicy
{
    /// <summary>
    /// The longest title a forum topic may be saved with. The column is text and holds any
    /// length: the bound keeps a pasted page out of the rows of the topic table, the
    /// breadcrumbs and the news on the main page, where the title stands in full.
    /// </summary>
    public const int TitleMaxLength = 130;
}
