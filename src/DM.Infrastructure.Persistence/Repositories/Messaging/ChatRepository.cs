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
    private readonly PublicIdService _publicIdService;

    public ChatRepository(
        DmDbContext dbContext,
        PublicIdService publicIdService)
    {
        _dbContext = dbContext;
        _publicIdService = publicIdService;
    }

    private static Expression<Func<DbChat, bool>> UserParticipates(Guid userId) =>
        c => c.UserLinks.Any(l => !l.IsRemoved && l.UserId == userId);

    /// <summary>
    /// Global chats are open to everyone, others to their participants.
    /// </summary>
    private static Expression<Func<DbChat, bool>> Readable(Guid userId) =>
        c => c.Type == ChatType.Global || c.UserLinks.Any(l => !l.IsRemoved && l.UserId == userId);

    /// <summary>
    /// The conversations the list shows, for the count and the page alike: the user
    /// takes part and somebody has written (opening a person creates an empty chat).
    /// </summary>
    private IQueryable<DbChat> Started(Guid userId) =>
        _dbContext.Chats.Where(UserParticipates(userId)).Where(c => c.LastMessageId.HasValue);

    /// <inheritdoc />
    public Task<int> Count(Guid userId) => Started(userId).CountAsync();

    /// <inheritdoc />
    public async Task<IEnumerable<DtoChat>> Get(Guid userId, PagingData paging) =>
        await Started(userId)
            .OrderByDescending(c => c.LastMessage!.CreatedUtc)
            .Page(paging)
            .ProjectToChat(userId)
            .ToArrayAsync();

    /// <inheritdoc />
    public Task<DtoChat?> Get(Guid chatId, Guid userId) => _dbContext.Chats
        .Where(c => c.ChatId == chatId)
        .Where(Readable(userId))
        .ProjectToChat(userId)
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public Task<DtoChat?> GetByPublicId(string publicId, Guid userId) => _dbContext.Chats
        .TagWith("DM.Messaging.GetChatByPublicId")
        .Where(c => c.PublicId == publicId)
        .Where(Readable(userId))
        .ProjectToChat(userId)
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public Task<DtoChat?> GetForUpdate(Guid chatId) => _dbContext.Chats
        .Where(c => c.ChatId == chatId)
        .ProjectToChat()
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    /// <remarks>
    /// Ordered, and not for presentation: nothing in the schema stops a pair from
    /// owning two direct chats, and two openings at once each read no chat and
    /// each created one. Without an order the two correspondents can settle on
    /// different rows of that pair - each writing into a conversation the other
    /// never opens - and which row a reader gets is the planner's to choose. The
    /// oldest is the one every reader reaches, which also collapses the pairs that
    /// have already accumulated.
    /// </remarks>
    public Task<DtoChat?> FindDirectChat(Guid userId, Guid otherUserId) => _dbContext.Chats
        .Where(c => c.Type == ChatType.Direct)
        .Where(UserParticipates(userId))
        .Where(UserParticipates(otherUserId))
        .OrderBy(c => c.SerialNumber)
        .ProjectToChat(userId)
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public async Task<DtoChat> Create(CreateChatEntity chat, IEnumerable<CreateChatLinkEntity> chatLinks)
    {
        // Taken from the sequence before the insert, so the row is written with the address it keeps.
        var serialNumber = await SerialNumberAllocator.NextAsync<DbChat>(_dbContext);

        _dbContext.Chats.Add(new DbChat
        {
            ChatId = chat.ChatId,
            Type = chat.Type,
            Title = chat.Title,
            RoomId = chat.RoomId,
            SerialNumber = serialNumber,
            PublicId = _publicIdService.Encode(serialNumber)
        });
        _dbContext.UserChatLinks.AddRange(chatLinks.Select(Link));
        await _dbContext.SaveChangesAsync();

        return await _dbContext.Chats
            .Where(c => c.ChatId == chat.ChatId)
            .ProjectToChat()
            .FirstAsync();
    }

    /// <inheritdoc />
    public async Task<DtoChat> Update(UpdateChatEntity update, Guid viewerId)
    {
        var dbChat = await _dbContext.Chats.FindAsync(update.ChatId);
        if (dbChat == null)
        {
            throw new InvalidOperationException($"Chat {update.ChatId} not found");
        }

        if (update.Title != null)
            dbChat.Title = update.Title;

        _dbContext.UserChatLinks.AddRange(update.AddLinks.Select(Link));

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
            .ProjectToChat(viewerId)
            .FirstAsync();
    }

    /// <inheritdoc />
    /// <remarks>
    /// The link is stamped with who ended it and when: the leaver himself, at the
    /// moment he left.
    /// </remarks>
    public Task Leave(Guid chatId, Guid userId, DateTimeOffset leftUtc) =>
        SoftDelete.MarkWhere(
            _dbContext.UserChatLinks.Where(l => l.ChatId == chatId && l.UserId == userId && !l.IsRemoved),
            userId, leftUtc);

    /// <inheritdoc />
    public Task ClearHistory(Guid chatId, Guid userId, DateTimeOffset clearedUtc) =>
        _dbContext.UserChatLinks
            .Where(l => l.ChatId == chatId && l.UserId == userId && !l.IsRemoved)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.HistoryClearedUtc, clearedUtc));

    /// <inheritdoc />
    public Task<DtoChat> CreateGameRoomChat(CreateChatEntity chat) => Create(chat, []);

    private static UserChatLink Link(CreateChatLinkEntity link) => new()
    {
        UserChatLinkId = link.UserChatLinkId,
        ChatId = link.ChatId,
        UserId = link.UserId,
        IsRemoved = link.IsRemoved
    };

    /// <inheritdoc />
    /// <remarks>
    /// One statement, because the database already carries the rest: messages,
    /// their edits and the participation links hang off the chat with ON DELETE
    /// CASCADE. Reading the correspondence back to delete it row by row pulled
    /// every message body into memory only to throw it away, and that cost grew
    /// with the length of the conversation without any bound.
    /// </remarks>
    public Task Delete(Guid chatId) =>
        _dbContext.Chats
            .Where(c => c.ChatId == chatId)
            .ExecuteDeleteAsync();
}
