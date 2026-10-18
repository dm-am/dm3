using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DM.Domain.Core.Users;

/// <summary>
/// "Аккаунт удален": the name everything removed accounts wrote is signed with,
/// and the address of the profile that gathers it.
/// </summary>
/// <remarks>
/// Not an account. No row answers to the name: the profile under it is the sum of
/// every removed row, read past the soft-delete filter by the few reads that know
/// the name (the profile, and the host, author and player filters of the lists its
/// tabs are made of). Every other read that takes a name answers it the way it
/// answers a name nobody holds. DM2 had the same address,
/// "Cabinet/?user=Аккаунт Удален", over a real shared account; the import
/// brings that row in removed, and it becomes one of the removed rows like any
/// other.
///
/// Two questions, asked by different callers. <see cref="Is" /> is whether a name
/// in an address or a filter means this profile: the way a reader types it, in any
/// case and with or without the dots over the "е". <see cref="IsReserved" /> is
/// whether a name may be taken by an account, and it is wider on purpose - a live
/// "Aккаунт удален" spelled with a Latin "A" would sign its posts exactly like the
/// removed ones, and a reader cannot tell the two apart. Everything that
/// <see cref="Is" /> accepts is reserved.
/// </remarks>
public static class RemovedAccount
{
    /// <summary>The name, as the site writes it.</summary>
    public const string Name = "Аккаунт удален";

    /// <summary>
    /// Latin letters that read as Cyrillic ones in the fonts the site uses, after
    /// lower-casing. The capitals that look alike (B, H, M, T) land here through
    /// their lower-case forms.
    /// </summary>
    private static readonly IReadOnlyDictionary<char, char> Lookalikes = new Dictionary<char, char>
    {
        ['a'] = 'а',
        ['b'] = 'в',
        ['c'] = 'с',
        ['e'] = 'е',
        ['h'] = 'н',
        ['k'] = 'к',
        ['m'] = 'м',
        ['o'] = 'о',
        ['p'] = 'р',
        ['t'] = 'т',
        ['x'] = 'х',
        ['y'] = 'у',
    };

    /// <summary>
    /// Whether the name, as a reader would type it into an address or a filter,
    /// is this profile's: surrounding spaces, the case and the dots over the "е"
    /// do not matter.
    /// </summary>
    public static bool Is(string? username) =>
        username is not null &&
        string.Equals(Fold(username.Trim()), Fold(Name), StringComparison.Ordinal);

    /// <summary>
    /// Whether no account may be named this: <see cref="Is" />, and every spelling
    /// a reader would take for it - Latin letters that look Cyrillic, spaces,
    /// underscores, hyphens and dots moved, added or dropped.
    /// </summary>
    public static bool IsReserved(string? username) =>
        username is not null && Skeleton(username) == Skeleton(Name);

    /// <summary>Whether any of the names asked for is this profile's.</summary>
    public static bool IsAmong(IEnumerable<string>? usernames) =>
        usernames is not null && usernames.Any(Is);

    // The letter with the dots is spelled as its code point: the sources keep to
    // the plain "е" everywhere, and this is the one place that has to name the other.
    private static string Fold(string value) =>
        value.ToLowerInvariant().Replace(DottedE, 'е');

    /// <summary>The lower-case "е" with two dots, U+0451.</summary>
    private const char DottedE = (char)0x0451;

    private static string Skeleton(string value)
    {
        var skeleton = new StringBuilder(value.Length);
        foreach (var symbol in Fold(value))
        {
            if (char.IsWhiteSpace(symbol) || symbol is '_' or '-' or '.')
            {
                continue;
            }

            skeleton.Append(Lookalikes.TryGetValue(symbol, out var cyrillic) ? cyrillic : symbol);
        }

        return skeleton.ToString();
    }
}
