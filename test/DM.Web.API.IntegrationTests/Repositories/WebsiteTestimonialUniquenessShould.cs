using DM.Domain.Community.Features.WebsiteTestimonials;
using DM.Domain.Core.Exceptions;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;
using DM.Web.API.IntegrationTests.Helpers;

namespace DM.Web.API.IntegrationTests.Repositories;

// Creating is check-then-insert, so the partial unique index by author decides, and the repository
// answers in the domain's terms.
public class WebsiteTestimonialUniquenessShould : IntegrationTestBase
{
    public WebsiteTestimonialUniquenessShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task RefuseASecondTestimonialForOneParticipant()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IWebsiteTestimonialRepository>();
        var authorId = await AddUserAsync(dbContext);

        await repository.Create(NewEntity(authorId, "Первый отзыв"));

        // Another entry identifier, the same participant: the insert the loser of
        // the race issues after its own check came back empty.
        Func<Task> second = () => repository.Create(NewEntity(authorId, "Второй отзыв"));

        var refusal = await second.Should().ThrowAsync<DuplicateEntityException>(
            "one participant is signed under one entry, whatever identifier the row carries");
        refusal.And.InnerException.Should().BeOfType<DbUpdateException>()
            .Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be("23505",
                "the refusal is the index answering the insert, not a read answering it");

        (await dbContext.WebsiteTestimonials.CountAsync(t => t.AuthorId == authorId))
            .Should().Be(1);
    }

    // A removed entry frees the participant: the index is filtered by IsRemoved, and a moderator
    // who deleted a testimonial can accept a new one.
    [Fact]
    public async Task AcceptATestimonialAfterThePreviousOneWasDeleted()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IWebsiteTestimonialRepository>();
        var authorId = await AddUserAsync(dbContext);
        var moderatorId = await AddUserAsync(dbContext);

        var first = await repository.Create(NewEntity(authorId, "Первый отзыв"));
        await repository.Delete(first.Id, moderatorId);

        Func<Task> again = () => repository.Create(NewEntity(authorId, "Отзыв после удаления"));

        await again.Should().NotThrowAsync();
        (await dbContext.WebsiteTestimonials.CountAsync(t => t.AuthorId == authorId && !t.IsRemoved))
            .Should().Be(1);
    }

    private static CreateWebsiteTestimonialEntity NewEntity(Guid authorId, string text) =>
        TestimonialEntities.New(authorId, text);

    // A participant of this test's own: the fixture database is shared and seeded, so a fixed
    // identifier would depend on whoever else wrote one.
    private static Task<Guid> AddUserAsync(DmDbContext dbContext) => UserRows.AddAsync(dbContext, "wt");
}
