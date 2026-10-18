using System;
using System.Linq;
using System.Text.RegularExpressions;
using DM.Infrastructure.Core.Parsing;
using DM.Infrastructure.Persistence.Repositories.Search;
using DM.Testing.Markup;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Search;

/// <summary>
/// The search never indexes a word of a hidden block, on texts nobody wrote
/// down: generated from the shared alphabet (markupAlphabet.ts) and read against
/// the reference reading of hidden blocks (HiddenTextOracle).
/// </summary>
/// <remarks>
/// The search is the one reader who never has a right to a hidden block: what is
/// indexed is found by everybody who can search. So every word the rule places
/// inside a block has to be missing from the projection, on every surface, and
/// from what the second lock of the database leaves of it.
///
/// Half of the texts start nested deeper than the renderer builds. The
/// projection then falls back to cutting blocks out of the raw text by the
/// pattern of the second lock, which is a second reading of the markup, and that
/// path is otherwise never reached by a generated text.
/// </remarks>
public class SearchTextProjectionPropertiesShould
{
    private static readonly string TooDeep = string.Concat(Enumerable.Repeat("[quote]", 520));

    private static readonly Regex SecondLock = new(SearchSnippet.PrivateBlockPattern);

    [Fact]
    public void IndexNoWordOfAHiddenBlock() =>
        MarkupProperty.Check(nameof(IndexNoWordOfAHiddenBlock), runs: 300,
            random =>
            {
                var sample = MarkupGenerator.Soup(random);
                return random.Next(2) == 0 ? new MarkupSample([TooDeep, .. sample.Atoms]) : sample;
            },
            input =>
            {
                var hidden = HiddenTextOracle.Read(input).Words.Where(word => word.Blocks.Count > 0).ToArray();
                if (hidden.Length == 0) return null;

                // Every surface, the hint of a tag description included, which
                // nothing indexes today: it reads [private] and erases it too.
                foreach (var surface in Enum.GetValues<BbSurface>())
                {
                    var projected = SearchTextProjection.Of(input, surface);
                    var indexed = SecondLock.Replace(projected, " ");
                    var found = hidden.FirstOrDefault(word => indexed.Contains(word.Word, StringComparison.Ordinal));
                    if (found != null)
                        return $"the search of {surface} indexes {found.Word}: {MarkupLiteral.Of(projected)}";
                }

                return null;
            });
}
