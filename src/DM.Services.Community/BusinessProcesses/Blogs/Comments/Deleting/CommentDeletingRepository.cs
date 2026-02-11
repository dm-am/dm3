using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Common;
using PublicationDal = DM.Services.DataAccess.BusinessObjects.Blogs.Publication;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Community.BusinessProcesses.Blogs.Comments.Dto;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Community.BusinessProcesses.Blogs.Comments.Deleting;

/// <inheritdoc />
internal class CommentDeletingRepository : ICommentDeletingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public CommentDeletingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public Task<CommentToDelete?> GetForDelete(Guid commentId)
    {
        return _dbContext.Comments
            .TagWith("DM.Blogs.CommentToDelete")
            .Where(c => !c.IsRemoved && c.CommentId == commentId)
            .ProjectTo<CommentToDelete>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public Task Delete(IUpdateBuilder<Comment> update, IUpdateBuilder<PublicationDal> publicationUpdate)
    {
        update.AttachTo(_dbContext);
        publicationUpdate.AttachTo(_dbContext);
        return _dbContext.SaveChangesAsync();
    }
}
