using System;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Content;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Parsing;
using DM.Testing.Dsl;
using Xunit;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// A tag nobody closed is printed as it was typed, and a hidden block written
/// under it stays hidden on every path a text is read by.
/// </summary>
/// <remarks>
/// An unclosed tag used to be drawn to the end of the text, and an unclosed
/// [code] or [noparse] used to show everything after it as an example unless a
/// hidden block stood in it, in which case the first such block was read after
/// all. Now every one of them is the text it is, and what follows its opening is
/// read like any other text: a hidden block under it is a hidden block by the
/// ordinary rule. That is the rule the privacy of the block rests on, so it is
/// asked of every tag a post can leave open and on every path - the page for a
/// reader the block is not for and for one it is, the plain text the mail and
/// the search are built from, the second lock the search cuts with, the author's
/// editor and the save path.
/// </remarks>
public class BbUnclosedTagShould
{
    private static readonly Guid AuthorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid LeadId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OwnerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid StrangerId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private const string Secret = "секрет";

    private readonly IBbParserProvider _parserProvider = new BbParserProvider();

    /// <summary>Every tag a post can leave open, in the cases a post has them in.</summary>
    private static readonly string[] Openers =
    [
        "[b]", "[B]", "[i]", "[u]", "[strike]",
        "[quote]", "[QUOTE]", "[spoiler]", "[spoiler text=\"Подпись\"]", "[nsfw]",
        "[ul]", "[ol]", "[li]",
        "[code]", "[CODE]", "[noparse]", "[NoParse]",
        "[img]", "[img width=\"300\"]", "[link]", "[link text=\"Главная\"]", "[mention]"
    ];

    /// <summary>The hidden blocks, each addressed to the same character, in both cases of the name.</summary>
    private static readonly string[] Blocks =
    [
        "[private=\"Вася\"]" + Secret + "[/private]",
        "[PRIVATE=\"Вася\"]" + Secret + "[/PRIVATE]"
    ];

    public static TheoryData<string, string> OpenersAndBlocks()
    {
        var data = new TheoryData<string, string>();
        foreach (var opener in Openers)
        {
            foreach (var block in Blocks)
            {
                data.Add(opener, block);
            }
        }

        return data;
    }

    public static TheoryData<string> AllOpeners() => new(Openers);

    [Theory]
    [MemberData(nameof(OpenersAndBlocks))]
    public void ShowTheBlockToItsReadersAlone_UnderATagLeftOpen(string opener, string block)
    {
        var input = $"до {opener} пример {block} хвост";
        var wrapper = (BbParserWrapper)_parserProvider.GetForSurface(BbSurface.GamePost);
        var snapshot = PrivateAddresseeSnapshot.Read(PrivateAddresseeSnapshot.Build(input,
            [new PrivateAddressee(Guid.NewGuid(), "Вася", OwnerId)]));

        wrapper.RenderHtml(input, Display(BbSurface.GamePost, StrangerId, snapshot))
            .Should().Be($"до {opener} пример  хвост");

        foreach (var reader in new[] { AuthorId, LeadId, OwnerId })
        {
            wrapper.RenderHtml(input, Display(BbSurface.GamePost, reader, snapshot))
                .Should().Be($"до {opener} пример {HiddenBlock(Secret)} хвост");
        }
    }

    [Theory]
    [MemberData(nameof(OpenersAndBlocks))]
    public void KeepTheBlockOutOfThePlainTextAndTheSearch_UnderATagLeftOpen(string opener, string block)
    {
        var input = $"до {opener} пример {block} хвост";
        var wrapper = (BbParserWrapper)_parserProvider.GetForSurface(BbSurface.GamePost);

        wrapper.RenderText(input, RenderContext.ForPlainText() with { Surface = BbSurface.GamePost })
            .Should().Be($"до {opener} пример  хвост");
        new Regex(PrivateBlockMarkup.BlockPattern).Replace(input, " ").Should().NotContain(Secret);
    }

    /// <summary>
    /// The author's editor gets the block as a block, so a save that changes
    /// nothing writes back the text that was loaded.
    /// </summary>
    /// <remarks>
    /// The danger here is the other way round from the page: a code block drawn
    /// around the hidden one in the editor would be written back with its closing
    /// tag, and the block would become an example printed to the room.
    /// </remarks>
    [Theory]
    [MemberData(nameof(OpenersAndBlocks))]
    public void GiveTheAuthorTheBlockAsABlock_UnderATagLeftOpen(string opener, string block)
    {
        var input = $"до {opener} пример {block} хвост";
        var wrapper = (BbParserWrapper)_parserProvider.GetForAuthorEdit(BbSurface.GamePost);

        wrapper.RenderHtml(input, RenderContext.ForAuthorEdit(Subject(AuthorId), BbSurface.GamePost))
            .Should().Be($"{BbParserWrapper.LineBreaksWritten}до {opener} пример " +
                         "<span class=\"private-message\" data-bb-tag=\"private\" " +
                         $"data-bb-addressees=\"Вася\">{Secret}</span> хвост");
    }

    /// <summary>
    /// Off the game post the block is erased for everybody, its author as well,
    /// whatever was left open above it.
    /// </summary>
    [Theory]
    [MemberData(nameof(OpenersAndBlocks))]
    public void EraseTheBlockOffTheGamePost_UnderATagLeftOpen(string opener, string block)
    {
        var input = $"до {opener} пример {block} хвост";
        var wrapper = (BbParserWrapper)_parserProvider.GetForSurface(BbSurface.Comment);
        var snapshot = PrivateAddresseeSnapshot.Read(PrivateAddresseeSnapshot.Empty);

        wrapper.RenderHtml(input, Display(BbSurface.Comment, AuthorId, snapshot))
            .Should().Be($"до {opener} пример  хвост");
    }

    /// <summary>
    /// The save path accepts the block under an ordinary tag left open, and
    /// refuses it under a [noparse] left open: the author almost always wrote an
    /// example there and forgot the closing tag.
    /// </summary>
    /// <remarks>
    /// [code] is an ordinary tag here. It changes the font and shows nothing as
    /// an example, so a block under one left open is no forgotten example.
    /// </remarks>
    [Theory]
    [MemberData(nameof(OpenersAndBlocks))]
    public void RefuseTheBlockOnlyUnderANoparseLeftOpen(string opener, string block)
    {
        var input = $"до {opener} пример {block} хвост";
        var underNoparse = opener.StartsWith("[noparse", StringComparison.OrdinalIgnoreCase);

        PrivateBlockMarkup.IsBalanced(input).Should().Be(!underNoparse);
        PrivateBlockMarkup.ReadAddresseeAttributes(input).Should().Equal("Вася");
    }

    /// <summary>
    /// A hidden block that is not closed either holds the rest of the text,
    /// under whatever was left open above it.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllOpeners))]
    public void HideTheRestOfTheText_WhenTheBlockUnderATagLeftOpenIsNotClosedEither(string opener)
    {
        var input = $"до {opener} пример [PRIVATE=\"Вася\"]{Secret} хвост";
        var wrapper = (BbParserWrapper)_parserProvider.GetForSurface(BbSurface.GamePost);
        var snapshot = PrivateAddresseeSnapshot.Read(PrivateAddresseeSnapshot.Empty);

        wrapper.RenderHtml(input, Display(BbSurface.GamePost, StrangerId, snapshot))
            .Should().Be($"до {opener} пример ");
        wrapper.RenderText(input, RenderContext.ForPlainText() with { Surface = BbSurface.GamePost })
            .Should().Be($"до {opener} пример ");
        new Regex(PrivateBlockMarkup.BlockPattern).Replace(input, " ").Should().NotContain(Secret);
        PrivateBlockMarkup.IsBalanced(input).Should().BeFalse();
    }

    private static string HiddenBlock(string content) =>
        $"<span class=\"private-message\" data-recipients=\"DungeonMaster, Assistant, Вася\">{content}</span>";

    private static RenderContext Display(BbSurface surface, Guid viewer, PrivateAddresseeSnapshotView snapshot) =>
        new()
        {
            Audience = RenderAudience.Display,
            Surface = surface,
            Viewer = Subject(viewer),
            PostAuthorUserId = AuthorId,
            GameLeadUserIds = [LeadId],
            PrivateAddresseeOwnerUserIdsByAttribute = snapshot.OwnerUserIdsByAttribute,
            PrivateAddresseeNamesByAttribute = snapshot.AddresseeNamesByAttribute
        };

    private static IAuthorizationSubject Subject(Guid userId) => new TestSubject
    {
        UserId = userId,
        Role = UserRole.RegularUser,
        IsAuthenticated = true,
        AccessPolicy = AccessPolicy.NotSpecified
    };
}
