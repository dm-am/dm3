using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.DataAccess;
using TopicDal = DM.Services.DataAccess.BusinessObjects.Boards.Topic;
using TopicDto = DM.Services.Forum.Dto.Output.Topic;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Forum.BusinessProcesses.Topics.Creating;

/// <inheritdoc />
internal class TopicCreatingRepository : ITopicCreatingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public TopicCreatingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<TopicDto> Create(TopicDal forumTopic, CancellationToken ct = default)
    {
        _dbContext.Topics.Add(forumTopic);
        await _dbContext.SaveChangesAsync(ct);
        return await _dbContext.Topics
            .TagWith("DM.Forum.CreatedTopic")
            .Where(t => t.TopicId == forumTopic.TopicId)
            .ProjectTo<TopicDto>(_mapper.ConfigurationProvider)
            .FirstAsync(ct);
    }
}