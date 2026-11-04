namespace DM.Domain.Account.Features.UsernameChange;

/// <summary>
/// Joins a note about what happened to a request onto the moderator's own
/// comment, which is usually not there.
/// </summary>
/// <remarks>
/// The separator only stands between two parts. The note is cut to fit
/// <see cref="MaxLength"/>, not the moderator's comment: one overflow would roll
/// back the whole expiry pass.
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
