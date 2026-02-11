using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.DataAccess;
using TopicDal = DM.Services.DataAccess.BusinessObjects.Boards.Topic;
using DM.Services.DataAccess.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using Comment = DM.Services.DataAccess.BusinessObjects.Common.Comment;

namespace DM.Services.Forum.BusinessProcesses.Commentaries.Creating;

/// <inheritdoc />
internal class CommentaryCreatingRepository : ICommentaryCreatingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public CommentaryCreatingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Services.Common.Dto.Comment> Create(Comment comment, IUpdateBuilder<TopicDal> topicUpdate)
    {
        _dbContext.Comments.Add(comment);
        topicUpdate.AttachTo(_dbContext);
        await _dbContext.SaveChangesAsync();
        return await _dbContext.Comments
            .TagWith("DM.Forum.CreatedComment")
            .Where(c => c.CommentId == comment.CommentId)
            .ProjectTo<Services.Common.Dto.Comment>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }
}