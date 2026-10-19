namespace DM.Domain.Core.Content;

/// <summary>
/// How long the text fields of a game may be, for both layers that check them.
/// </summary>
/// <remarks>
/// One source for the API contract and for both validators, creation and editing.
/// The client keeps copies of the title and the description bounds for its form
/// (GAME_TITLE_MAX_LENGTH, GAME_INFO_MIN_LENGTH), held to these by ClientFieldLimitsShould.
/// </remarks>
public static class GameFieldLimits
{
    /// <summary>Longest game title.</summary>
    /// <remarks>As DM2's form: a title is the name a game goes by in the lists, not a sentence.</remarks>
    public const int TitleMaxLength = 50;

    /// <summary>Shortest game title: any name at all.</summary>
    public const int TitleMinLength = 1;

    /// <summary>Longest name of the system a game is played by.</summary>
    /// <remarks>
    /// The server's alone, as on DM2, whose form bounds neither name: the column is
    /// text, and the bound keeps a name a name rather than a second description.
    /// </remarks>
    public const int SystemMaxLength = 100;

    /// <summary>Longest name of the setting a game is played in, for the reason of <see cref="SystemMaxLength" />.</summary>
    public const int SettingMaxLength = 100;

    /// <summary>
    /// Shortest description of a game.
    /// </summary>
    /// <remarks>
    /// A game is offered to strangers who decide by this text alone, which is why
    /// the rule exists at all; DM2 asked as much of it.
    /// </remarks>
    public const int InfoMinLength = 100;
}
