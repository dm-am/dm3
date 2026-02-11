using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.DataAccess;
using DM.Services.DataAccess.RelationalStorage;
using TopicDal = DM.Services.DataAccess.BusinessObjects.Boards.Topic;
using TopicDto = DM.Services.Forum.Dto.Output.Topic;
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
    public async Task<TopicDto> Update(IUpdateBuilder<TopicDal> updateBuilder)
    {
        var topicId = updateBuilder.AttachTo(_dbContext);
        await _dbContext.SaveChangesAsync();
        return await _dbContext.Topics
            .TagWith("DM.Forum.UpdatedTopic")
            .Where(t => t.TopicId == topicId)
            .ProjectTo<TopicDto>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }
}