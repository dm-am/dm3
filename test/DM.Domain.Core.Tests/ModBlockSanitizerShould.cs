using DM.Domain.Core.Content;
using DM.Domain.Core.Enums;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Core.Tests;

/// <summary>
/// The rule that decides who may author a [mod] block.
/// </summary>
/// <remarks>
/// [mod] is public on read and restricted on write: an author below Moderator
/// has the markers stripped and the inner text kept, at save time, with no error
/// and no data loss. Twenty-six call sites in twelve services lean on that, and
/// none of them covered it — the sanitizer appeared in the suite only
/// incidentally, through the rendering visitor. A regex that stopped matching
/// "[mod=Имя]", or a comparison that let Mentor through, would have surfaced as a
/// moderator's block on an ordinary user's comment and nowhere else.
///
/// The other half of the same rule — filtering on read rather than unwrapping on
/// write — is PermissionFilteringVisitorShould, and it stays with the parser
/// wrapper it belongs to.
/// </remarks>
public class ModBlockSanitizerShould
{
    private const string Authored = "До [mod]служебная пометка[/mod] после";
    private const string Unwrapped = "До служебная пометка после";

    [Theory]
    [InlineData(UserRole.Guest)]
    [InlineData(UserRole.RegularUser)]
    [InlineData(UserRole.Mentor)]
    public void UnwrapTheBlockForAnAuthorBelowModerator(UserRole role) =>
        ModBlockSanitizer.SanitizeForAuthor(Authored, role).Should().Be(Unwrapped);

    [Theory]
    [InlineData(UserRole.Moderator)]
    [InlineData(UserRole.SeniorModerator)]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.System)]
    public void KeepTheBlockForModeratorAndAbove(UserRole role) =>
        ModBlockSanitizer.SanitizeForAuthor(Authored, role).Should().Be(Authored);

    /// <summary>
    /// More than the parser reads as the tag, on purpose: every casing, and every
    /// spelling with an attribute or a parameter, the kind a hand-written pattern
    /// usually misses.
    /// </summary>
    [Theory]
    [InlineData("[MOD]громко[/MOD]", "громко")]
    [InlineData("[mod=Гоблин]с автором[/mod]", "с автором")]
    [InlineData("[mod=\"Гоблин\"]с автором[/mod=\"x\"]", "с автором")]
    [InlineData("[mod text=\"x\"]с параметром[/mod]", "с параметром")]
    [InlineData("[mod]первый[/mod] и [mod]второй[/mod]", "первый и второй")]
    public void UnwrapEveryFormOfTheMarker(string raw, string expected) =>
        ModBlockSanitizer.SanitizeForAuthor(raw, UserRole.RegularUser).Should().Be(expected);

    /// <summary>
    /// A tag that merely starts with the same three letters is a different tag,
    /// and stripping it would eat an ordinary author's text.
    /// </summary>
    [Theory]
    [InlineData("[modify]правка[/modify]")]
    [InlineData("[mods]список[/mods]")]
    [InlineData("[b]жирный[/b] без пометок")]
    public void LeaveEveryOtherTagAlone(string raw) =>
        ModBlockSanitizer.SanitizeForAuthor(raw, UserRole.RegularUser).Should().Be(raw);

    /// <summary>
    /// A marker taken out joins the text on either side of it, and the join is
    /// not allowed to be a marker.
    /// </summary>
    /// <remarks>
    /// One pass over the text used to be all there was, so [[mod]mod] lost its
    /// inner marker and kept the outer one, and a user below Moderator stored a
    /// moderator's block by typing the tag inside itself.
    /// </remarks>
    [Theory]
    [InlineData("[[mod]mod]FAKE[/[mod]mod]", "FAKE")]
    [InlineData("[[[mod]mod]mod]FAKE[/[/mod]mod]", "FAKE")]
    [InlineData("[[MOD]Mod]FAKE[/[/mod]MOD]", "FAKE")]
    [InlineData("[mo[mod]d]FAKE[/m[/mod]od]", "FAKE")]
    [InlineData("[[mod]mod=\"x\"]FAKE[[mod]/mod]", "FAKE")]
    [InlineData("[b][[mod]mod][/b]", "[b][/b]")]
    public void UnwrapAMarkerThatTheRemovalOfAnotherWouldJoin(string raw, string expected) =>
        ModBlockSanitizer.SanitizeForAuthor(raw, UserRole.RegularUser).Should().Be(expected);

    /// <summary>
    /// The property itself, over every join of a handful of pieces: whatever an
    /// author below Moderator typed, no marker of the tag is left in what is
    /// stored, in any casing.
    /// </summary>
    [Fact]
    public void LeaveNoMarkerInAnyJoinOfThePieces()
    {
        string[] pieces = ["[", "]", "/", "mod", "MoD", "=", " ", "x", "[mod]", "[/mod]"];
        var marker = new System.Text.RegularExpressions.Regex(
            @"\[/?mod(?:[=\s][^\[\]]*)?\]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var survivors = new System.Collections.Generic.List<string>();
        foreach (var raw in Joins(pieces, 5))
        {
            var sanitized = ModBlockSanitizer.SanitizeForAuthor(raw, UserRole.RegularUser);
            if (marker.IsMatch(sanitized)) survivors.Add($"{raw} -> {sanitized}");
        }

        survivors.Should().BeEmpty();
    }

    private static System.Collections.Generic.IEnumerable<string> Joins(string[] pieces, int length)
    {
        if (length == 0)
        {
            yield return string.Empty;
            yield break;
        }

        foreach (var head in pieces)
        {
            foreach (var tail in Joins(pieces, length - 1))
            {
                yield return head + tail;
            }
        }
    }

    [Fact]
    public void ReturnEmptyInputUnchanged() =>
        ModBlockSanitizer.SanitizeForAuthor(string.Empty, UserRole.RegularUser)
            .Should().BeEmpty();
}
