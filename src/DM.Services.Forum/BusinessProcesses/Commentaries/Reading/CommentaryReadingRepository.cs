using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.Common.Dto;
using DM.Services.Core.Dto;
using DM.Services.Core.Extensions;
using DM.Services.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Forum.BusinessProcesses.Commentaries.Reading;

/// <inheritdoc />
internal class CommentaryReadingRepository : ICommentaryReadingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public CommentaryReadingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public Task<int> Count(Guid topicId) => _dbContext.Comments
        .TagWith("DM.Forum.CommentsCount")
        .CountAsync(c => !c.IsRemoved && c.EntityId == topicId);

    /// <inheritdoc />
    public async Task<IEnumerable<Comment>> Get(Guid topicId, PagingData paging)
    {
        return await _dbContext.Comments
            .TagWith("DM.Forum.CommentsList")
            .Where(c => !c.IsRemoved && c.EntityId == topicId)
            .OrderBy(c => c.CreatedUtc)
            .Page(paging)
            .ProjectTo<Comment>(_mapper.ConfigurationProvider)
            .ToArrayAsync();
    }

    /// <inheritdoc />
    public Task<Comment?> Get(Guid commentId)
    {
        return _dbContext.Comments
            .TagWith("DM.Forum.Comment")
            .Where(c => !c.IsRemoved && c.CommentId == commentId)
            .ProjectTo<Comment>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync()!;
    }
}