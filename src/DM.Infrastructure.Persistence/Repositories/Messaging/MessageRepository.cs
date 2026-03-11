using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Messaging.Features.Messages;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using DbMessage = DM.Infrastructure.Persistence.Entities.Messaging.Message;

namespace DM.Infrastructure.Persistence.Repositories.Messaging;

/// <inheritdoc />
internal class MessageRepository : IMessageRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly ICursorService _cursorService;

    /// <inheritdoc />
    public MessageRepository(
        DmDbContext dbContext,
        IMapper mapper,
        ICursorService cursorService)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _cursorService = cursorService;
    }

    private IQueryable<DbMessage> ChatMessages(Guid chatId) =>
        _dbContext.Messages.Where(m => !m.IsRemoved && m.ChatId == chatId);

    // ═══ READ ═══

    /// <inheritdoc />
    public Task<Message?> Get(Guid messageId, Guid userId, CancellationToken ct = default) => _dbContext.Messages
        .Where(m => !m.IsRemoved && m.MessageId == messageId)
        .Where(m => m.Chat.UserLinks.Any(l => !l.IsRemoved && l.UserId == userId))
        .ProjectTo<Message>(_mapper.ConfigurationProvider)
        .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public async Task<CursorResult<Message>> GetWithCursor(Guid chatId, CursorQuery query, CancellationToken ct = default)
    {
        var limit = query.EffectiveLimit;

        // If we have a cursor, decode it
        if (!string.IsNullOrEmpty(query.Cursor) && _cursorService.TryDecode(query.Cursor, out var cursorData))
        {
            return cursorData.Direction == CursorDirection.Before
                ? await GetBefore(chatId, cursorData.EntityId, cursorData.TimestampUtc, limit, ct)
                : await GetAfter(chatId, cursorData.EntityId, cursorData.TimestampUtc, limit, ct);
        }

        // If we have aroundEntityId, get messages around that message
        if (query.AroundEntityId.HasValue)
        {
            return await GetAround(chatId, query.AroundEntityId.Value, limit, ct);
        }

        // If we have nearTimestampUtc, get messages near that timestamp
        if (query.NearTimestampUtc.HasValue)
        {
            return await GetNearTimestamp(chatId, query.NearTimestampUtc.Value, limit, ct);
        }

        // Default: get latest messages (newest first, then reverse for display)
        return await GetLatest(chatId, limit, ct);
    }

    private async Task<CursorResult<Message>> GetLatest(Guid chatId, int limit, CancellationToken ct)
    {
        var messages = await ChatMessages(chatId)
            .OrderByDescending(m => m.CreatedUtc)
            .Take(limit + 1) // +1 to check if there are more
            .ProjectTo<Message>(_mapper.ConfigurationProvider)
            .ToArrayAsync(ct);

        var hasMore = messages.Length > limit;
        var result = messages.Take(limit).OrderBy(m => m.CreatedUtc).ToArray();

        return CreateCursorResult(result, hasPrev: hasMore, hasNext: false);
    }

    private async Task<CursorResult<Message>> GetBefore(Guid chatId, Guid messageId, DateTimeOffset timestampUtc, int limit, CancellationToken ct)
    {
        var messages = await ChatMessages(chatId)
            .Where(m => m.CreatedUtc < timestampUtc || (m.CreatedUtc == timestampUtc && m.MessageId != messageId))
            .OrderByDescending(m => m.CreatedUtc)
            .Take(limit + 1)
            .ProjectTo<Message>(_mapper.ConfigurationProvider)
            .ToArrayAsync(ct);

        var hasMore = messages.Length > limit;
        var result = messages.Take(limit).OrderBy(m => m.CreatedUtc).ToArray();

        // Check if there are messages after
        var hasNext = await ChatMessages(chatId)
            .AnyAsync(m => m.CreatedUtc > timestampUtc || (m.CreatedUtc == timestampUtc && m.MessageId != messageId), ct);

        return CreateCursorResult(result, hasPrev: hasMore, hasNext: hasNext);
    }

    private async Task<CursorResult<Message>> GetAfter(Guid chatId, Guid messageId, DateTimeOffset timestampUtc, int limit, CancellationToken ct)
    {
        var messages = await ChatMessages(chatId)
            .Where(m => m.CreatedUtc > timestampUtc || (m.CreatedUtc == timestampUtc && m.MessageId != messageId))
            .OrderBy(m => m.CreatedUtc)
            .Take(limit + 1)
            .ProjectTo<Message>(_mapper.ConfigurationProvider)
            .ToArrayAsync(ct);

        var hasMore = messages.Length > limit;
        var result = messages.Take(limit).ToArray();

        // Check if there are messages before
        var hasPrev = await ChatMessages(chatId)
            .AnyAsync(m => m.CreatedUtc < timestampUtc || (m.CreatedUtc == timestampUtc && m.MessageId != messageId), ct);

        return CreateCursorResult(result, hasPrev: hasPrev, hasNext: hasMore);
    }

    /// <inheritdoc />
    public async Task<CursorResult<Message>> GetAround(Guid chatId, Guid messageId, int limit, CancellationToken ct = default)
    {
        var referenceMessage = await ChatMessages(chatId)
            .Where(m => m.MessageId == messageId)
            .Select(m => new { m.MessageId, m.CreatedUtc })
            .FirstOrDefaultAsync(ct);

        if (referenceMessage == null)
        {
            return CreateCursorResult(Array.Empty<Message>(), false, false);
        }

        var halfCount = limit / 2;

        var before = await ChatMessages(chatId)
            .Where(m => m.CreatedUtc < referenceMessage.CreatedUtc)
            .OrderByDescending(m => m.CreatedUtc)
            .Take(halfCount + 1)
            .ProjectTo<Message>(_mapper.ConfigurationProvider)
            .ToArrayAsync(ct);

        var target = await ChatMessages(chatId)
            .Where(m => m.MessageId == messageId)
            .ProjectTo<Message>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);

        var after = await ChatMessages(chatId)
            .Where(m => m.CreatedUtc > referenceMessage.CreatedUtc)
            .OrderBy(m => m.CreatedUtc)
            .Take(halfCount + 1)
            .ProjectTo<Message>(_mapper.ConfigurationProvider)
            .ToArrayAsync(ct);

        var hasPrev = before.Length > halfCount;
        var hasNext = after.Length > halfCount;

        var result = new List<Message>();
        result.AddRange(before.Take(halfCount).OrderBy(m => m.CreatedUtc));
        if (target != null) result.Add(target);
        result.AddRange(after.Take(halfCount));

        return CreateCursorResult(result.ToArray(), hasPrev, hasNext);
    }

    /// <inheritdoc />
    public async Task<CursorResult<Message>> GetNearTimestamp(Guid chatId, DateTimeOffset timestampUtc, int limit, CancellationToken ct = default)
    {
        // Find the first message on or after the timestamp
        var nearestMessage = await ChatMessages(chatId)
            .Where(m => m.CreatedUtc >= timestampUtc)
            .OrderBy(m => m.CreatedUtc)
            .Select(m => m.MessageId)
            .FirstOrDefaultAsync(ct);

        if (nearestMessage == default)
        {
            // No messages after, get the last message before
            nearestMessage = await ChatMessages(chatId)
                .Where(m => m.CreatedUtc < timestampUtc)
                .OrderByDescending(m => m.CreatedUtc)
                .Select(m => m.MessageId)
                .FirstOrDefaultAsync(ct);
        }

        if (nearestMessage == default)
        {
            return CreateCursorResult(Array.Empty<Message>(), false, false);
        }

        return await GetAround(chatId, nearestMessage, limit, ct);
    }

    /// <inheritdoc />
    public async Task<bool> HasMessagesBefore(Guid chatId, Guid messageId, CancellationToken ct = default)
    {
        var referenceMessage = await ChatMessages(chatId)
            .Where(m => m.MessageId == messageId)
            .Select(m => m.CreatedUtc)
            .FirstOrDefaultAsync(ct);

        if (referenceMessage == default) return false;

        return await ChatMessages(chatId)
            .AnyAsync(m => m.CreatedUtc < referenceMessage, ct);
    }

    /// <inheritdoc />
    public async Task<bool> HasMessagesAfter(Guid chatId, Guid messageId, CancellationToken ct = default)
    {
        var referenceMessage = await ChatMessages(chatId)
            .Where(m => m.MessageId == messageId)
            .Select(m => m.CreatedUtc)
            .FirstOrDefaultAsync(ct);

        if (referenceMessage == default) return false;

        return await ChatMessages(chatId)
            .AnyAsync(m => m.CreatedUtc > referenceMessage, ct);
    }

    private CursorResult<Message> CreateCursorResult(Message[] messages, bool hasPrev, bool hasNext)
    {
        string? prevCursor = null;
        string? nextCursor = null;

        if (messages.Length > 0)
        {
            var firstMessage = messages[0];
            var lastMessage = messages[^1];

            if (hasPrev)
            {
                prevCursor = _cursorService.CreateBeforeCursor(firstMessage.Id, firstMessage.CreatedUtc);
            }

            if (hasNext)
            {
                nextCursor = _cursorService.CreateAfterCursor(lastMessage.Id, lastMessage.CreatedUtc);
            }
        }

        return new CursorResult<Message>
        {
            Data = messages,
            PrevCursor = prevCursor,
            NextCursor = nextCursor,
            HasPrev = hasPrev,
            HasNext = hasNext
        };
    }

    // ═══ WRITE ═══

    /// <inheritdoc />
    public async Task<Message> Create(CreateMessageEntity message, UpdateChatLastMessageEntity updateChat, CancellationToken ct = default)
    {
        var dbMessage = new DbMessage
        {
            MessageId = message.MessageId,
            ChatId = message.ChatId,
            UserId = message.UserId,
            Text = message.Text,
            CreatedUtc = message.CreatedUtc,
            IsRemoved = message.IsRemoved,
            GlobalChatEventId = message.GlobalChatEventId
        };

        _dbContext.Messages.Add(dbMessage);

        // Update chat's last message
        var dbChat = await _dbContext.Chats.FindAsync(new object[] { updateChat.ChatId }, ct);
        if (dbChat != null)
        {
            dbChat.LastMessageId = updateChat.LastMessageId;
        }

        await _dbContext.SaveChangesAsync(ct);

        return await _dbContext.Messages
            .Where(m => m.MessageId == message.MessageId)
            .ProjectTo<Message>(_mapper.ConfigurationProvider)
            .FirstAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Message> Update(UpdateMessageEntity update)
    {
        var dbMessage = await _dbContext.Messages.FindAsync(update.MessageId);
        if (dbMessage == null)
        {
            throw new InvalidOperationException($"Message {update.MessageId} not found");
        }

        if (update.Text != null)
            dbMessage.Text = update.Text;
        if (update.IsRemoved.HasValue)
            dbMessage.IsRemoved = update.IsRemoved.Value;
        if (update.ModifiedUtc.HasValue)
            dbMessage.ModifiedUtc = update.ModifiedUtc.Value;

        await _dbContext.SaveChangesAsync();
        return await _dbContext.Messages
            .TagWith("DM.Community.UpdatedMessage")
            .Where(m => m.MessageId == update.MessageId)
            .ProjectTo<Message>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }

    /// <inheritdoc />
    public async Task Delete(Guid messageId, Guid deletedByUserId, CancellationToken ct = default)
    {
        // Get chat info before deletion
        var messageInfo = await _dbContext.Messages
            .Where(m => m.MessageId == messageId)
            .Select(m => new { m.ChatId, m.Chat.LastMessageId })
            .FirstOrDefaultAsync(ct);

        // Mark message as removed
        await _dbContext.Messages
            .Where(m => m.MessageId == messageId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.IsRemoved, true)
                .SetProperty(m => m.DeletedByUserId, deletedByUserId)
                .SetProperty(m => m.DeletedUtc, DateTimeOffset.UtcNow), ct);

        // If this was the last message in chat, update LastMessageId
        if (messageInfo?.LastMessageId == messageId)
        {
            var newLastMessageId = await _dbContext.Messages
                .Where(m => m.ChatId == messageInfo.ChatId)
                .OrderByDescending(m => m.CreatedUtc)
                .Select(m => (Guid?)m.MessageId)
                .FirstOrDefaultAsync(ct);

            await _dbContext.Chats
                .Where(c => c.ChatId == messageInfo.ChatId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.LastMessageId, newLastMessageId), ct);
        }
    }
}
