using System;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Content;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Parsing;
using Xunit;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// A tag nobody closed is printed as it was typed, and a hidden block written
/// under it stays hidden on every path a text is read by.
/// </summary>
/// <remarks>
/// What follows the opening of an unclosed tag is read like any other text, so a hidden block under it
/// is hidden by the ordinary rule. Asked of every tag a post can leave open, on every path a text is read by.
/// </remarks>
public class BbUnclosedTagShould
{
    private static readonly Guid LeadId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OwnerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid StrangerId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private const string Secret = "секрет";

    private readonly IBbParserProvider _parserProvider = new BbParserProvider();

    private static readonly string[] Openers =
    [
        "[b]", "[B]", "[i]", "[u]", "[strike]",
        "[quote]", "[QUOTE]", "[spoiler]", "[spoiler text=\"Подпись\"]", "[nsfw]",
        "[ul]", "[ol]", "[li]",
        "[code]", "[CODE]", "[noparse]", "[NoParse]",
        "[img]", "[img width=\"300\"]", "[link]", "[link text=\"Главная\"]", "[mention]"
    ];

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
    public void ShowTheBlockToItsReadersAloneUnderATagLeftOpen(string opener, string block)
    {
        var input = $"до {opener} пример {block} хвост";
        var wrapper = _parserProvider.GetForSurface(BbSurface.GamePost);
        var snapshot = PrivateAddresseeSnapshot.Read(PrivateAddresseeSnapshot.Build(input,
            [new PrivateAddressee(Guid.NewGuid(), "Вася", OwnerId)]));

        wrapper.RenderHtml(input, Display(BbSurface.GamePost, StrangerId, snapshot))
            .Should().Be($"до {opener} пример  хвост");

        foreach (var reader in new[] { BbVisibilityRender.AuthorId, LeadId, OwnerId })
        {
            wrapper.RenderHtml(input, Display(BbSurface.GamePost, reader, snapshot))
                .Should().Be($"до {opener} пример {HiddenBlock(Secret)} хвост");
        }
    }

    [Theory]
    [MemberData(nameof(OpenersAndBlocks))]
    public void KeepTheBlockOutOfThePlainTextAndTheSearchUnderATagLeftOpen(string opener, string block)
    {
        var input = $"до {opener} пример {block} хвост";
        var wrapper = _parserProvider.GetForSurface(BbSurface.GamePost);

        wrapper.RenderText(input, RenderContext.ForPlainText() with { Surface = BbSurface.GamePost })
            .Should().Be($"до {opener} пример  хвост");
        new Regex(PrivateBlockMarkup.BlockPattern).Replace(input, " ").Should().NotContain(Secret);
    }

    [Theory]
    [MemberData(nameof(OpenersAndBlocks))]
    public void EraseTheBlockOffTheGamePostUnderATagLeftOpen(string opener, string block)
    {
        var input = $"до {opener} пример {block} хвост";
        var wrapper = _parserProvider.GetForSurface(BbSurface.Comment);
        var snapshot = PrivateAddresseeSnapshot.Read(PrivateAddresseeSnapshot.Empty);

        wrapper.RenderHtml(input, Display(BbSurface.Comment, BbVisibilityRender.AuthorId, snapshot))
            .Should().Be($"до {opener} пример  хвост");
    }

    // The save path accepts the block under an ordinary tag left open, and refuses it under a
    // [noparse] left open: the author almost always wrote an example there and forgot the closing
    // tag.
    [Theory]
    [MemberData(nameof(OpenersAndBlocks))]
    public void RefuseTheBlockOnlyUnderANoparseLeftOpen(string opener, string block)
    {
        var input = $"до {opener} пример {block} хвост";
        var underNoparse = opener.StartsWith("[noparse", StringComparison.OrdinalIgnoreCase);

        PrivateBlockMarkup.IsBalanced(input).Should().Be(!underNoparse);
        PrivateBlockMarkup.ReadAddresseeAttributes(input).Should().Equal("Вася");
    }

    [Theory]
    [MemberData(nameof(AllOpeners))]
    public void HideTheRestOfTheTextWhenTheBlockUnderATagLeftOpenIsNotClosedEither(string opener)
    {
        var input = $"до {opener} пример [PRIVATE=\"Вася\"]{Secret} хвост";
        var wrapper = _parserProvider.GetForSurface(BbSurface.GamePost);
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
            PostAuthorUserId = BbVisibilityRender.AuthorId,
            GameLeadUserIds = [LeadId],
            PrivateAddresseeOwnerUserIdsByAttribute = snapshot.OwnerUserIdsByAttribute,
            PrivateAddresseeNamesByAttribute = snapshot.AddresseeNamesByAttribute
        };

    private static IAuthorizationSubject Subject(Guid userId) => BbVisibilityRender.Viewer(UserRole.RegularUser, userId);
}
