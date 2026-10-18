using System;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Messaging.Features.Chats;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// Two correspondents open the same conversation, whatever the database holds.
/// </summary>
/// <remarks>
/// A direct chat is opened by "look for one, create one if there is none", and
/// nothing in the schema stops a pair from owning two: two openings at once each
/// read no chat and each created one. The lookup used to take whichever row came
/// back first, and which one that is belongs to the planner - so the two sides
/// could settle on different rows of the pair and write past each other, each in
/// a conversation the other never opens.
///
/// Run against PostgreSQL because the claim is about what a query returns when
/// more than one row satisfies it, and that is the database's answer to give.
/// </remarks>
public class DirectChatLookupShould : IntegrationTestBase
{
    /// <summary>
    /// Serial numbers of the two rows, deliberately out of step with the order
    /// they are written in.
    /// </summary>
    /// <remarks>
    /// Written younger first, so the row a scan reaches first is not the row the
    /// order names. Far above the identity sequence, because the fixture database
    /// is shared and the column is unique by way of PublicId.
    /// </remarks>
    private const int ElderSerialNumber = 1_900_000;

    private const int YoungerSerialNumber = 1_900_001;

    public DirectChatLookupShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>
    /// Both sides of a pair that owns two conversations reach the same one.
    /// </summary>
    [Fact]
    public async Task ReturnTheOldestOfTheDuplicateConversationsOfOnePair()
    {
        Guid oneId;
        Guid otherId;
        Guid elder;
        await using (var dbContext = DatabaseFixture.CreateDbContext())
        {
            oneId = await AddUserAsync(dbContext);
            otherId = await AddUserAsync(dbContext);
            // Younger first: a scan of the table reaches this one before the row
            // the order names, so an unordered lookup answers with it.
            await AddDirectChatAsync(dbContext, Guid.NewGuid(), YoungerSerialNumber, oneId, otherId);
            elder = Guid.NewGuid();
            await AddDirectChatAsync(dbContext, elder, ElderSerialNumber, oneId, otherId);
        }

        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IChatRepository>();

        var found = await repository.FindDirectChat(oneId, otherId);
        var foundFromTheOtherSide = await repository.FindDirectChat(otherId, oneId);

        found!.Id.Should().Be(elder,
            "the conversation the pair already has is the one both of them must be given, " +
            "and which of two rows a query without an order returns is the planner's choice");
        foundFromTheOtherSide!.Id.Should().Be(found.Id,
            "the two correspondents are looking for the same conversation, and naming " +
            "themselves in the other order does not make it a different one");
    }

    /// <summary>
    /// A user of this test's own: the fixture database is shared and seeded, so a
    /// fixed identifier would depend on whoever else wrote one.
    /// </summary>
    private static async Task<Guid> AddUserAsync(DmDbContext dbContext)
    {
        var userId = Guid.NewGuid();
        dbContext.Users.Add(new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed, so the id is
            // truncated rather than used whole.
            Username = $"dcl{userId:N}"[..20],
            Email = $"{userId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
        });
        await dbContext.SaveChangesAsync();
        return userId;
    }

    /// <summary>
    /// A direct chat of two, written with the serial number it is to carry.
    /// </summary>
    /// <remarks>
    /// Raw SQL because the column is an identity one: the repository takes its
    /// value from the sequence, and this test is about rows whose order in the
    /// table is not the order of their serial numbers.
    /// </remarks>
    private static async Task AddDirectChatAsync(
        DmDbContext dbContext, Guid chatId, int serialNumber, Guid oneId, Guid otherId)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "Chats" ("ChatId", "SerialNumber", "PublicId", "Type", "Title")
            VALUES ({0}, {1}, {2}, {3}, '')
            """,
            chatId, serialNumber, $"d{chatId:N}"[..10], (int)ChatType.Direct);

        foreach (var userId in new[] { oneId, otherId })
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO "UserChatLinks" ("UserChatLinkId", "ChatId", "UserId", "IsRemoved")
                VALUES ({0}, {1}, {2}, false)
                """,
                Guid.NewGuid(), chatId, userId);
        }
    }
}
