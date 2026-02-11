using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.Core.Dto;
using DM.Services.Core.Extensions;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess;
using Microsoft.EntityFrameworkCore;
using DbMessage = DM.Services.DataAccess.BusinessObjects.Messaging.Message;

namespace DM.Services.Community.BusinessProcesses.Messaging.Reading;

/// <inheritdoc />
internal class MessageReadingRepository : IMessageReadingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly ICursorService _cursorService;

    /// <inheritdoc />
    public MessageReadingRepository(
        DmDbContext dbContext,
        IMapper mapper,
        ICursorService cursorService)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _cursorService = cursorService;
    }

    private IQueryable<DbMessage> ConversationMessages(Guid conversationId) =>
        _dbContext.Messages.Where(m => !m.IsRemoved && m.ConversationId == conversationId);

    /// <inheritdoc />
    public Task<int> Count(Guid conversationId, CancellationToken ct = default) =>
        ConversationMessages(conversationId).CountAsync(ct);

    /// <inheritdoc />
    public async Task<IEnumerable<Message>> Get(Guid conversationId, PagingData paging, CancellationToken ct = default) =>
        await ConversationMessages(conversationId)
            .OrderBy(m => m.CreatedUtc)
            .Page(paging)
            .ProjectTo<Message>(_mapper.ConfigurationProvider)
            .ToArrayAsync(ct);

    /// <inheritdoc />
    public Task<Message?> Get(Guid messageId, Guid userId, CancellationToken ct = default) => _dbContext.Messages
        .Where(m => !m.IsRemoved && m.MessageId == messageId)
        .Where(m => m.Conversation.UserLinks.Any(l => !l.IsRemoved && l.UserId == userId))
        .ProjectTo<Message>(_mapper.ConfigurationProvider)
        .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public async Task<CursorResult<Message>> GetWithCursor(Guid conversationId, CursorQuery query, CancellationToken ct = default)
    {
        var limit = query.EffectiveLimit;

        // If we have a cursor, decode it
        if (!string.IsNullOrEmpty(query.Cursor) && _cursorService.TryDecode(query.Cursor, out var cursorData))
        {
            return cursorData.Direction == CursorDirection.Before
                ? await GetBefore(conversationId, cursorData.EntityId, cursorData.TimestampUtc, limit, ct)
                : await GetAfter(conversationId, cursorData.EntityId, cursorData.TimestampUtc, limit, ct);
        }

        // If we have aroundEntityId, get messages around that message
        if (query.AroundEntityId.HasValue)
        {
            return await GetAround(conversationId, query.AroundEntityId.Value, limit, ct);
        }

        // If we have nearTimestampUtc, get messages near that timestamp
        if (query.NearTimestampUtc.HasValue)
        {
            return await GetNearTimestamp(conversationId, query.NearTimestampUtc.Value, limit, ct);
        }

        // Default: get latest messages (newest first, then reverse for display)
        return await GetLatest(conversationId, limit, ct);
    }

    private async Task<CursorResult<Message>> GetLatest(Guid conversationId, int limit, CancellationToken ct)
    {
        var messages = await ConversationMessages(conversationId)
            .OrderByDescending(m => m.CreatedUtc)
            .Take(limit + 1) // +1 to check if there are more
            .ProjectTo<Message>(_mapper.ConfigurationProvider)
            .ToArrayAsync(ct);

        var hasMore = messages.Length > limit;
        var result = messages.Take(limit).OrderBy(m => m.CreatedUtc).ToArray();

        return CreateCursorResult(result, hasPrev: hasMore, hasNext: false);
    }

    private async Task<CursorResult<Message>> GetBefore(Guid conversationId, Guid messageId, DateTimeOffset timestampUtc, int limit, CancellationToken ct)
    {
        var messages = await ConversationMessages(conversationId)
            .Where(m => m.CreatedUtc < timestampUtc || (m.CreatedUtc == timestampUtc && m.MessageId != messageId))
            .OrderByDescending(m => m.CreatedUtc)
            .Take(limit + 1)
            .ProjectTo<Message>(_mapper.ConfigurationProvider)
            .ToArrayAsync(ct);

        var hasMore = messages.Length > limit;
        var result = messages.Take(limit).OrderBy(m => m.CreatedUtc).ToArray();

        // Check if there are messages after
        var hasNext = await ConversationMessages(conversationId)
            .AnyAsync(m => m.CreatedUtc > timestampUtc || (m.CreatedUtc == timestampUtc && m.MessageId != messageId), ct);

        return CreateCursorResult(result, hasPrev: hasMore, hasNext: hasNext);
    }

    private async Task<CursorResult<Message>> GetAfter(Guid conversationId, Guid messageId, DateTimeOffset timestampUtc, int limit, CancellationToken ct)
    {
        var messages = await ConversationMessages(conversationId)
            .Where(m => m.CreatedUtc > timestampUtc || (m.CreatedUtc == timestampUtc && m.MessageId != messageId))
            .OrderBy(m => m.CreatedUtc)
            .Take(limit + 1)
            .ProjectTo<Message>(_mapper.ConfigurationProvider)
            .ToArrayAsync(ct);

        var hasMore = messages.Length > limit;
        var result = messages.Take(limit).ToArray();

        // Check if there are messages before
        var hasPrev = await ConversationMessages(conversationId)
            .AnyAsync(m => m.CreatedUtc < timestampUtc || (m.CreatedUtc == timestampUtc && m.MessageId != messageId), ct);

        return CreateCursorResult(result, hasPrev: hasPrev, hasNext: hasMore);
    }

    /// <inheritdoc />
    public async Task<CursorResult<Message>> GetAround(Guid conversationId, Guid messageId, int limit, CancellationToken ct = default)
    {
        var referenceMessage = await ConversationMessages(conversationId)
            .Where(m => m.MessageId == messageId)
            .Select(m => new { m.MessageId, m.CreatedUtc })
            .FirstOrDefaultAsync(ct);

        if (referenceMessage == null)
        {
            return CreateCursorResult(Array.Empty<Message>(), false, false);
        }

        var halfCount = limit / 2;

        var before = await ConversationMessages(conversationId)
            .Where(m => m.CreatedUtc < referenceMessage.CreatedUtc)
            .OrderByDescending(m => m.CreatedUtc)
            .Take(halfCount + 1)
            .ProjectTo<Message>(_mapper.ConfigurationProvider)
            .ToArrayAsync(ct);

        var target = await ConversationMessages(conversationId)
            .Where(m => m.MessageId == messageId)
            .ProjectTo<Message>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);

        var after = await ConversationMessages(conversationId)
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
    public async Task<CursorResult<Message>> GetNearTimestamp(Guid conversationId, DateTimeOffset timestampUtc, int limit, CancellationToken ct = default)
    {
        // Find the first message on or after the timestamp
        var nearestMessage = await ConversationMessages(conversationId)
            .Where(m => m.CreatedUtc >= timestampUtc)
            .OrderBy(m => m.CreatedUtc)
            .Select(m => m.MessageId)
            .FirstOrDefaultAsync(ct);

        if (nearestMessage == default)
        {
            // No messages after, get the last message before
            nearestMessage = await ConversationMessages(conversationId)
                .Where(m => m.CreatedUtc < timestampUtc)
                .OrderByDescending(m => m.CreatedUtc)
                .Select(m => m.MessageId)
                .FirstOrDefaultAsync(ct);
        }

        if (nearestMessage == default)
        {
            return CreateCursorResult(Array.Empty<Message>(), false, false);
        }

        return await GetAround(conversationId, nearestMessage, limit, ct);
    }

    /// <inheritdoc />
    public async Task<bool> HasMessagesBefore(Guid conversationId, Guid messageId, CancellationToken ct = default)
    {
        var referenceMessage = await ConversationMessages(conversationId)
            .Where(m => m.MessageId == messageId)
            .Select(m => m.CreatedUtc)
            .FirstOrDefaultAsync(ct);

        if (referenceMessage == default) return false;

        return await ConversationMessages(conversationId)
            .AnyAsync(m => m.CreatedUtc < referenceMessage, ct);
    }

    /// <inheritdoc />
    public async Task<bool> HasMessagesAfter(Guid conversationId, Guid messageId, CancellationToken ct = default)
    {
        var referenceMessage = await ConversationMessages(conversationId)
            .Where(m => m.MessageId == messageId)
            .Select(m => m.CreatedUtc)
            .FirstOrDefaultAsync(ct);

        if (referenceMessage == default) return false;

        return await ConversationMessages(conversationId)
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
}