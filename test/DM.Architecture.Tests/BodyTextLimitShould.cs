using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using DM.Domain.Core.Content;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The composers count against the same number the save refuses at.
/// </summary>
/// <remarks>
/// The limit is the server's, and the browser cannot ask for it, so the client
/// keeps a copy. Two copies of a number are two numbers as soon as one of them
/// moves, and the direction that hurts is the client's being the larger one: the
/// author writes a body the counter calls acceptable and the save refuses it.
///
/// It matters most for the Quote action, which is why the check arrives with it.
/// A quotation of a long message lands in the draft in one press, so the moment
/// the counter turns red is the moment the author still has a choice about what
/// to cut; without the counter that moment is the refusal after the answer is
/// written.
///
/// Read out of the sources, because the client cannot be asked from here at all.
/// </remarks>
public class BodyTextLimitShould
{
    private static readonly Regex ClientLimit =
        new(@"BODY_TEXT_MAX_LENGTH\s*=\s*(\d+)", RegexOptions.Compiled);

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;

    [Fact]
    public void BeTheSameNumberOnBothSides()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot,
            "src", "DM.Web.Client", "src", "shared", "lib", "constants", "content.ts"));

        var match = ClientLimit.Match(source);
        match.Success.Should().BeTrue("the client keeps its copy of the limit in content.ts");

        int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)
            .Should().Be(BodyTextLimits.MaxLength,
                "a composer that counts against a different number than the save refuses at " +
                "either forbids what would have been accepted or accepts what will be refused");
    }
}
