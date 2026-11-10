using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using DM.Domain.Core.Content;
using DM.Domain.Moderation.Features.Tickets;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

// The client keeps a copy of the server's limit, read out of the sources here.
public class BodyTextLimitShould
{
    private static readonly Regex ClientLimit =
        new(@"BODY_TEXT_MAX_LENGTH\s*=\s*(\d+)", RegexOptions.Compiled);

    private static readonly Regex ClientTicketLimit =
        new(@"TICKET_TEXT_MAX_LENGTH\s*=\s*(\d+)", RegexOptions.Compiled);


    [Fact]
    public void BeTheSameNumberOnBothSides()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryLayout.Root,
            "src", "DM.Web.Client", "src", "shared", "lib", "constants", "content.ts"));

        var match = ClientLimit.Match(source);
        match.Success.Should().BeTrue("the client keeps its copy of the limit in content.ts");

        int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)
            .Should().Be(BodyTextLimits.MaxLength,
                "a composer that counts against a different number than the save refuses at " +
                "either forbids what would have been accepted or accepts what will be refused");
    }

    /// <summary>
    /// The text of a ticket and of a comment in its conversation has a limit of its
    /// own, shorter than a comment's, and the form of a ticket counts against the
    /// client's copy of it.
    /// </summary>
    [Fact]
    public void BeTheSameNumberOnBothSidesForATicket()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryLayout.Root,
            "src", "DM.Web.Client", "src", "entities", "ticket", "lib", "limits.ts"));

        var match = ClientTicketLimit.Match(source);
        match.Success.Should().BeTrue("the client keeps its copy of the limit of a ticket in limits.ts");

        int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)
            .Should().Be(TicketTextLimits.MaxLength,
                "the form of a ticket that counts against a different number than the save refuses " +
                "at either forbids what would have been accepted or accepts what will be refused");
    }
}
