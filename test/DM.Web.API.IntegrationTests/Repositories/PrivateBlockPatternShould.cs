using System.Text.RegularExpressions;
using DM.Domain.Core.Content;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <remarks>
/// The stored tsvector, the room filter and the snippet share it, two in Postgres and one in .NET; Postgres takes a
/// whole expression's greediness from its first quantifier. Against the container Postgres.
/// </remarks>
public class PrivateBlockPatternShould : IntegrationTestBase
{
    public PrivateBlockPatternShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private static readonly Regex DotNet = new(PrivateBlockMarkup.BlockPattern, RegexOptions.Compiled);

    [Theory]
    // One block, the ordinary case.
    [InlineData("до [private]тайна[/private] после", "до   после")]
    // Two blocks: what stands between them is public and stays.
    [InlineData("A [private]x[/private] СЕРЕДИНА [private]y[/private] B", "A   СЕРЕДИНА   B")]
    // Addressees are part of the tag rather than of the text, and so is every
    // spelling the renderer reads as a hidden block.
    [InlineData("[private=\"Вася, Петя\"]x[/private] хвост", "  хвост")]
    [InlineData("[private=Вася]x[/private] хвост", "  хвост")]
    [InlineData("[private to=\"Вася\"]x[/private] хвост", "  хвост")]
    // The opening and the closing tag in any casing, as on the page.
    [InlineData("до [PRIVATE]тайна[/private] после", "до   после")]
    [InlineData("до [private]тайна[/PRIVATE] после", "до   после")]
    [InlineData("до [Private]тайна[/pRiVaTe] после", "до   после")]
    // A block nobody closed runs to the end of the text, as on the page.
    [InlineData("до [private]без закрытия", "до  ")]
    [InlineData("до [private", "до  ")]
    // A line break at the very end is where the two engines read an end anchor
    // differently; the pattern has none, so it is cut the same way.
    [InlineData("до [private]тайна\n", "до  ")]
    // A word that starts like the tag is a word.
    [InlineData("[privateer] и [private_x]", "[privateer] и [private_x]")]
    public async Task CutTheSameTextAsTheDotNetSideDoes(string input, string expected)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var inPostgres = await dbContext.Database
            // The column has to be named Value: that is the shape SqlQuery reads a
            // scalar out of.
            .SqlQuery<string>($"select regexp_replace({input}, {PrivateBlockMarkup.BlockPattern}, ' ', {PrivateBlockMarkup.BlockPatternFlags}) as \"Value\"")
            .SingleAsync();

        inPostgres.Should().Be(expected);
        DotNet.Replace(input, " ").Should().Be(expected, "the two engines share the string");
    }
}
