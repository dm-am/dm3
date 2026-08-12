namespace DM.Domain.Core.Users;

/// <summary>
/// What a username may be.
/// </summary>
/// <remarks>
/// One rule, written once. It was written six times in two incompatible models:
/// three domain validators and the client spelled out what a name may NOT
/// contain, while two request DTOs cut by a list of what it MAY, and the two
/// disagreed. The check-availability endpoint answered "free" for a name the
/// form then refused with 400 and the word "Недопустимые символы или формат",
/// because availability goes through the domain and submission through the
/// attribute above it.
///
/// The deny-list is the model the project decided on — USERNAME_POLICY.md states
/// it, and four of the six places already implemented it — so the allow-list is
/// what went, not the document.
/// </remarks>
public static class UsernamePolicy
{
    /// <summary>
    /// Two to twenty characters with at least one letter or digit among them,
    /// none of them a control character, an HTML or URL-unsafe one, a quote, a
    /// bracket or a zero-width one, and no leading, trailing or repeated
    /// whitespace. See docs/conventions/USERNAME_POLICY.md.
    /// </summary>
    /// <remarks>
    /// The "at least one letter or digit" is the one thing the allow-list this
    /// replaced enforced and the deny-list did not: it required the first and
    /// last character to be alphanumeric. Without it ".." is a valid name, and a
    /// username is a path segment — both browsers and HTTP clients resolve
    /// dot-segments away before the request leaves, so /users/.. is /users/ and
    /// that account's profile has no address at all.
    ///
    /// A const rather than a static readonly: <c>GeneratedRegex</c> needs a
    /// compile-time constant, and passing it one is what lets the three
    /// validators share this instead of each carrying its own copy.
    /// </remarks>
    public const string Pattern =
        @"^(?=.*[\p{L}\p{N}])(?!\s)(?!.*\s$)(?!.*\s{2})[^\p{Cc}<>""'`\\/@?#%&\[\](){}=~!$^*+|;:\u200B-\u200F\u2028-\u202F\uFEFF]{2,20}$";
}
