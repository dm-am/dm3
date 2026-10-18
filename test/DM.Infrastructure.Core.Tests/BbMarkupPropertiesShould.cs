using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using BBCodeParser;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Content;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Parsing;
using DM.Testing.Dsl;
using DM.Testing.Markup;
using Xunit;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// The server holds to the rule of hidden text on texts nobody wrote down:
/// generated ones, from the shared alphabet (markupAlphabet.ts), read against a
/// reference reading of the rule (HiddenTextOracle).
/// </summary>
/// <remarks>
/// The grammar table holds the spellings somebody thought of; these checks hold
/// the combinations nobody did. Every word of a generated text is different, so
/// a check can follow each one to every reader of every surface, to the plain
/// text of the mail and to the embed, and say which reader got a word the rule
/// keeps from them, or lost one the rule gives them. How a failure is reported
/// and replayed: docs/guides/TESTING.md, section "Генеративные проверки
/// разметки".
/// </remarks>
public class BbMarkupPropertiesShould
{
    private static readonly Guid AuthorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid LeadId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid StrangerId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid AddresseeId = Guid.Parse("00000000-0000-0000-0001-000000000001");
    private static readonly Guid OtherOwnerId = Guid.Parse("00000000-0000-0000-0001-000000000002");

    /// <summary>The characters of the room: "Вася", whose owner is the addressee reader, and "Петя".</summary>
    private static readonly PrivateAddressee[] Roster =
    [
        new(Guid.Parse("00000000-0000-0000-0002-000000000001"), HiddenTextOracle.AddresseeName, AddresseeId),
        new(Guid.Parse("00000000-0000-0000-0002-000000000002"), "Петя", OtherOwnerId)
    ];

    private const string BlockElement = "<span class=\"private-message\" data-recipients=";

    /// <summary>An element holding nothing but white space and line breaks.</summary>
    private static readonly Regex EmptyElement = new(@"<([a-z]+)\b[^>]*>(?:\s|<br />)*</\1>", RegexOptions.Compiled);

    private static readonly IBbParserProvider Provider = new BbParserProvider();

    // Every surface, the hint of a tag description included: a generated post
    // is prose there, and a hidden block in it is erased for every reader as on
    // any surface that has none, which is what these properties ask.
    private static readonly BbSurface[] Surfaces = Enum.GetValues<BbSurface>();

    private static readonly HiddenTextReader[] Readers = Enum.GetValues<HiddenTextReader>();

    /// <summary>
    /// Every reader of every surface is shown every word the blocks around it let
    /// them read, and no other; the plain text and the embed carry no word of any
    /// block and every word outside them.
    /// </summary>
    /// <remarks>
    /// Besides the words, the blocks themselves are counted: a reader gets one
    /// block element for every block they read whose every outer block they read
    /// too, and the line "DungeonMaster, Assistant" never reaches a reader who
    /// reads no block. A block with no word in it is still held that way.
    /// </remarks>
    [Fact]
    public void ShowEveryReaderExactlyTheWordsTheBlocksLetThemRead() =>
        MarkupProperty.Check(nameof(ShowEveryReaderExactlyTheWordsTheBlocksLetThemRead), runs: 300,
            random => random.Next(4) == 0
                ? MarkupGenerator.Valid(random, random.Pick(Surfaces))
                : MarkupGenerator.Soup(random),
            input =>
            {
                var reading = HiddenTextOracle.Read(input);
                var snapshot = Snapshot(input);

                foreach (var surface in Surfaces)
                {
                    foreach (var reader in Readers)
                    {
                        var html = Display(input, surface, reader, snapshot);
                        var wrong = Misread(reading, html, reader, surface, $"{reader} on {surface}");
                        if (wrong != null) return wrong;
                    }

                    var plain = Render(() => Wrapper(Provider.GetForSurface(surface))
                        .RenderText(input, RenderContext.ForPlainText() with { Surface = surface }));
                    var plainWrong = Misread(reading, plain, reader: null, surface, $"plain text of {surface}");
                    if (plainWrong != null) return plainWrong;

                    var embed = Render(() => Wrapper(Provider.GetSafeForSurface(surface))
                        .RenderHtml(input, RenderContext.ForEmbedSafe(surface)));
                    var embedWrong = Misread(reading, embed, reader: null, surface, $"embed of {surface}");
                    if (embedWrong != null) return embedWrong;
                }

                return null;
            });

    /// <summary>
    /// A block a reader does not read leaves no trace of itself in what that
    /// reader gets: not its words, not its length, not its addressees or its
    /// spelling, and not the tags around it that held nothing else.
    /// </summary>
    /// <remarks>
    /// Two comparisons, for every reader of every surface and for the plain text
    /// and the embed. Every block the reader does not read (the outermost ones:
    /// what stands inside is gone with them) is first replaced by an empty bare
    /// block, and the reader must get exactly what they got before: nothing in
    /// the answer depends on what the block held or how it was spelled. Then it
    /// is replaced by a character, and the reader of the original must get no
    /// more empty elements than the reader of that text: a tag that held nothing
    /// but the block would otherwise stay on the page, empty, where the author
    /// wrote no empty tag.
    /// </remarks>
    [Fact]
    public void LeaveNoTraceOfABlockTheReaderDoesNotRead() =>
        MarkupProperty.Check(nameof(LeaveNoTraceOfABlockTheReaderDoesNotRead), runs: 300,
            random => random.Next(4) == 0
                ? MarkupGenerator.Valid(random, random.Pick(Surfaces))
                : MarkupGenerator.Soup(random),
            input =>
            {
                var reading = HiddenTextOracle.Read(input);
                if (reading.Blocks.Count == 0) return null;

                var text = reading.Text;
                var snapshot = Snapshot(text);

                foreach (var surface in Surfaces)
                {
                    foreach (var reader in Readers)
                    {
                        var cut = Unread(reading, block => block.IsReadBy(reader, surface));
                        if (cut.Count == 0) continue;

                        var got = Display(text, surface, reader, snapshot);
                        if (got == null) continue;

                        var trace = Trace(got, Display(Replace(text, cut, EmptyBlock), surface, reader, snapshot),
                            Display(Replace(text, cut, Stand), surface, reader, snapshot), $"{reader} on {surface}");
                        if (trace != null) return trace;

                        var lineTrace = LineTrace(got,
                            Display(WithoutTheirLines(text, cut, keepEmptyBlocks: true), surface, reader, snapshot),
                            Display(WithoutTheirLines(text, cut, keepEmptyBlocks: false), surface, reader, snapshot),
                            $"{reader} on {surface}");
                        if (lineTrace != null) return lineTrace;
                    }

                    var all = Unread(reading, _ => false);

                    var plain = Plain(text, surface);
                    if (plain != null && plain != Plain(Replace(text, all, EmptyBlock), surface))
                        return $"the plain text of {surface} depends on a block it does not show: {MarkupLiteral.Of(plain)}";

                    var plainLines = plain == null
                        ? null
                        : LineTrace(plain, Plain(WithoutTheirLines(text, all, keepEmptyBlocks: true), surface),
                            Plain(WithoutTheirLines(text, all, keepEmptyBlocks: false), surface), $"the plain text of {surface}");
                    if (plainLines != null) return plainLines;

                    var embed = Embed(text, surface);
                    if (embed == null) continue;

                    var embedTrace = Trace(embed, Embed(Replace(text, all, EmptyBlock), surface),
                        Embed(Replace(text, all, Stand), surface), $"embed of {surface}");
                    if (embedTrace != null) return embedTrace;

                    var embedLines = LineTrace(embed, Embed(WithoutTheirLines(text, all, keepEmptyBlocks: true), surface),
                        Embed(WithoutTheirLines(text, all, keepEmptyBlocks: false), surface), $"embed of {surface}");
                    if (embedLines != null) return embedLines;
                }

                return null;
            });

    /// <summary>
    /// A block a reader does not read, standing on a line of its own, takes the line
    /// with it: the reader gets what the text reads like without that line.
    /// </summary>
    /// <remarks>
    /// The texts above put a block on a line of its own only now and then, so here
    /// the blocks of a generated text, as they were spelled, are laid out on lines of
    /// their own between plain words - at the start of the text, at its end, between
    /// words or one after another, by the length of the text - and the new text is
    /// read again from scratch: what is a block, and who reads it, is whatever the
    /// rule makes of it. Every reader of every surface, the plain text and the embed
    /// then have to get what they get for the same text with those lines taken out
    /// (see <see cref="WithoutTheirLines" />). Nothing but words stands outside the
    /// blocks, so no tag of the author's can be left out around them and blur what
    /// taking a line out means.
    /// </remarks>
    [Fact]
    public void LeaveNoLineOfABlockTheReaderDoesNotRead() =>
        MarkupProperty.Check(nameof(LeaveNoLineOfABlockTheReaderDoesNotRead), runs: 300,
            random => random.Next(4) == 0
                ? MarkupGenerator.Valid(random, random.Pick(Surfaces))
                : MarkupGenerator.Soup(random),
            input =>
            {
                var first = HiddenTextOracle.Read(input);
                if (first.Blocks.Count == 0) return null;

                var reading = HiddenTextOracle.Read(OnLinesOfTheirOwn(first.Text, Unread(first, _ => false)));
                if (reading.Blocks.Count == 0) return null;

                var text = reading.Text;
                var snapshot = Snapshot(text);

                foreach (var surface in Surfaces)
                {
                    foreach (var reader in Readers)
                    {
                        var cut = Unread(reading, block => block.IsReadBy(reader, surface));
                        if (cut.Count == 0) continue;

                        var got = Display(text, surface, reader, snapshot);
                        if (got == null) continue;

                        var lineTrace = LineTrace(got,
                            Display(WithoutTheirLines(text, cut, keepEmptyBlocks: true), surface, reader, snapshot),
                            Display(WithoutTheirLines(text, cut, keepEmptyBlocks: false), surface, reader, snapshot),
                            $"{reader} on {surface}");
                        if (lineTrace != null) return lineTrace;
                    }

                    var all = Unread(reading, _ => false);

                    var plain = Plain(text, surface);
                    var plainLines = plain == null
                        ? null
                        : LineTrace(plain, Plain(WithoutTheirLines(text, all, keepEmptyBlocks: true), surface),
                            Plain(WithoutTheirLines(text, all, keepEmptyBlocks: false), surface), $"the plain text of {surface}");
                    if (plainLines != null) return plainLines;

                    var embed = Embed(text, surface);
                    var embedLines = embed == null
                        ? null
                        : LineTrace(embed, Embed(WithoutTheirLines(text, all, keepEmptyBlocks: true), surface),
                            Embed(WithoutTheirLines(text, all, keepEmptyBlocks: false), surface), $"embed of {surface}");
                    if (embedLines != null) return embedLines;
                }

                return null;
            });

    /// <summary>
    /// The blocks as spelled, each on a line of its own among plain words, laid out one
    /// of four ways by the length of the text: words around every block, the blocks one
    /// after another between two words, the first block at the start of the text, the
    /// last one at its end.
    /// </summary>
    private static string OnLinesOfTheirOwn(string text, IReadOnlyList<HiddenBlock> blocks)
    {
        var spelled = blocks.OrderBy(block => block.Start)
            .Select(block => text[block.Start..block.End])
            .ToList();

        return (text.Length % 4) switch
        {
            0 => "Утро.\n" + string.Join("\nТуман.\n", spelled) + "\nВечер.",
            1 => "Утро.\n" + string.Join("\n", spelled) + "\nВечер.",
            2 => string.Join("\nТуман.\n", spelled) + "\nВечер.",
            _ => "Утро.\n" + string.Join("\nТуман.\n", spelled),
        };
    }

    /// <summary>
    /// What is wrong with what a reader got, against the same text with every line
    /// that held nothing but blocks they do not read taken out, in either spelling
    /// of <see cref="WithoutTheirLines" />.
    /// </summary>
    private static string? LineTrace(string got, string? withEmptyBlocks, string? withoutBlocks, string who) =>
        withEmptyBlocks == null || withoutBlocks == null || got == withEmptyBlocks || got == withoutBlocks
            ? null
            : $"{who} gets the line a block they do not read stood alone on: {MarkupLiteral.Of(got)} " +
              $"against {MarkupLiteral.Of(withoutBlocks)} without that line";

    /// <summary>
    /// The text as if the author had not typed the lines that hold nothing but the
    /// blocks: for each such line one line break taken out - the one before it while
    /// it is there, else the one after - and every other block replaced by an empty
    /// bare one.
    /// </summary>
    /// <remarks>
    /// The lines are read over the whole text, and a break inside a block is none:
    /// the block is one piece of whatever line it stands on.
    ///
    /// The blocks of a line taken out are spelled two ways, and a reader is right to
    /// get either. Kept as empty blocks, they stand glued to the line next to theirs,
    /// and a tag they were all of is still left out the way the renderer leaves it
    /// out, rather than turning into the empty tag the author never typed; but glued
    /// to an empty line they make a line of their own again, which the renderer then
    /// takes out as well. Taken out, they leave no such line, and a tag they were all
    /// of is left with its white space alone. Each spelling is the text without the
    /// line wherever the other one is not.
    /// </remarks>
    private static string WithoutTheirLines(string text, IReadOnlyList<HiddenBlock> blocks, bool keepEmptyBlocks)
    {
        bool InBlock(int at) => blocks.Any(block => block.Start <= at && at < block.End);

        // The line breaks outside the blocks, as (index, length), and for each line
        // whether it holds anything outside them and whether it holds a block.
        var breaks = new List<(int At, int Length)>();
        var content = new List<bool> { false };
        var held = new List<bool> { false };
        for (var at = 0; at < text.Length; at++)
        {
            if (InBlock(at))
            {
                held[^1] = true;
                continue;
            }

            var length = text[at] == '\n' ? 1 : text[at] == '\r' && at + 1 < text.Length && text[at + 1] == '\n' ? 2 : 0;
            if (length == 0)
            {
                content[^1] = true;
                continue;
            }

            breaks.Add((at, length));
            content.Add(false);
            held.Add(false);
            at += length - 1;
        }

        var dropped = new bool[breaks.Count];
        for (var line = 0; line < content.Count; line++)
        {
            if (content[line] || !held[line]) continue;
            if (line - 1 >= 0 && !dropped[line - 1]) dropped[line - 1] = true;
            else if (line < breaks.Count && !dropped[line]) dropped[line] = true;
        }

        // The line each block stands on, to know whether that line goes.
        var lineOf = new int[text.Length + 1];
        for (int at = 0, line = 0, next = 0; at <= text.Length; at++)
        {
            while (next < breaks.Count && breaks[next].At + breaks[next].Length <= at)
            {
                next++;
                line++;
            }

            lineOf[at] = line;
        }

        bool LineGoes(int line) => !content[line] && held[line] &&
                                   ((line - 1 >= 0 && dropped[line - 1]) || (line < breaks.Count && dropped[line]));

        var cuts = blocks
            .Select(block => (block.Start, block.End,
                Replacement: keepEmptyBlocks || !LineGoes(lineOf[block.Start]) ? EmptyBlock : ""))
            .Concat(breaks.Where((_, index) => dropped[index])
                .Select(lineBreak => (Start: lineBreak.At, End: lineBreak.At + lineBreak.Length, Replacement: "")))
            .OrderBy(cut => cut.Start);

        var output = new System.Text.StringBuilder();
        var from = 0;
        foreach (var (start, end, replacement) in cuts)
        {
            output.Append(text, from, start - from).Append(replacement);
            from = end;
        }

        return output.Append(text, from, text.Length - from).ToString();
    }

    /// <summary>What an erased block is replaced by to check that its content left nothing.</summary>
    private const string EmptyBlock = "[private][/private]";

    /// <summary>What it is replaced by to check that the tags around it left nothing: a character, no letter.</summary>
    private const string Stand = "0";

    /// <summary>What is wrong with what a reader got, against the same text with the blocks replaced.</summary>
    private static string? Trace(string got, string? withEmptyBlocks, string? withCharacters, string who)
    {
        if (got != withEmptyBlocks)
        {
            return $"{who} gets something of a block they do not read: {MarkupLiteral.Of(got)} " +
                   $"against {MarkupLiteral.Of(withEmptyBlocks ?? "(nothing)")} with the blocks empty";
        }

        if (withCharacters != null && EmptyElement.Matches(got).Count > EmptyElement.Matches(withCharacters).Count)
        {
            return $"{who} gets an empty tag left by a block they do not read: {MarkupLiteral.Of(got)} " +
                   $"against {MarkupLiteral.Of(withCharacters)} with the blocks as text";
        }

        return null;
    }

    /// <summary>The outermost blocks the predicate does not read, in the order they stand.</summary>
    private static List<HiddenBlock> Unread(HiddenTextReading reading, Func<HiddenBlock, bool> reads)
    {
        var cut = new List<HiddenBlock>();
        foreach (var block in reading.Blocks)
        {
            if (reads(block) || cut.Any(outer => outer.Start <= block.Start && block.End <= outer.End)) continue;
            cut.Add(block);
        }

        return cut;
    }

    /// <summary>The text with each of the blocks, which do not overlap, replaced.</summary>
    private static string Replace(string text, IReadOnlyList<HiddenBlock> blocks, string replacement)
    {
        var output = new System.Text.StringBuilder();
        var from = 0;
        foreach (var block in blocks.OrderBy(block => block.Start))
        {
            output.Append(text, from, block.Start - from).Append(replacement);
            from = block.End;
        }

        return output.Append(text, from, text.Length - from).ToString();
    }

    private static string? Plain(string input, BbSurface surface) =>
        Render(() => Wrapper(Provider.GetForSurface(surface))
            .RenderText(input, RenderContext.ForPlainText() with { Surface = surface }));

    private static string? Embed(string input, BbSurface surface) =>
        Render(() => Wrapper(Provider.GetSafeForSurface(surface))
            .RenderHtml(input, RenderContext.ForEmbedSafe(surface)));

    /// <summary>
    /// The save path refuses exactly the texts the rule refuses: on a game post a
    /// block spelled wrong, left open, inside another, a closing tag with nothing
    /// to close and hiding markup after a [noparse] left open; anywhere else any
    /// hiding markup outside a closed [noparse].
    /// </summary>
    [Fact]
    public void RefuseOnSaveExactlyWhatTheRuleRefuses() =>
        MarkupProperty.Check(nameof(RefuseOnSaveExactlyWhatTheRuleRefuses), runs: 1500,
            random => random.Next(4) == 0
                ? MarkupGenerator.Valid(random, random.Pick(Surfaces))
                : MarkupGenerator.Soup(random),
            input =>
            {
                var reading = HiddenTextOracle.Read(input);

                var balanced = PrivateBlockMarkup.IsBalanced(input);
                if (balanced == (reading.GamePostRefusal != null))
                {
                    return balanced
                        ? $"the save path of a game post takes it, and the rule refuses it: {reading.GamePostRefusal}"
                        : "the save path of a game post refuses it, and the rule takes it: " +
                          PrivateBlockMarkup.DescribeBalanceRefusal(input);
                }

                if (!balanced && !Regex.IsMatch(PrivateBlockMarkup.DescribeBalanceRefusal(input), @"символ \d+"))
                    return "the refusal does not name the place: " + PrivateBlockMarkup.DescribeBalanceRefusal(input);

                var hiding = PrivateBlockMarkup.ContainsPrivateMarkup(input);
                if (hiding != reading.HidingMarkupAt >= 0)
                {
                    return hiding
                        ? "the save path of a surface without [private] refuses it, and the rule finds no hiding markup"
                        : $"the save path of a surface without [private] takes it, and the rule finds hiding markup at {reading.HidingMarkupAt}";
                }

                return null;
            });

    /// <summary>
    /// The reference reading agrees with the grammar table on every refusal the
    /// table states, so the checks above are held to the rule and not to a
    /// reading of it nobody checked.
    /// </summary>
    [Theory]
    [MemberData(nameof(BbGrammarShould.DisplayRows), MemberType = typeof(BbGrammarShould))]
    public void ReadTheGrammarTableAsTheTableSays(string id)
    {
        var row = BbGrammarTable.Row(id);
        var reading = HiddenTextOracle.Read(row.Input);

        foreach (var surface in row.Surfaces)
        {
            reading.IsRefusedOn(surface).Should().Be(row.SaveRefused,
                $"the table says {row.Input} is {(row.SaveRefused ? "refused" : "taken")} on {surface}");
        }
    }

    // ════════════════════════════════════════════════════════════════
    // What a reader got
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// What is wrong with what one reader got, or null. A null reader is a
    /// channel that reads no block: the plain text and the embed.
    /// </summary>
    private static string? Misread(HiddenTextReading reading, string? output, HiddenTextReader? reader,
        BbSurface surface, string who)
    {
        // The renderer refused the text as too deep: the reader gets nothing,
        // which keeps every block, and loses nothing it could be blamed for.
        if (output == null) return null;

        bool Reads(HiddenTextWord word) => reader is { } kind
            ? word.IsReadBy(kind, surface)
            : word.Blocks.Count == 0;

        foreach (var word in reading.Words)
        {
            var got = output.Contains(word.Word, StringComparison.Ordinal);
            if (got && !Reads(word))
                return $"{who} reads {word.Word}, which a block keeps from them: {MarkupLiteral.Of(output)}";

            if (!got && Reads(word) && !word.InOpeningTag)
                return $"{who} does not get {word.Word}, which nothing keeps from them: {MarkupLiteral.Of(output)}";
        }

        var blocksShown = Regex.Matches(output, Regex.Escape(BlockElement)).Count;
        var blocksRead = reader is { } someone
            ? reading.Blocks.Count(block => reading.Blocks
                .Where(outer => outer.Start <= block.Start && block.End <= outer.End)
                .All(outer => outer.IsReadBy(someone, surface)))
            : 0;
        if (blocksShown != blocksRead)
            return $"{who} gets {blocksShown} hidden blocks and reads {blocksRead}: {MarkupLiteral.Of(output)}";

        if (blocksRead == 0 && output.Contains("DungeonMaster", StringComparison.Ordinal))
            return $"{who} reads no block and gets the line of its recipients: {MarkupLiteral.Of(output)}";

        return null;
    }

    private static string? Display(string input, BbSurface surface, HiddenTextReader reader,
        PrivateAddresseeSnapshotView snapshot)
    {
        var viewer = reader switch
        {
            HiddenTextReader.Author => AuthorId,
            HiddenTextReader.Lead => LeadId,
            HiddenTextReader.Addressee => AddresseeId,
            _ => StrangerId
        };

        var context = new RenderContext
        {
            Audience = RenderAudience.Display,
            Surface = surface,
            Viewer = new TestSubject
            {
                UserId = viewer,
                Role = UserRole.RegularUser,
                IsAuthenticated = true,
                AccessPolicy = AccessPolicy.NotSpecified
            },
            PostAuthorUserId = AuthorId,
            GameLeadUserIds = [LeadId],
            PrivateAddresseeOwnerUserIdsByAttribute = snapshot.OwnerUserIdsByAttribute,
            PrivateAddresseeNamesByAttribute = snapshot.AddresseeNamesByAttribute,
            RoomViewPrivateText = reader == HiddenTextReader.RoomOverride,
            PostSharePrivateWithAll = reader == HiddenTextReader.PostOverride
        };

        return Render(() => Wrapper(Provider.GetForSurface(surface)).RenderHtml(input, context));
    }

    /// <summary>The render, or null where the page shows nothing: a tree too deep to build.</summary>
    private static string? Render(Func<string> render)
    {
        try
        {
            return render();
        }
        catch (BbParserException)
        {
            return null;
        }
    }

    private static BbParserWrapper Wrapper(IBbParser parser) => (BbParserWrapper)parser;

    /// <summary>The snapshot the save path builds from the text, for the room of "Вася" and "Петя".</summary>
    private static PrivateAddresseeSnapshotView Snapshot(string input) =>
        PrivateAddresseeSnapshot.Read(PrivateAddresseeSnapshot.Build(input, Roster));
}
