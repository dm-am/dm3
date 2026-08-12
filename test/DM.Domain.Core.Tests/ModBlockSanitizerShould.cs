using DM.Domain.Core.Content;
using DM.Domain.Core.Enums;
using FluentAssertions;
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
    /// Both spellings the parser accepts, in both cases: the attribute form is
    /// the one a hand-written pattern usually misses.
    /// </summary>
    [Theory]
    [InlineData("[MOD]громко[/MOD]", "громко")]
    [InlineData("[mod=Гоблин]с автором[/mod]", "с автором")]
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

    [Fact]
    public void ReturnEmptyInputUnchanged() =>
        ModBlockSanitizer.SanitizeForAuthor(string.Empty, UserRole.RegularUser)
            .Should().BeEmpty();
}
