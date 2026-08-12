namespace DM.Infrastructure.Persistence.Shared.Queries;

/// <summary>
/// SSOT for turning what a reader typed into an ILIKE pattern.
/// </summary>
/// <remarks>
/// "%" and "_" are ordinary characters in a search box and wildcards to LIKE and ILIKE.
/// Passed through unescaped, "50%" matches every row of the table and "он_" matches "она",
/// "оно" and "они": the reader gets an answer to a question he did not ask and has no way
/// to see why. ChatRepository names the same trap in as many words over the login lookup.
///
/// The backslash is the escape character, so it is escaped first — and a pattern that ends
/// on a lone backslash is not a pattern at all but an error from the database, which is a
/// 500 on a search box rather than an empty page of results.
///
/// Written out by hand in three repositories, absent in a fourth and in three ordering
/// clauses: three copies of a rule are what let the places without it look like the same
/// thing. One place now, and adding a search means calling it.
/// </remarks>
internal static class LikePatterns
{
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
