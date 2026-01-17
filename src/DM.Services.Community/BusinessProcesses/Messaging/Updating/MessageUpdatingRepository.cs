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
    private readonly DmDbContext dbContext;
    private readonly IMapper mapper;

    /// <inheritdoc />
    public MessageUpdatingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        this.dbContext = dbContext;
        this.mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Message> Update(IUpdateBuilder<MessageDal> update)
    {
        var messageId = update.AttachTo(dbContext);
        await dbContext.SaveChangesAsync();
        return await dbContext.Messages
            .TagWith("DM.Community.UpdatedMessage")
            .Where(m => m.MessageId == messageId)
            .ProjectTo<Message>(mapper.ConfigurationProvider)
            .FirstAsync();
    }
}
