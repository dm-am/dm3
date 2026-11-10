using DM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DM.Web.API.IntegrationTests.Helpers;

internal static class ChatRows
{
    public static async Task DropAsync(DmDbContext dbContext, Guid chatId)
    {
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
