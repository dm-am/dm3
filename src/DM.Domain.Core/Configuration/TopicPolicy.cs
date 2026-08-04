namespace DM.Domain.Core.Configuration;

/// <summary>
/// Product rules a forum topic obeys wherever it is written down.
/// </summary>
/// <remarks>
/// The title limit is one decision kept in four places that cannot read each
/// other: the two validators that answer a save, the request contract the model
/// binder checks first, and the browser that stops the typing before either is
/// reached. Two of them disagreed with the other two. Both validators refused a
/// title over a hundred and thirty, while the create form and the contract
/// promised two hundred, so a title in between was typed in full, accepted by
/// the form and then refused by the save with a message naming no number -- and
/// the edit form of the same topic stopped at a hundred and thirty.
///
/// The number here is the one that was already enforced, so no title that saves
/// today stops saving: what changes is that the form and the contract stop
/// promising more than the save accepts. An architecture test compares all four
/// places, because none of them can read the other three.
/// </remarks>
public static class TopicPolicy
{
    /// <summary>
    /// The longest title a forum topic may be saved with.
    /// </summary>
    public const int TitleMaxLength = 130;
}
