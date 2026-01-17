using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.Core.Dto;
using DM.Services.Core.Extensions;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Messaging;
using Microsoft.EntityFrameworkCore;
using DbConversation = DM.Services.DataAccess.BusinessObjects.Messaging.Conversation;

namespace DM.Services.Community.BusinessProcesses.Messaging.Reading;

/// <inheritdoc />
internal class ConversationReadingRepository : IConversationReadingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public ConversationReadingRepository(
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
    public static Expression<Func<DbConversation, bool>> UserParticipates(Guid userId) =>
        c => c.UserLinks.Any(l => !l.IsRemoved && l.UserId == userId);

    /// <inheritdoc />
    public Task<int> Count(Guid userId) => _dbContext.Conversations
        .Where(UserParticipates(userId))
        .CountAsync();

    /// <inheritdoc />
    public async Task<IEnumerable<Conversation>> Get(Guid userId, PagingData paging) =>
        await _dbContext.Conversations
            .Where(UserParticipates(userId))
            .Where(c => c.LastMessageId.HasValue)
            .OrderByDescending(c => c.LastMessage.CreateDate)
            .Page(paging)
            .ProjectTo<Conversation>(_mapper.ConfigurationProvider)
            .ToArrayAsync();

    /// <inheritdoc />
    public Task<Conversation> Get(Guid conversationId, Guid userId) => _dbContext.Conversations
        .Where(UserParticipates(userId))
        .ProjectTo<Conversation>(_mapper.ConfigurationProvider)
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public async Task<Guid?> FindUser(string login) => (await _dbContext.Users
        .Where(u => u.Login.ToLower() == login.ToLower() && !u.IsRemoved && u.Activated)
        .Select(u => new {u.UserId})
        .FirstOrDefaultAsync())?.UserId;

    /// <inheritdoc />
    public Task<Conversation> FindVisaviConversation(Guid userId, Guid visaviId) => _dbContext.Conversations
        .Where(c => c.Visavi)
        .Where(UserParticipates(userId))
        .Where(UserParticipates(visaviId))
        .ProjectTo<Conversation>(_mapper.ConfigurationProvider)
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public async Task<Conversation> Create(DbConversation conversation,
        IEnumerable<UserConversationLink> conversationLinks)
    {
        _dbContext.Conversations.Add(conversation);
        _dbContext.UserConversationLinks.AddRange(conversationLinks);
        await _dbContext.SaveChangesAsync();

        return await _dbContext.Conversations
            .Where(c => c.ConversationId == conversation.ConversationId)
            .ProjectTo<Conversation>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }
}