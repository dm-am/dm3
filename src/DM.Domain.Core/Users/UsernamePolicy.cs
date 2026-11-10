using System.Text.RegularExpressions;

namespace DM.Domain.Core.Users;

/// <remarks>
/// A deny-list, as USERNAME_POLICY.md states it.
/// </remarks>
public static partial class UsernamePolicy
{
    /// <summary>
    /// Two to twenty characters with at least one letter or digit among them,
    /// none of them a control character, an HTML or URL-unsafe one, a quote, a
    /// bracket or a zero-width one, and no leading, trailing or repeated
    /// whitespace. See docs/conventions/USERNAME_POLICY.md.
    /// </summary>
    /// <remarks>
    /// "At least one letter or digit" because a username is a path segment, and
    /// clients resolve /users/.. to /users/.
    /// </remarks>
    public const string Pattern =
        @"^(?=.*[\p{L}\p{N}])(?!\s)(?!.*\s$)(?!.*\s{2})[^\p{Cc}<>""'`\\/@?#%&\[\](){}=~!$^*+|;:\u200B-\u200F\u2028-\u202F\uFEFF]{2,20}$";

    /// <summary>
    /// Whether the name is one <see cref="Pattern"/> accepts
    /// </summary>
    /// <param name="username">Name as the caller will store it</param>
    public static bool IsValid(string username) => PatternRegex().IsMatch(username);

    [GeneratedRegex(Pattern)]
    private static partial Regex PatternRegex();
}
