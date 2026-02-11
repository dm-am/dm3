using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Messaging;
using DM.Services.DataAccess.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using Conversation = DM.Services.Community.BusinessProcesses.Messaging.Reading.Conversation;
using DbConversation = DM.Services.DataAccess.BusinessObjects.Messaging.Conversation;

namespace DM.Services.Community.BusinessProcesses.Messaging.Updating;

/// <inheritdoc />
internal class ConversationUpdatingRepository : IConversationUpdatingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public ConversationUpdatingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public Task<Conversation?> Get(Guid conversationId) => _dbContext.Conversations
        .Where(c => c.ConversationId == conversationId)
        .ProjectTo<Conversation>(_mapper.ConfigurationProvider)
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public async Task<Conversation> Update(
        IUpdateBuilder<DbConversation> update,
        IEnumerable<UserConversationLink> addLinks,
        IEnumerable<Guid> removeUserIds)
    {
        var conversationId = update.AttachTo(_dbContext);

        var linksToAdd = addLinks?.ToArray() ?? Array.Empty<UserConversationLink>();
        if (linksToAdd.Length > 0)
        {
            _dbContext.UserConversationLinks.AddRange(linksToAdd);
        }

        var removeUserIdsList = removeUserIds?.ToArray() ?? Array.Empty<Guid>();
        if (removeUserIdsList.Length > 0)
        {
            var linksToRemove = await _dbContext.UserConversationLinks
                .Where(l => l.ConversationId == conversationId && removeUserIdsList.Contains(l.UserId))
                .ToArrayAsync();
            foreach (var link in linksToRemove)
            {
                link.IsRemoved = true;
            }
        }

        await _dbContext.SaveChangesAsync();

        return await _dbContext.Conversations
            .TagWith("DM.Community.UpdatedConversation")
            .Where(c => c.ConversationId == conversationId)
            .ProjectTo<Conversation>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }
}
