namespace DM.Domain.Account.Features.UsernameChange;

/// <summary>
/// Joins a note about what happened to a request onto the moderator's own
/// comment, which is usually not there.
/// </summary>
/// <remarks>
/// Approving a request takes no comment - only rejection demands one - so the
/// stored comment is normally empty by the time an approval lapses or a rename
/// is rolled back. Two places pasted their note onto it with the separator baked
/// in, and produced " | Токен истек..." with nothing before the bar.
///
/// The other half is length. The column holds <see cref="MaxLength"/> characters
/// and a moderator's comment is validated at exactly that, so any note appended
/// to a full comment overflows it. The write that overflows is not one row: the
/// expiry pass updates every lapsed approval in a single statement, so one long
/// comment would roll the whole pass back, every hour, silently. The note is the
/// half that gets cut, because it is the half nobody typed.
/// </remarks>
public static class ResolutionComment
{
    /// <summary>How much the column holds.</summary>
    public const int MaxLength = 500;

    /// <summary>Placed between a moderator's own comment and the appended note.</summary>
    public const string Separator = " | ";

    /// <summary>
    /// Returns the note alone when there is no comment to join it to, and the two
    /// joined otherwise. The result never exceeds <see cref="MaxLength"/>.
    /// </summary>
    public static string Join(string? comment, string note)
    {
        var joined = string.IsNullOrEmpty(comment) ? note : comment + Separator + note;
        return joined.Length <= MaxLength ? joined : joined[..MaxLength];
    }
}
