using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DM.Domain.Core.Content;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// The pattern that cuts a [private] block out means the same thing in
/// PostgreSQL and in .NET.
/// </summary>
/// <remarks>
/// Three paths share it: the stored tsvector a post is indexed by, the ILike
/// filter of the room search, and the snippet the search results show. Two of
/// them run in Postgres and one in .NET, and the two engines read the same string
/// differently — Postgres takes the greediness of a whole expression from its
/// first quantifier, so a greedy optional attribute made the lazy body greedy
/// too, and one substitution ate everything between the first opening tag and
/// the last closing one. Public text standing between two private blocks was
/// dropped from the index, from the filter and from the preview; the .NET copy of
/// the same string kept it. Nobody saw an error either way — the text was simply
/// unfindable.
///
/// Runs against the container Postgres, because the question is what that engine
/// does with the string rather than what the string looks like.
/// </remarks>
public class PrivateBlockPatternShould : IntegrationTestBase
{
    public PrivateBlockPatternShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private static readonly Regex DotNet = new(
        PrivateBlockMarkup.BlockPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [Theory]
    // One block, the ordinary case.
    [InlineData("до [private]тайна[/private] после", "до   после")]
    // Two blocks: what stands between them is public and stays.
    [InlineData("A [private]x[/private] СЕРЕДИНА [private]y[/private] B", "A   СЕРЕДИНА   B")]
    // Addressees, quoted and bare, are part of the tag rather than of the text.
    [InlineData("[private=Вася]x[/private] хвост", "  хвост")]
    [InlineData("[private=\"Вася, Петя\"]x[/private] хвост", "  хвост")]
    // Casing is the author's business.
    [InlineData("до [PRIVATE]тайна[/PRIVATE] после", "до   после")]
    // An unclosed tag is not a block: there is nothing to cut out, and what
    // follows it is indexed. This is the shape the save path has to refuse.
    [InlineData("до [private]без закрытия", "до [private]без закрытия")]
    public async Task CutTheSameTextAsTheDotNetSideDoes(string input, string expected)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var inPostgres = await dbContext.Database
            // The column has to be named Value: that is the shape SqlQuery reads a
            // scalar out of.
            .SqlQuery<string>($"select regexp_replace({input}, {PrivateBlockMarkup.BlockPattern}, ' ', 'gi') as \"Value\"")
            .SingleAsync();

        inPostgres.Should().Be(expected);
        DotNet.Replace(input, " ").Should().Be(expected, "the two engines share the string");
    }
}
