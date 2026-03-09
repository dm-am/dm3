using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
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
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public ChatRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <summary>
    /// Participation predicate
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns></returns>
    public static Expression<Func<DbChat, bool>> UserParticipates(Guid userId) =>
        c => c.UserLinks.Any(l => !l.IsRemoved && l.UserId == userId);

    // ═══ READ ═══

    /// <inheritdoc />
    public Task<int> Count(Guid userId) => _dbContext.Chats
        .Where(UserParticipates(userId))
        .CountAsync();

    /// <inheritdoc />
    public async Task<IEnumerable<DtoChat>> Get(Guid userId, PagingData paging) =>
        await _dbContext.Chats
            .Where(UserParticipates(userId))
            .Where(c => c.LastMessageId.HasValue)
            .OrderByDescending(c => c.LastMessage!.CreatedUtc)
            .Page(paging)
            .ProjectTo<DtoChat>(_mapper.ConfigurationProvider)
            .ToArrayAsync();

    /// <inheritdoc />
    public Task<DtoChat?> Get(Guid chatId, Guid userId) => _dbContext.Chats
        .Where(c => c.ChatId == chatId)
        .Where(UserParticipates(userId))
        .ProjectTo<DtoChat>(_mapper.ConfigurationProvider)
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public Task<DtoChat?> GetForUpdate(Guid chatId) => _dbContext.Chats
        .Where(c => c.ChatId == chatId)
        .ProjectTo<DtoChat>(_mapper.ConfigurationProvider)
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public async Task<Guid?> FindUser(string username) => (await _dbContext.Users
        .Where(u => EF.Functions.ILike(u.Username, username) && !u.IsRemoved)
        .Select(u => new { u.UserId })
        .FirstOrDefaultAsync())?.UserId;

    /// <inheritdoc />
    public Task<DtoChat?> FindDirectChat(Guid userId, Guid otherUserId) => _dbContext.Chats
        .Where(c => c.Type == ChatType.Direct)
        .Where(UserParticipates(userId))
        .Where(UserParticipates(otherUserId))
        .ProjectTo<DtoChat>(_mapper.ConfigurationProvider)
        .FirstOrDefaultAsync();

    // ═══ WRITE ═══

    /// <inheritdoc />
    public async Task<DtoChat> Create(CreateChatEntity chat, IEnumerable<CreateChatLinkEntity> chatLinks)
    {
        var dbChat = new DbChat
        {
            ChatId = chat.ChatId,
            Type = chat.Type,
            Title = chat.Title
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
            .ProjectTo<DtoChat>(_mapper.ConfigurationProvider)
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
            .ProjectTo<DtoChat>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }
}
