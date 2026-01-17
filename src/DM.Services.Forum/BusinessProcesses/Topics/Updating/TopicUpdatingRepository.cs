using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Boards;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Forum.Dto.Output;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Forum.BusinessProcesses.Topics.Updating;

/// <inheritdoc />
internal class TopicUpdatingRepository : ITopicUpdatingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public TopicUpdatingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Topic> Update(IUpdateBuilder<ForumTopic> updateBuilder)
    {
        var topicId = updateBuilder.AttachTo(_dbContext);
        await _dbContext.SaveChangesAsync();
        return await _dbContext.ForumTopics
            .TagWith("DM.Forum.UpdatedTopic")
            .Where(t => t.ForumTopicId == topicId)
            .ProjectTo<Topic>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }
}