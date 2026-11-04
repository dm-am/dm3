namespace DM.Infrastructure.Persistence.Shared.Queries;

/// <summary>
/// The one place that turns what a reader typed into an ILIKE pattern.
/// </summary>
/// <remarks>
/// "%" and "_" are ordinary characters in a search box and wildcards to ILIKE, and the
/// backslash is the escape character, so it is escaped first: a pattern ending on a lone
/// backslash is an error from the database.
/// </remarks>
internal static class LikePatterns
{
    /// <summary>
    /// The trigram similarity above which the catalogue and user searches take a fuzzy match.
    /// </summary>
    public const double TrigramThreshold = 0.3;
    /// <summary>
    /// Pattern matching <paramref name="term" /> anywhere in the value.
    /// </summary>
    public static string Contains(string term) => $"%{Escape(term)}%";

    /// <summary>
    /// Pattern matching the values that begin with <paramref name="term" />.
    /// </summary>
    public static string StartsWith(string term) => $"{Escape(term)}%";

    /// <summary>
    /// The term as a literal: everything LIKE reads as a wildcard or as an escape,
    /// spelled so that it stands for itself. The backslash goes first, or the escapes
    /// added after it would be escaped in turn.
    /// </summary>
    public static string Escape(string term) => term
        .Replace("\\", "\\\\")
        .Replace("%", "\\%")
        .Replace("_", "\\_");
}
