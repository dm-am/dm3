namespace DM.Domain.Core.Configuration;

/// <summary>
/// Product rules a character obeys wherever it is written down.
/// </summary>
/// <remarks>
/// The name limit is one decision kept in three places that cannot read each
/// other: the validators that answer a save, the column the name is stored in,
/// and the browser that stops the typing before either is reached. This is the
/// one the server compiles against; the migration and the client constant
/// repeat the number, and an architecture test compares all three, because a
/// form accepting more than the validator does turns a finished name into a
/// refusal that names neither the field nor a number.
///
/// Forty is measured rather than picked. The name is shown in the roster column
/// of the game page, in the fixed left column of every post and in the character
/// card, and forty characters are the most that still fit the roster in two
/// lines on a narrow desktop. Nothing breaks above it (the roster grows in
/// height, the card truncates), so the limit buys tidiness rather than safety,
/// which is why it is not smaller: composite names are the norm of the genre.
/// </remarks>
public static class CharacterPolicy
{
    /// <summary>
    /// The longest name a character may be saved with.
    /// </summary>
    public const int NameMaxLength = 40;
}
