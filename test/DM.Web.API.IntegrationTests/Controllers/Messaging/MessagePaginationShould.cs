using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Messaging.Features.Messages;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Messaging;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbMessage = DM.Infrastructure.Persistence.Entities.Messaging.Message;

namespace DM.Web.API.IntegrationTests.Controllers.Messaging;

/// <summary>
/// Messages are paged by the total order (CreatedUtc, MessageId). Ordered by
/// CreatedUtc alone the page boundary — which is exactly what becomes the cursor —
/// is whichever row the plan happened to return, and the cursor predicate used
/// "!= id", which is not an ordering: every message sharing the boundary timestamp
/// qualified for the next page regardless of which side of the cursor it sat on.
///
/// Same-instant messages are routine for bulk-imported data and reachable in a
/// busy chat, so scrollback duplicated and skipped rows.
/// </summary>
public class MessagePaginationShould : IntegrationTestBase
{
    private const int SameInstantCount = 7;
    private const int PageSize = 2;

    public MessagePaginationShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task WalkAChatOfSameInstantMessagesWithoutRepeatingOrSkipping()
    {
        var (chatId, expected) = await CreateChat();
        try
        {
            var seen = await WalkBackwards(chatId);

            seen.Should().OnlyHaveUniqueItems();
            seen.Should().BeEquivalentTo(expected);
        }
        finally
        {
            await DropChat(chatId);
        }
    }

    /// <summary>
    /// The window around a message used strict timestamp comparison on both sides,
    /// so every message sharing the target's timestamp fell out of both halves and
    /// disappeared from the result entirely.
    /// </summary>
    [Fact]
    public async Task KeepSameInstantNeighboursInTheWindowAroundAMessage()
    {
        var (chatId, expected) = await CreateChat();
        try
        {
            var repository = Repository(out var scope);
            using (scope)
            {
                var around = await repository.GetWithCursor(chatId,
                    new CursorQuery { AroundEntityId = expected[SameInstantCount / 2], Limit = 100 });

                around.Data.Select(m => m.Id).Should().BeEquivalentTo(expected);
            }
        }
        finally
        {
            await DropChat(chatId);
        }
    }

    /// <summary>
    /// The anchor takes one of the requested slots. Two halves of limit / 2 plus the
    /// anchor returned limit + 1 for every even limit, so the default page was 51
    /// messages and a request for the documented maximum returned 101.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public async Task NotExceedTheRequestedWindowSize(int limit)
    {
        var (chatId, expected) = await CreateChat();
        try
        {
            var repository = Repository(out var scope);
            using (scope)
            {
                var around = await repository.GetWithCursor(chatId,
                    new CursorQuery { AroundEntityId = expected[SameInstantCount / 2], Limit = limit });

                around.Data.Should().HaveCount(limit);
                around.Data.Select(m => m.Id).Should().Contain(expected[SameInstantCount / 2]);
            }
        }
        finally
        {
            await DropChat(chatId);
        }
    }

    private async Task<List<Guid>> WalkBackwards(Guid chatId)
    {
        var seen = new List<Guid>();
        string? cursor = null;

        // One extra iteration over the strict minimum, so a walk that fails to
        // terminate is caught by the assertions rather than by a hang.
        for (var page = 0; page <= SameInstantCount / PageSize + 1; page++)
        {
            var repository = Repository(out var scope);
            using (scope)
            {
                var result = await repository.GetWithCursor(chatId,
                    new CursorQuery { Cursor = cursor, Limit = PageSize });

                seen.AddRange(result.Data.Select(m => m.Id));
                cursor = result.PrevCursor;
                if (cursor == null)
                {
                    break;
                }
            }
        }

        return seen;
    }

    private IMessageRepository Repository(out IServiceScope scope)
    {
        scope = DatabaseFixture.Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IMessageRepository>();
    }

    /// <summary>
    /// Creates a chat whose messages all carry one timestamp, oldest first in the
    /// total order — the state the ordering is supposed to make deterministic.
    /// </summary>
    private async Task<(Guid ChatId, List<Guid> MessageIds)> CreateChat()
    {
        var chatId = Guid.NewGuid();
        var sameInstant = new DateTimeOffset(2026, 3, 14, 12, 0, 0, TimeSpan.Zero);

        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        dbContext.Chats.Add(new Chat
        {
            ChatId = chatId,
            Type = ChatType.Group,
            Title = $"Pagination chat {chatId:N}",
        });
        dbContext.Set<UserChatLink>().Add(new UserChatLink
        {
            UserChatLinkId = Guid.NewGuid(),
            ChatId = chatId,
            UserId = TestConstants.TestUserId,
        });

        var messageIds = Enumerable.Range(0, SameInstantCount).Select(_ => Guid.NewGuid()).ToList();
        foreach (var messageId in messageIds)
        {
            dbContext.Messages.Add(new DbMessage
            {
                MessageId = messageId,
                ChatId = chatId,
                UserId = TestConstants.TestUserId,
                CreatedUtc = sameInstant,
                Text = messageId.ToString(),
            });
        }

        await dbContext.SaveChangesAsync();

        // The expected reading order is the one the database defines for uuid,
        // which is byte order and matches Guid.CompareTo exactly.
        messageIds.Sort();
        return (chatId, messageIds);
    }

    private async Task DropChat(Guid chatId)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync(
            """UPDATE "Chats" SET "LastMessageId" = NULL WHERE "ChatId" = {0}""", chatId);
        await dbContext.Database.ExecuteSqlRawAsync(
            """DELETE FROM "Messages" WHERE "ChatId" = {0}""", chatId);
        await dbContext.Database.ExecuteSqlRawAsync(
            """DELETE FROM "UserChatLinks" WHERE "ChatId" = {0}""", chatId);
        await dbContext.Database.ExecuteSqlRawAsync(
            """DELETE FROM "Chats" WHERE "ChatId" = {0}""", chatId);
    }
}
