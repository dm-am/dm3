using DM.Infrastructure.Persistence.Shared.Queries;
using AwesomeAssertions;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests.Shared.Queries;

/// <summary>
/// What a reader types is matched as text, whatever LIKE would have read it as.
/// </summary>
/// <remarks>
/// Asserted on the pattern: the in-memory provider does not read patterns. The escape is escaped before the
/// two wildcards, or it escapes them again.
/// </remarks>
public class LikePatternsShould
{
    [Theory]
    [InlineData("50%", "%50\\%%")]
    [InlineData("он_", "%он\\_%")]
    [InlineData("C:\\", "%C:\\\\%")]
    [InlineData("обычный запрос", "%обычный запрос%")]
    public void MatchWildcardCharactersLiterally(string term, string pattern) =>
        LikePatterns.Contains(term).Should().Be(pattern,
            "a percent sign typed into a search box is a percent sign, not \"anything\"");

    [Fact]
    public void EscapeTheEscapeCharacterBeforeTheWildcards() =>
        LikePatterns.Contains("\\%").Should().Be("%\\\\\\%%",
            "escaping the wildcard first would leave the backslash escaping the escape, and " +
            "the percent sign would go back to meaning \"anything\"");

    /// <summary>
    /// A backslash typed on its own reaches the database as a pair.
    /// </summary>
    /// <remarks>On the whole pattern: `%\\%` and `%\%` end alike and differ in count.</remarks>
    [Fact]
    public void SendALoneBackslashAsAnEscapedPair() =>
        LikePatterns.Contains("\\").Should().Be("%\\\\%",
            "unescaped, the backslash would escape the closing wildcard instead of standing " +
            "for itself, and the search would answer about a percent sign nobody typed");

    [Theory]
    [InlineData("50%", "50\\%%")]
    [InlineData("Иван", "Иван%")]
    public void KeepThePrefixFormOpenOnlyAtTheEnd(string term, string pattern) =>
        LikePatterns.StartsWith(term).Should().Be(pattern,
            "the prefix pattern ranks the results of a search, and a term of wildcards would " +
            "rank every row as an exact prefix match");
}
