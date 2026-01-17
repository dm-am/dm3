using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.Core.Dto;
using DM.Services.Core.Extensions;
using DM.Services.DataAccess;
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

    /// <inheritdoc />
    public Task<int> Count() => _dbContext.ChatMessages.CountAsync();

    /// <inheritdoc />
    public async Task<IEnumerable<ChatMessage>> Get(PagingData pagingData) => await _dbContext.ChatMessages
        .OrderByDescending(m => m.CreateDate)
        .Page(pagingData)
        .ProjectTo<ChatMessage>(_mapper.ConfigurationProvider)
        .OrderBy(m => m.CreateDate)
        .ToArrayAsync();

    /// <inheritdoc />
    public async Task<IEnumerable<ChatMessage>> Get(DateTimeOffset since) =>
        await _dbContext.ChatMessages
            .OrderByDescending(m => m.CreateDate)
            .Where(m => m.CreateDate >= since)
            .ProjectTo<ChatMessage>(_mapper.ConfigurationProvider)
            .OrderBy(m => m.CreateDate)
            .ToArrayAsync();

    /// <inheritdoc />
    public async Task<ChatMessage> Get(Guid id) => await _dbContext.ChatMessages
        .Where(m => m.ChatMessageId == id)
        .ProjectTo<ChatMessage>(_mapper.ConfigurationProvider)
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public async Task<IEnumerable<ChatMessage>> GetByDate(DateOnly date)
    {
        var startOfDay = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endOfDay = date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        return await _dbContext.ChatMessages
            .Where(m => m.CreateDate >= startOfDay && m.CreateDate <= endOfDay)
            .OrderBy(m => m.CreateDate)
            .ProjectTo<ChatMessage>(_mapper.ConfigurationProvider)
            .ToArrayAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ChatMessage>> GetBefore(Guid messageId, int count)
    {
        var referenceMessage = await _dbContext.ChatMessages
            .Where(m => m.ChatMessageId == messageId)
            .Select(m => m.CreateDate)
            .FirstOrDefaultAsync();

        if (referenceMessage == default) return Array.Empty<ChatMessage>();

        return await _dbContext.ChatMessages
            .Where(m => m.CreateDate < referenceMessage)
            .OrderByDescending(m => m.CreateDate)
            .Take(count)
            .ProjectTo<ChatMessage>(_mapper.ConfigurationProvider)
            .OrderBy(m => m.CreateDate)
            .ToArrayAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ChatMessage>> GetAfter(Guid messageId, int count)
    {
        var referenceMessage = await _dbContext.ChatMessages
            .Where(m => m.ChatMessageId == messageId)
            .Select(m => m.CreateDate)
            .FirstOrDefaultAsync();

        if (referenceMessage == default) return Array.Empty<ChatMessage>();

        return await _dbContext.ChatMessages
            .Where(m => m.CreateDate > referenceMessage)
            .OrderBy(m => m.CreateDate)
            .Take(count)
            .ProjectTo<ChatMessage>(_mapper.ConfigurationProvider)
            .ToArrayAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ChatMessage>> GetAround(Guid messageId, int count)
    {
        var referenceMessage = await _dbContext.ChatMessages
            .Where(m => m.ChatMessageId == messageId)
            .Select(m => m.CreateDate)
            .FirstOrDefaultAsync();

        if (referenceMessage == default) return Array.Empty<ChatMessage>();

        var halfCount = count / 2;

        var before = await _dbContext.ChatMessages
            .Where(m => m.CreateDate < referenceMessage)
            .OrderByDescending(m => m.CreateDate)
            .Take(halfCount)
            .ProjectTo<ChatMessage>(_mapper.ConfigurationProvider)
            .ToArrayAsync();

        var target = await _dbContext.ChatMessages
            .Where(m => m.ChatMessageId == messageId)
            .ProjectTo<ChatMessage>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();

        var after = await _dbContext.ChatMessages
            .Where(m => m.CreateDate > referenceMessage)
            .OrderBy(m => m.CreateDate)
            .Take(halfCount)
            .ProjectTo<ChatMessage>(_mapper.ConfigurationProvider)
            .ToArrayAsync();

        var result = new List<ChatMessage>();
        result.AddRange(before.OrderBy(m => m.CreateDate));
        if (target != null) result.Add(target);
        result.AddRange(after);

        return result;
    }

    /// <inheritdoc />
    public async Task<bool> HasMessagesBefore(Guid messageId)
    {
        var referenceMessage = await _dbContext.ChatMessages
            .Where(m => m.ChatMessageId == messageId)
            .Select(m => m.CreateDate)
            .FirstOrDefaultAsync();

        if (referenceMessage == default) return false;

        return await _dbContext.ChatMessages
            .AnyAsync(m => m.CreateDate < referenceMessage);
    }

    /// <inheritdoc />
    public async Task<bool> HasMessagesAfter(Guid messageId)
    {
        var referenceMessage = await _dbContext.ChatMessages
            .Where(m => m.ChatMessageId == messageId)
            .Select(m => m.CreateDate)
            .FirstOrDefaultAsync();

        if (referenceMessage == default) return false;

        return await _dbContext.ChatMessages
            .AnyAsync(m => m.CreateDate > referenceMessage);
    }

    /// <inheritdoc />
    public async Task<ChatMessage> GetFirstOnOrAfterDate(DateOnly date)
    {
        var startOfDay = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        return await _dbContext.ChatMessages
            .Where(m => m.CreateDate >= startOfDay)
            .OrderBy(m => m.CreateDate)
            .ProjectTo<ChatMessage>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }
}