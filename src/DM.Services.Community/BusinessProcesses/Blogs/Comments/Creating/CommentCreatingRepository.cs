using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.DataAccess;
using PublicationDal = DM.Services.DataAccess.BusinessObjects.Blogs.Publication;
using DM.Services.DataAccess.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using Comment = DM.Services.DataAccess.BusinessObjects.Common.Comment;

namespace DM.Services.Community.BusinessProcesses.Blogs.Comments.Creating;

/// <inheritdoc />
internal class CommentCreatingRepository : ICommentCreatingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public CommentCreatingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Services.Common.Dto.Comment> Create(Comment comment, IUpdateBuilder<PublicationDal> publicationUpdate)
    {
        _dbContext.Comments.Add(comment);
        publicationUpdate.AttachTo(_dbContext);
        await _dbContext.SaveChangesAsync();
        return await _dbContext.Comments
            .TagWith("DM.Blogs.CreatedComment")
            .Where(c => c.CommentId == comment.CommentId)
            .ProjectTo<Services.Common.Dto.Comment>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }
}
