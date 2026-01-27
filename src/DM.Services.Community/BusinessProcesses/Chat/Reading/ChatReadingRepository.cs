using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.Core.Dto;
using DM.Services.Core.Extensions;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Messaging;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Community.BusinessProcesses.Chat.Reading;

/// <inheritdoc />
internal class ChatReadingRepository : IChatReadingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public ChatReadingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    private IQueryable<Message> GlobalChatQuery =>
        _dbContext.Messages.Where(m => m.ConversationId == Message.GlobalChatId);

    /// <inheritdoc />
    public Task<int> Count() => GlobalChatQuery.CountAsync();

    /// <inheritdoc />
    public async Task<IEnumerable<ChatMessage>> Get(PagingData pagingData) => await GlobalChatQuery
        .OrderByDescending(m => m.CreatedUtc)
        .Page(pagingData)
        .ProjectTo<ChatMessage>(_mapper.ConfigurationProvider)
        .OrderBy(m => m.CreatedUtc)
        .ToArrayAsync();

    /// <inheritdoc />
    public async Task<IEnumerable<ChatMessage>> Get(DateTimeOffset since) =>
        await GlobalChatQuery
            .OrderByDescending(m => m.CreatedUtc)
            .Where(m => m.CreatedUtc >= since)
            .ProjectTo<ChatMessage>(_mapper.ConfigurationProvider)
            .OrderBy(m => m.CreatedUtc)
            .ToArrayAsync();

    /// <inheritdoc />
    public async Task<ChatMessage> Get(Guid id) => await GlobalChatQuery
        .Where(m => m.MessageId == id)
        .ProjectTo<ChatMessage>(_mapper.ConfigurationProvider)
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public async Task<IEnumerable<ChatMessage>> GetByDate(DateOnly date)
    {
        var startOfDay = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endOfDay = date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        return await GlobalChatQuery
            .Where(m => m.CreatedUtc >= startOfDay && m.CreatedUtc <= endOfDay)
            .OrderBy(m => m.CreatedUtc)
            .ProjectTo<ChatMessage>(_mapper.ConfigurationProvider)
            .ToArrayAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ChatMessage>> GetBefore(Guid messageId, int count)
    {
        var referenceMessage = await GlobalChatQuery
            .Where(m => m.MessageId == messageId)
            .Select(m => m.CreatedUtc)
            .FirstOrDefaultAsync();

        if (referenceMessage == default) return Array.Empty<ChatMessage>();

        return await GlobalChatQuery
            .Where(m => m.CreatedUtc < referenceMessage)
            .OrderByDescending(m => m.CreatedUtc)
            .Take(count)
            .ProjectTo<ChatMessage>(_mapper.ConfigurationProvider)
            .OrderBy(m => m.CreatedUtc)
            .ToArrayAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ChatMessage>> GetAfter(Guid messageId, int count)
    {
        var referenceMessage = await GlobalChatQuery
            .Where(m => m.MessageId == messageId)
            .Select(m => m.CreatedUtc)
            .FirstOrDefaultAsync();

        if (referenceMessage == default) return Array.Empty<ChatMessage>();

        return await GlobalChatQuery
            .Where(m => m.CreatedUtc > referenceMessage)
            .OrderBy(m => m.CreatedUtc)
            .Take(count)
            .ProjectTo<ChatMessage>(_mapper.ConfigurationProvider)
            .ToArrayAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ChatMessage>> GetAround(Guid messageId, int count)
    {
        var referenceMessage = await GlobalChatQuery
            .Where(m => m.MessageId == messageId)
            .Select(m => m.CreatedUtc)
            .FirstOrDefaultAsync();

        if (referenceMessage == default) return Array.Empty<ChatMessage>();

        var halfCount = count / 2;

        var before = await GlobalChatQuery
            .Where(m => m.CreatedUtc < referenceMessage)
            .OrderByDescending(m => m.CreatedUtc)
            .Take(halfCount)
            .ProjectTo<ChatMessage>(_mapper.ConfigurationProvider)
            .ToArrayAsync();

        var target = await GlobalChatQuery
            .Where(m => m.MessageId == messageId)
            .ProjectTo<ChatMessage>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();

        var after = await GlobalChatQuery
            .Where(m => m.CreatedUtc > referenceMessage)
            .OrderBy(m => m.CreatedUtc)
            .Take(halfCount)
            .ProjectTo<ChatMessage>(_mapper.ConfigurationProvider)
            .ToArrayAsync();

        var result = new List<ChatMessage>();
        result.AddRange(before.OrderBy(m => m.CreatedUtc));
        if (target != null) result.Add(target);
        result.AddRange(after);

        return result;
    }

    /// <inheritdoc />
    public async Task<bool> HasMessagesBefore(Guid messageId)
    {
        var referenceMessage = await GlobalChatQuery
            .Where(m => m.MessageId == messageId)
            .Select(m => m.CreatedUtc)
            .FirstOrDefaultAsync();

        if (referenceMessage == default) return false;

        return await GlobalChatQuery
            .AnyAsync(m => m.CreatedUtc < referenceMessage);
    }

    /// <inheritdoc />
    public async Task<bool> HasMessagesAfter(Guid messageId)
    {
        var referenceMessage = await GlobalChatQuery
            .Where(m => m.MessageId == messageId)
            .Select(m => m.CreatedUtc)
            .FirstOrDefaultAsync();

        if (referenceMessage == default) return false;

        return await GlobalChatQuery
            .AnyAsync(m => m.CreatedUtc > referenceMessage);
    }

    /// <inheritdoc />
    public async Task<ChatMessage> GetFirstOnOrAfterDate(DateOnly date)
    {
        var startOfDay = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        return await GlobalChatQuery
            .Where(m => m.CreatedUtc >= startOfDay)
            .OrderBy(m => m.CreatedUtc)
            .ProjectTo<ChatMessage>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public async Task<ChatMessage> GetLastOnOrBeforeDate(DateOnly date)
    {
        var endOfDay = date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        return await GlobalChatQuery
            .Where(m => m.CreatedUtc <= endOfDay)
            .OrderByDescending(m => m.CreatedUtc)
            .ProjectTo<ChatMessage>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }
}
