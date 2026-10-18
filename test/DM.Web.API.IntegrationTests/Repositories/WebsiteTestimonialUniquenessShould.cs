using System;
using System.Threading.Tasks;
using DM.Domain.Community.Features.WebsiteTestimonials;
using DM.Domain.Core.Exceptions;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// One participant is signed under one testimonial, and the refusal of a second
/// one comes from the schema rather than from a read.
/// </summary>
/// <remarks>
/// Creating is check-then-insert - the service asks whether the named
/// participant already has an entry and inserts afterwards - and a check cannot
/// see the request running beside it. The partial unique index by author is the
/// only place the rule holds for rows that do not exist yet, so what is asserted
/// here is that it answers the insert and that the repository hands the domain
/// its own currency instead of a storage error the moderator reads as a broken
/// site.
/// </remarks>
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

    /// <summary>
    /// A removed entry frees the participant: the index is filtered by IsRemoved,
    /// and a moderator who deleted a testimonial can accept a new one.
    /// </summary>
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

    private static CreateWebsiteTestimonialEntity NewEntity(Guid authorId, string text) => new()
    {
        Id = Guid.NewGuid(),
        AuthorId = authorId,
        CreatedUtc = DateTime.UtcNow,
        Text = text
    };

    /// <summary>
    /// A participant of this test's own: the fixture database is shared and
    /// seeded, so a fixed identifier would depend on whoever else wrote one.
    /// </summary>
    private static async Task<Guid> AddUserAsync(DmDbContext dbContext)
    {
        var userId = Guid.NewGuid();
        dbContext.Users.Add(new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed, so the id is
            // truncated rather than used whole.
            Username = $"wt{userId:N}"[..20],
            Email = $"{userId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
        });
        await dbContext.SaveChangesAsync();
        return userId;
    }
}
