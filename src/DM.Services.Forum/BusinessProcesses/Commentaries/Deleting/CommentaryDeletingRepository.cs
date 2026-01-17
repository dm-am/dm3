using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Common;
using DM.Services.DataAccess.BusinessObjects.Boards;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Forum.Dto.Internal;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Forum.BusinessProcesses.Commentaries.Deleting;

/// <inheritdoc />
internal class CommentaryDeletingRepository : ICommentaryDeletingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public CommentaryDeletingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public Task<CommentToDelete> GetForDelete(Guid commentId)
    {
        return _dbContext.Comments
            .TagWith("DM.Forum.CommentToDelete")
            .Where(c => !c.IsRemoved && c.CommentId == commentId)
            .ProjectTo<CommentToDelete>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public Task Delete(IUpdateBuilder<Comment> update, IUpdateBuilder<ForumTopic> topicUpdate)
    {
        update.AttachTo(_dbContext);
        topicUpdate.AttachTo(_dbContext);
        return _dbContext.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<Guid?> GetSecondLastCommentId(Guid topicId)
    {
        var result = await _dbContext.Comments
            .TagWith("DM.Forum.SecondLastCommentAfterDelete")
            .Where(c => !c.IsRemoved && c.EntityId == topicId)
            .OrderByDescending(c => c.CreateDate)
            .Skip(1)
            .Select(c => c.CommentId)
            .FirstOrDefaultAsync();
        return result == default ? null : result;
    }
}