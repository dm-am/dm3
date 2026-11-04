using DM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DM.Web.API.IntegrationTests.Helpers;

/// <summary>
/// A private chat of a test's own, written straight to the database, and its removal.
/// </summary>
internal static class ChatRows
{
    /// <summary>Removes the chat with its messages and participants.</summary>
    /// <param name="dbContext">The context the statements run through.</param>
    /// <param name="chatId">The chat.</param>
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
