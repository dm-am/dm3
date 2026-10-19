using DM.Domain.Community.Features.WebsiteTestimonials;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DM.Web.API.IntegrationTests.Helpers;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// The search of the testimonials reads "%" and "_" as themselves, as every
/// other search of the site does.
/// </summary>
public class WebsiteTestimonialSearchShould : IntegrationTestBase
{
    public WebsiteTestimonialSearchShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Theory]
    [InlineData("скидка 50%", "скидка 500 рублей")]
    [InlineData("код a_b", "код aXb")]
    public async Task FindTheSignsLiterally(string literal, string lookalike)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IWebsiteTestimonialRepository>();
        // The fixture database is shared: a marker of this run keeps others' rows out.
        var marker = Guid.NewGuid().ToString("N")[..8];

        await repository.Create(NewEntity(await AddUserAsync(dbContext), $"{marker} {literal}"));
        await repository.Create(NewEntity(await AddUserAsync(dbContext), $"{marker} {lookalike}"));

        (await repository.Count(new WebsiteTestimonialsQuery { Search = $"{marker} {literal}" }))
            .Should().Be(1, "the sign in the query is a character of the text, not a wildcard");
    }

    private static CreateWebsiteTestimonialEntity NewEntity(Guid authorId, string text) =>
        TestimonialEntities.New(authorId, text);

    private static Task<Guid> AddUserAsync(DmDbContext dbContext) => UserRows.AddAsync(dbContext, "ws");
}
