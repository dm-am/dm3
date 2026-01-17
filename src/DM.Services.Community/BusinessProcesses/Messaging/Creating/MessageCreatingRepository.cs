using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.Community.BusinessProcesses.Messaging.Reading;
using DM.Services.DataAccess;
using DM.Services.DataAccess.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using DbConversation = DM.Services.DataAccess.BusinessObjects.Messaging.Conversation;
using DbMessage = DM.Services.DataAccess.BusinessObjects.Messaging.Message;

namespace DM.Services.Community.BusinessProcesses.Messaging.Creating;

/// <inheritdoc />
internal class MessageCreatingRepository : IMessageCreatingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public MessageCreatingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Message> Create(DbMessage message, IUpdateBuilder<DbConversation> updateConversation, CancellationToken ct = default)
    {
        _dbContext.Messages.Add(message);
        updateConversation.AttachTo(_dbContext);
        await _dbContext.SaveChangesAsync(ct);

        return await _dbContext.Messages
            .Where(m => m.MessageId == message.MessageId)
            .ProjectTo<Message>(_mapper.ConfigurationProvider)
            .FirstAsync(ct);
    }
}