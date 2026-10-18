using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AwesomeAssertions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Content;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Parsing;
using DM.Testing.Dsl;
using Xunit;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// A character the parser removes before it reads the text does not let the
/// save path and the page read two different texts.
/// </summary>
/// <remarks>
/// The parser drops the control characters other than tab and the two line
/// breaks before reading (BbParser), and removing a character joins what stood
/// on either side of it. The save path used to read the text as stored, so
/// "[m" U+0001 "od]" was no marker to the sanitizer and a moderator block on the
/// page, and "[code" U+0001 "]" was no code block to the balance check and a
/// code block that printed the hidden block under it to the whole room.
///
/// Each check puts one character into every place of a tag and asks the save
/// path and the page together: what the sanitizer keeps renders no moderator
/// block, and a post the balance check accepts shows a stranger nothing of a
/// hidden block the save path read in it, on the page and in the plain text. The
/// characters are every control, whitespace, format and surrogate character,
/// which is where an invisible join can come from.
/// </remarks>
public class ControlCharacterMarkupShould
{
    private const string Secret = "SECRET";
    private static readonly Guid AuthorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AddresseeId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid StrangerId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private readonly IBbParserProvider _parserProvider = new BbParserProvider();

    private static IEnumerable<char> InvisibleCharacters() =>
        Enumerable.Range(0, char.MaxValue + 1)
            .Select(code => (char)code)
            .Where(character => char.IsControl(character) || char.IsWhiteSpace(character) ||
                                char.IsSurrogate(character) ||
                                char.GetUnicodeCategory(character) == UnicodeCategory.Format);

    /// <summary>The spelling with the character put at every place from the bracket to the bracket.</summary>
    private static IEnumerable<string> Spliced(string tag, char character) =>
        Enumerable.Range(1, tag.Length - 1).Select(at => tag.Insert(at, character.ToString()));

    [Fact]
    public void LeaveNoModeratorBlock_InWhatAnUnauthorisedAuthorSaves()
    {
        var wrapper = (BbParserWrapper)_parserProvider.GetForSurface(BbSurface.Comment);
        var context = StrangerContext(BbSurface.Comment, PrivateAddresseeSnapshot.Read(PrivateAddresseeSnapshot.Empty));
        var leaks = new List<string>();

        foreach (var character in InvisibleCharacters())
        {
            var inputs = Spliced("[mod]", character).Select(opening => $"{opening}FAKE[/mod]")
                .Concat(Spliced("[/mod]", character).Select(closing => $"[mod]FAKE{closing}"))
                .Concat(Spliced("[mod]", character).Zip(Spliced("[/mod]", character),
                    (opening, closing) => $"{opening}FAKE{closing}"));

            foreach (var input in inputs)
            {
                var saved = ModBlockSanitizer.SanitizeForAuthor(input, UserRole.RegularUser);
                var html = wrapper.RenderHtml(saved, context);
                if (html.Contains("mod-block", StringComparison.Ordinal))
                {
                    leaks.Add($"U+{(int)character:X4}: {Show(input)} -> {Show(html)}");
                }
            }
        }

        leaks.Should().BeEmpty("a user below Moderator cannot author a moderator block by any spelling");
    }

    [Fact]
    public void ShowAStrangerNothingOfAHiddenBlock_ThatTheSavePathAccepted()
    {
        var wrapper = (BbParserWrapper)_parserProvider.GetForSurface(BbSurface.GamePost);
        var leaks = new List<string>();

        foreach (var character in InvisibleCharacters())
        {
            foreach (var input in HiddenBlockInputs(character))
            {
                // What the save path accepts as a block addressed to A has to be one on
                // the page. What it reads as an example inside a closed [noparse] is
                // shown as the example it is.
                if (!PrivateBlockMarkup.IsBalanced(input) ||
                    !PrivateBlockMarkup.ReadAddresseeAttributes(input).Contains("A")) continue;

                var snapshot = PrivateAddresseeSnapshot.Read(PrivateAddresseeSnapshot.Build(input,
                    [new PrivateAddressee(Guid.NewGuid(), "A", AddresseeId)]));
                var html = wrapper.RenderHtml(input, StrangerContext(BbSurface.GamePost, snapshot));
                var plain = wrapper.RenderText(input,
                    RenderContext.ForPlainText() with { Surface = BbSurface.GamePost });

                if (html.Contains(Secret, StringComparison.Ordinal) ||
                    plain.Contains(Secret, StringComparison.Ordinal))
                {
                    leaks.Add($"U+{(int)character:X4}: {Show(input)} -> {Show(html)}");
                }
            }
        }

        leaks.Should().BeEmpty("a post the save path accepts shows its hidden text to nobody it does not name");
    }

    private static IEnumerable<string> HiddenBlockInputs(char character)
    {
        const string block = "[private=\"A\"]" + Secret + "[/private]";

        foreach (var verbatim in new[] { "code", "noparse" })
        {
            foreach (var opening in Spliced($"[{verbatim}]", character))
                yield return $"{opening}{block}[/{verbatim}]";
            foreach (var closing in Spliced($"[/{verbatim}]", character))
                yield return $"[{verbatim}]x{closing}{block}";
        }

        foreach (var opening in Spliced("[private=\"A\"]", character))
            yield return $"{opening}{Secret}[/private]";
        foreach (var closing in Spliced("[/private]", character))
            yield return $"[private=\"A\"]{Secret}{closing} tail";
    }

    private static RenderContext StrangerContext(BbSurface surface, PrivateAddresseeSnapshotView snapshot) => new()
    {
        Audience = RenderAudience.Display,
        Surface = surface,
        Viewer = new TestSubject
        {
            UserId = StrangerId,
            Role = UserRole.RegularUser,
            IsAuthenticated = true,
            AccessPolicy = AccessPolicy.NotSpecified
        },
        PostAuthorUserId = AuthorId,
        GameLeadUserIds = [],
        PrivateAddresseeOwnerUserIdsByAttribute = snapshot.OwnerUserIdsByAttribute,
        PrivateAddresseeNamesByAttribute = snapshot.AddresseeNamesByAttribute
    };

    private static string Show(string value) =>
        string.Concat(value.Select(character => char.IsControl(character) || char.IsSurrogate(character) ||
                                                char.GetUnicodeCategory(character) == UnicodeCategory.Format
            ? $"\\u{(int)character:X4}"
            : character.ToString()));
}
