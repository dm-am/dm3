using DM.Domain.Core.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Persistence.Entities.Messaging;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using DbChat = DM.Infrastructure.Persistence.Entities.Messaging.Chat;
using DtoChat = DM.Domain.Messaging.Features.Chats.Chat;
using DM.Domain.Messaging.Features.Chats;

namespace DM.Infrastructure.Persistence.Repositories.Messaging;

/// <inheritdoc />
internal class ChatRepository : IChatRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IPublicIdService _publicIdService;

    /// <inheritdoc />
    public ChatRepository(
        DmDbContext dbContext,
        IPublicIdService publicIdService)
    {
        _dbContext = dbContext;
        _publicIdService = publicIdService;
    }

    /// <summary>
    /// Participation predicate
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns>Expression checking if user participates in chat</returns>
    public static Expression<Func<DbChat, bool>> UserParticipates(Guid userId) =>
        c => c.UserLinks.Any(l => !l.IsRemoved && l.UserId == userId);

    // ═══ READ ═══

    /// <summary>
    /// A conversation the list shows: one the user takes part in and somebody
    /// has written in.
    /// </summary>
    /// <remarks>
    /// Both reads have to agree on this, and they did not: the count took every
    /// chat and the page took only the ones with a message. Opening a person
    /// from the search box creates an empty chat on the spot, so the list said
    /// "Нет переписок" over a total of one, and further along it paged to
    /// screens with nothing on them.
    /// </remarks>
    private static Expression<Func<DbChat, bool>> Started(Guid userId) =>
        c => c.UserLinks.Any(l => !l.IsRemoved && l.UserId == userId) && c.LastMessageId.HasValue;

    /// <inheritdoc />
    public Task<int> Count(Guid userId) => _dbContext.Chats
        .Where(Started(userId))
        .CountAsync();

    /// <inheritdoc />
    public async Task<IEnumerable<DtoChat>> Get(Guid userId, PagingData paging) =>
        await _dbContext.Chats
            .Where(Started(userId))
            .OrderByDescending(c => c.LastMessage!.CreatedUtc)
            .Page(paging)
            .ProjectToChat()
            .ToArrayAsync();

    /// <inheritdoc />
    public Task<DtoChat?> Get(Guid chatId, Guid userId) => _dbContext.Chats
        .Where(c => c.ChatId == chatId)
        // Global chats are accessible to everyone, others require participation
        .Where(c => c.Type == ChatType.Global || c.UserLinks.Any(l => !l.IsRemoved && l.UserId == userId))
        .ProjectToChat()
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public Task<DtoChat?> GetByPublicId(string publicId, Guid userId) => _dbContext.Chats
        .TagWith("DM.Messaging.GetChatByPublicId")
        .Where(c => c.PublicId == publicId)
        // Global chats are accessible to everyone, others require participation
        .Where(c => c.Type == ChatType.Global || c.UserLinks.Any(l => !l.IsRemoved && l.UserId == userId))
        .ProjectToChat()
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public Task<DtoChat?> GetForUpdate(Guid chatId) => _dbContext.Chats
        .Where(c => c.ChatId == chatId)
        .ProjectToChat()
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public async Task<Guid?> FindUser(string username) => (await _dbContext.Users
        // Equality over lower(), not ILIKE. This resolves the person a direct chat
        // is opened with, and "_" is a legal login character that ILIKE reads as
        // "any character": the caller would be handed a private chat with whoever
        // the plan reached first and would write into it. The form also reaches
        // IX_Users_Username_Lower, which ILIKE cannot use at all.
        .Where(u => u.Username.ToLower() == username.ToLower() && !u.IsRemoved)
        .Select(u => new { u.UserId })
        .FirstOrDefaultAsync())?.UserId;

    /// <inheritdoc />
    public Task<DtoChat?> FindDirectChat(Guid userId, Guid otherUserId) => _dbContext.Chats
        .Where(c => c.Type == ChatType.Direct)
        .Where(UserParticipates(userId))
        .Where(UserParticipates(otherUserId))
        .ProjectToChat()
        .FirstOrDefaultAsync();

    // ═══ WRITE ═══

    /// <inheritdoc />
    public async Task<DtoChat> Create(CreateChatEntity chat, IEnumerable<CreateChatLinkEntity> chatLinks)
    {
        var serialNumber = await SerialNumberAllocator.NextAsync<DbChat>(_dbContext);

        var dbChat = new DbChat
        {
            ChatId = chat.ChatId,
            Type = chat.Type,
            Title = chat.Title,
            // Taken from the sequence before the insert, so the row is written with the
            // address it keeps — same shape as games and blogs, and for the same reason.
            SerialNumber = serialNumber,
            PublicId = _publicIdService.Encode(serialNumber)
        };

        var dbChatLinks = chatLinks.Select(l => new UserChatLink
        {
            UserChatLinkId = l.UserChatLinkId,
            ChatId = l.ChatId,
            UserId = l.UserId,
            IsRemoved = l.IsRemoved
        });

        _dbContext.Chats.Add(dbChat);
        _dbContext.UserChatLinks.AddRange(dbChatLinks);
        await _dbContext.SaveChangesAsync();

        return await _dbContext.Chats
            .Where(c => c.ChatId == chat.ChatId)
            .ProjectToChat()
            .FirstAsync();
    }

    /// <inheritdoc />
    public async Task<DtoChat> Update(UpdateChatEntity update)
    {
        var dbChat = await _dbContext.Chats.FindAsync(update.ChatId);
        if (dbChat == null)
        {
            throw new InvalidOperationException($"Chat {update.ChatId} not found");
        }

        if (update.Title != null)
            dbChat.Title = update.Title;
        if (update.LastMessageId.HasValue)
            dbChat.LastMessageId = update.LastMessageId;

        // Add new links
        var linksToAdd = update.AddLinks.ToArray();
        if (linksToAdd.Length > 0)
        {
            var dbLinksToAdd = linksToAdd.Select(l => new UserChatLink
            {
                UserChatLinkId = l.UserChatLinkId,
                ChatId = l.ChatId,
                UserId = l.UserId,
                IsRemoved = l.IsRemoved
            });
            _dbContext.UserChatLinks.AddRange(dbLinksToAdd);
        }

        // Remove links
        var removeUserIdsList = update.RemoveUserIds.ToArray();
        if (removeUserIdsList.Length > 0)
        {
            var linksToRemove = await _dbContext.UserChatLinks
                .Where(l => l.ChatId == update.ChatId && removeUserIdsList.Contains(l.UserId))
                .ToArrayAsync();
            foreach (var link in linksToRemove)
            {
                link.IsRemoved = true;
            }
        }

        await _dbContext.SaveChangesAsync();

        return await _dbContext.Chats
            .TagWith("DM.Community.UpdatedChat")
            .Where(c => c.ChatId == update.ChatId)
            .ProjectToChat()
            .FirstAsync();
    }

    /// <inheritdoc />
    public async Task<DtoChat> CreateGameRoomChat(CreateChatEntity chat)
    {
        var serialNumber = await SerialNumberAllocator.NextAsync<DbChat>(_dbContext);

        var dbChat = new DbChat
        {
            ChatId = chat.ChatId,
            Type = chat.Type,
            Title = chat.Title,
            RoomId = chat.RoomId,
            SerialNumber = serialNumber,
            PublicId = _publicIdService.Encode(serialNumber)
        };

        _dbContext.Chats.Add(dbChat);
        await _dbContext.SaveChangesAsync();

        return await _dbContext.Chats
            .Where(c => c.ChatId == chat.ChatId)
            .ProjectToChat()
            .FirstAsync();
    }

    /// <inheritdoc />
    /// <remarks>
    /// One statement, because the database already carries the rest: messages,
    /// their edits and the participation links hang off the chat with ON DELETE
    /// CASCADE. Reading the correspondence back to delete it row by row pulled
    /// every message body into memory only to throw it away, and that cost grew
    /// with the length of the conversation without any bound.
    /// </remarks>
    public async Task Delete(Guid chatId) =>
        await _dbContext.Chats
            .Where(c => c.ChatId == chatId)
            .ExecuteDeleteAsync();
}
