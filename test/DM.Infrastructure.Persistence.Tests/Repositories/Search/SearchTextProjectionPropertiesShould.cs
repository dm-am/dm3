using System;
using System.Linq;
using System.Text.RegularExpressions;
using DM.Domain.Core.Content;
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
/// What is indexed is found by everybody who can search. Half of the texts start nested past the renderer,
/// which sends the projection down its fallback path.
/// </remarks>
public class SearchTextProjectionPropertiesShould
{
    private static readonly string TooDeep = string.Concat(Enumerable.Repeat("[quote]", 520));

    private static readonly Regex SecondLock = new(PrivateBlockMarkup.BlockPattern);

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
