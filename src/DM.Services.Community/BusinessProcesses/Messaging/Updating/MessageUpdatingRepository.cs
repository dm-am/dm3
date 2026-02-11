using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.Community.BusinessProcesses.Messaging.Reading;
using DM.Services.DataAccess;
using DM.Services.DataAccess.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using MessageDal = DM.Services.DataAccess.BusinessObjects.Messaging.Message;

namespace DM.Services.Community.BusinessProcesses.Messaging.Updating;

/// <inheritdoc />
internal class MessageUpdatingRepository : IMessageUpdatingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public MessageUpdatingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Message> Update(IUpdateBuilder<MessageDal> update)
    {
        var messageId = update.AttachTo(_dbContext);
        await _dbContext.SaveChangesAsync();
        return await _dbContext.Messages
            .TagWith("DM.Community.UpdatedMessage")
            .Where(m => m.MessageId == messageId)
            .ProjectTo<Message>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }
}
