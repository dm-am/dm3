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

namespace DM.Services.Community.BusinessProcesses.Blogs.Comments.Reading;

/// <inheritdoc />
internal class CommentReadingRepository : ICommentReadingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public CommentReadingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public Task<int> Count(Guid publicationId) => _dbContext.Comments
        .TagWith("DM.Blogs.CommentsCount")
        .CountAsync(c => !c.IsRemoved && c.EntityId == publicationId);

    /// <inheritdoc />
    public async Task<IEnumerable<Comment>> Get(Guid publicationId, PagingData paging)
    {
        return await _dbContext.Comments
            .TagWith("DM.Blogs.CommentsList")
            .Where(c => !c.IsRemoved && c.EntityId == publicationId)
            .OrderBy(c => c.CreatedUtc)
            .Page(paging)
            .ProjectTo<Comment>(_mapper.ConfigurationProvider)
            .ToArrayAsync();
    }

    /// <inheritdoc />
    public Task<Comment?> Get(Guid commentId)
    {
        return _dbContext.Comments
            .TagWith("DM.Blogs.Comment")
            .Where(c => !c.IsRemoved && c.CommentId == commentId)
            .ProjectTo<Comment>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public Task<List<Guid>> GetPublicationIdsByBlogId(Guid blogId)
    {
        return _dbContext.Publications
            .TagWith("DM.Blogs.PublicationIdsByBlogId")
            .Where(p => !p.IsRemoved && p.BlogId == blogId)
            .Select(p => p.PublicationId)
            .ToListAsync();
    }
}
