using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using AwesomeAssertions;
using BBCodeParser;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Content;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Parsing;
using DM.Testing;
using DM.Testing.Dsl;
using DM.Testing.Markup;
using Xunit;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// The corpus the client is held to is what the server renders today.
/// </summary>
/// <remarks>
/// <para>
/// The editor draws its preview and reads the author's markup back by a
/// converter of its own, and the only way to know it agrees with the server on
/// texts nobody wrote down is to show it what the server did with them. So the
/// server writes, for a fixed set of generated texts, what the author of each is
/// shown, what the author's editor is given back, and whether the save path takes
/// the text, into markupCorpus.golden.json in the client tree; the client's
/// bbcodeProperties.spec.ts reads the file and compares its own answers.
/// </para>
/// <para>
/// This test keeps the file honest: it writes the corpus again in memory and
/// fails when the file says something else. A change to the renderer, the save
/// path, the alphabet or the generator therefore turns this red until the file is
/// written again (DM_MARKUP_CORPUS=write), and the client is then checked against
/// the new answers. docs/guides/TESTING.md, section "Генеративные проверки
/// разметки", has the commands.
/// </para>
/// </remarks>
public class BbMarkupCorpusShould
{
    /// <summary>How many texts the corpus holds.</summary>
    private const int Size = 240;

    private static readonly Guid AuthorId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly IBbParserProvider Provider = new BbParserProvider();

    /// <summary>Where the corpus is written.</summary>
    internal static string File => Path.Combine(RepositoryLayout.Root,
        "src", "DM.Web.Client", "src", "shared", "lib", "testing", "markupCorpus.golden.json");

    [Fact]
    public void BeWhatTheServerAnswersToday()
    {
        var target = Environment.GetEnvironmentVariable("DM_MARKUP_CORPUS");

        // A search beyond the corpus: another one, of the size DM_MARKUP_RUNS
        // asks for, from seeds DM_MARKUP_SALT mixes, written where the variable
        // points and compared with nothing. The client reads it from there when
        // pointed at it the same way.
        if (target is { Length: > 0 } and not "write")
        {
            var runs = int.TryParse(Environment.GetEnvironmentVariable("DM_MARKUP_RUNS"), out var told) ? told : Size;
            System.IO.File.WriteAllText(target, Write(runs, Environment.GetEnvironmentVariable("DM_MARKUP_SALT")),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return;
        }

        var written = Write(Size, salt: null);

        if (target == "write")
        {
            System.IO.File.WriteAllText(File, written, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        var stored = System.IO.File.Exists(File)
            ? System.IO.File.ReadAllText(File).Replace("\r\n", "\n", StringComparison.Ordinal)
            : string.Empty;

        stored.Should().Be(written,
            "markupCorpus.golden.json is what the client's preview, its reverse conversion and its markup " +
            "check are compared with, and it no longer says what the server answers. Write it again with " +
            "DM_MARKUP_CORPUS=write dotnet test test/DM.Infrastructure.Core.Tests --filter BbMarkupCorpusShould, " +
            "then run the client's bbcodeProperties.spec.ts against it");
    }

    /// <summary>The corpus as the server answers it now, one text a line.</summary>
    private static string Write(int size, string? salt)
    {
        var baseSeed = salt == null ? 0xC0FFEEUL : 0xC0FFEEUL ^ MarkupRandom.SeedOf(salt);
        var output = new StringBuilder()
            .Append("{\"about\":")
            .Append(MarkupLiteral.Of("Written by BbMarkupCorpusShould; do not edit by hand. " +
                                     "See docs/guides/TESTING.md, section on generated markup checks."))
            .Append(",\n\"entries\":[\n");

        // The surfaces of content, in their order: the corpus is post text,
        // and the client reads its surface as one of the five editor contexts.
        var surfaces = MarkupSurfaces.Content;
        for (var index = 0; index < size; index++)
        {
            var random = new MarkupRandom(MarkupRandom.CaseSeed(baseSeed, index));
            var surface = surfaces[index % surfaces.Length];
            var valid = index % 4 == 0;
            var sample = valid
                ? MarkupGenerator.Valid(random, surface)
                : MarkupGenerator.Soup(random, hiding: surface == BbSurface.GamePost || index % 3 == 0);

            output.Append(index == 0 ? "" : ",\n").Append(Entry(sample.Text, surface, valid));
        }

        // Every address of the alphabet, alone between the halves of each tag
        // that takes one: which of them the page draws. The editor reads an
        // address by the URL parser of the browser and the server by Uri, and
        // the two take different shapes; this is where the client is told which
        // ones the server takes.
        output.Append("\n],\n\"addresses\":[\n");
        var addresses = MarkupAlphabet.Shared.Addresses;
        var wrapper = (BbParserWrapper)Provider.GetForSurface(BbSurface.Comment);
        var reader = RenderContext.ForPlainText() with
        {
            Audience = RenderAudience.Display,
            Surface = BbSurface.Comment
        };
        for (var index = 0; index < addresses.Count; index++)
        {
            var address = addresses[index];
            bool Drawn(string tag, string element) =>
                wrapper.RenderHtml($"[{tag}]{address}[/{tag}]", reader).StartsWith(element, StringComparison.Ordinal);

            output.Append(index == 0 ? "" : ",\n")
                .Append("{\"address\":").Append(MarkupLiteral.Of(address))
                .Append(",\"link\":").Append(Drawn("link", "<a href=") ? "true" : "false")
                .Append(",\"img\":").Append(Drawn("img", "<img ") ? "true" : "false")
                .Append(",\"mention\":").Append(Drawn("mention", "<a class=\"bb-mention\"") ? "true" : "false")
                .Append('}');
        }

        return output.Append("\n]}\n").ToString();
    }

    /// <summary>
    /// One text: its surface, whether it was generated as valid markup, and the
    /// server's three answers about it.
    /// </summary>
    private static string Entry(string input, BbSurface surface, bool valid)
    {
        var author = new TestSubject
        {
            UserId = AuthorId,
            Role = UserRole.RegularUser,
            IsAuthenticated = true,
            AccessPolicy = AccessPolicy.NotSpecified
        };

        // What the author reads on the page: every block of a game post, with the
        // line under it naming what the tag names (no snapshot is consulted).
        var display = Render(() => ((BbParserWrapper)Provider.GetForSurface(surface)).RenderHtml(input,
            new RenderContext
            {
                Audience = RenderAudience.Display,
                Surface = surface,
                Viewer = author,
                PostAuthorUserId = AuthorId
            }));

        // What the author's editor is given back. A text too deep to render is
        // given back as it was typed, as BbConverter gives it.
        var authorEdit = Render(() => ((BbParserWrapper)Provider.GetForAuthorEdit(surface))
            .RenderHtml(input, RenderContext.ForAuthorEdit(author, surface))) ?? WebUtility.HtmlEncode(input);

        var refusal = surface == BbSurface.GamePost
            ? PrivateBlockMarkup.IsBalanced(input) ? null : PrivateBlockMarkup.DescribeBalanceRefusal(input)
            : PrivateBlockMarkup.ContainsPrivateMarkup(input)
                ? PrivateBlockMarkup.DescribeSurfaceRefusal(input)
                : null;

        return new StringBuilder("{")
            .Append("\"surface\":").Append(MarkupLiteral.Of(surface.ToString()))
            .Append(",\"valid\":").Append(valid ? "true" : "false")
            .Append(",\"input\":").Append(MarkupLiteral.Of(input))
            .Append(",\"display\":").Append(display == null ? "null" : MarkupLiteral.Of(display))
            .Append(",\"authorEdit\":").Append(MarkupLiteral.Of(authorEdit))
            .Append(",\"refusal\":").Append(refusal == null ? "null" : MarkupLiteral.Of(refusal))
            .Append('}')
            .ToString();
    }

    /// <summary>The render, or null for a tree too deep to build.</summary>
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
}
