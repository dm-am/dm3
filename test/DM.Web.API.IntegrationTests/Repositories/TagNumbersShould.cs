using System;
using System.Threading.Tasks;
using DM.Domain.Moderation.Features.Tags;
using DM.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;
using DbTag = DM.Infrastructure.Persistence.Entities.Shared.Tag;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// The number a tag is addressed by is issued once and never issued again.
/// </summary>
/// <remarks>
/// ShortId is what a /games link is written in, and it was handed out as MAX + 1 over the
/// table. Two moderators creating a tag in the same moment read one maximum and both commit
/// it; and because a tag is deleted physically, the maximum walks backwards, so the number
/// of the tag just deleted is handed to the next one and the links written in it point
/// somewhere else. Both are properties of a sequence of writes rather than of a unit, and
/// what answers them — the sequence and the unique index — exists only in the schema, which
/// is why this runs against the container Postgres.
/// </remarks>
public class TagNumbersShould : IntegrationTestBase
{
    /// <summary>A group of the seeded catalogue, so a created tag has somewhere to live.</summary>
    private static readonly Guid SeededGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001");

    public TagNumbersShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task IssueANumberPastEveryNumberAlreadyTaken()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITagManagementRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var highestBefore = await dbContext.Tags.MaxAsync(t => t.ShortId);

        var created = await repository.CreateTag(NewTag());

        created.ShortId.Should().BeGreaterThan(highestBefore,
            "the sequence starts past the catalogue the site ships with, and the unique " +
            "index would have refused the insert otherwise");
    }

    [Fact]
    public async Task NotIssueTheNumberOfADeletedTagAgain()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITagManagementRepository>();

        var first = await repository.CreateTag(NewTag());
        var second = await repository.CreateTag(NewTag());
        second.ShortId.Should().BeGreaterThan(first.ShortId, "numbers are issued in order");

        await repository.DeleteTag(second.Id);
        var third = await repository.CreateTag(NewTag());

        third.ShortId.Should().BeGreaterThan(second.ShortId,
            "deleting the newest tag frees its row, not the number links are written in");
    }

    [Fact]
    public async Task RefuseASecondTagCarryingOneNumber()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var taken = await dbContext.Tags.MinAsync(t => t.ShortId);

        dbContext.Tags.Add(new DbTag
        {
            TagId = Guid.NewGuid(),
            ShortId = taken,
            TagGroupId = SeededGroupId,
            Title = $"tag-{Guid.NewGuid():N}"[..12],
            SortOrder = 0,
        });
        Func<Task> duplicate = () => dbContext.SaveChangesAsync();

        var refusal = await duplicate.Should().ThrowAsync<DbUpdateException>(
            "one number addresses one tag, whichever row carries it");
        refusal.And.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be("23505", "that is unique_violation");
    }

    /// <summary>A tag the number checks can create, under a group the seed ships with.</summary>
    private static CreateTag NewTag() => new()
    {
        GroupId = SeededGroupId,
        Title = $"tag-{Guid.NewGuid():N}"[..12],
        Description = "Written by the tag number checks",
        SortOrder = 0,
    };
}
