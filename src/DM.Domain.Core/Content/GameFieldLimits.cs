namespace DM.Domain.Core.Content;

/// <summary>
/// How long the text fields of a game may be, for both layers that check them.
/// </summary>
/// <remarks>
/// Two layers checked the same fields by different numbers. The API contract
/// allowed a 200-character title, 100-character system and setting, and a
/// description of one character; the domain validator behind it cut the title at
/// 100, the other two at 50, and demanded at least 200 characters of description.
/// The form drew its own maxlength from the contract, so everything between the
/// two sets of numbers was accepted by the field, accepted by the contract and
/// refused by the domain — with a message the form could not render, because the
/// domain answers in codes.
///
/// The wider bound wins where the two disagreed on a maximum: narrowing one would
/// cut text a reader has already typed, and nothing in the product asked for the
/// narrower figure. The minimum is the domain's, because that rule was live —
/// a short description has been refused all along, only silently.
/// </remarks>
public static class GameFieldLimits
{
    /// <summary>Longest game title.</summary>
    public const int TitleMaxLength = 200;

    /// <summary>Shortest game title.</summary>
    public const int TitleMinLength = 3;

    /// <summary>Longest name of the system a game is played by.</summary>
    public const int SystemMaxLength = 100;

    /// <summary>Longest name of the setting a game is played in.</summary>
    public const int SettingMaxLength = 100;

    /// <summary>
    /// Shortest description of a game.
    /// </summary>
    /// <remarks>
    /// A game is offered to strangers who decide by this text alone, which is why
    /// the rule exists at all. It was enforced by the domain and hidden from the
    /// form, so the author learned about it after pressing "create".
    /// </remarks>
    public const int InfoMinLength = 200;
}
