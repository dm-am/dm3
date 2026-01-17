using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Boards;
using DM.Services.Forum.Dto.Output;
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
    public async Task<Topic> Create(ForumTopic forumTopic, CancellationToken ct = default)
    {
        _dbContext.ForumTopics.Add(forumTopic);
        await _dbContext.SaveChangesAsync(ct);
        return await _dbContext.ForumTopics
            .TagWith("DM.Forum.CreatedTopic")
            .Where(t => t.ForumTopicId == forumTopic.ForumTopicId)
            .ProjectTo<Topic>(_mapper.ConfigurationProvider)
            .FirstAsync(ct);
    }
}