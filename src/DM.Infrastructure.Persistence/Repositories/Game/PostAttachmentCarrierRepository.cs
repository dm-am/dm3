using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Posts;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <inheritdoc />
/// <remarks>
/// The projection every read of a post goes through, without the room filter and without the enrichments
/// a page needs: the caller asks the text and who reads its blocks, nothing else.
/// </remarks>
internal class PostAttachmentCarrierRepository : IPostAttachmentCarrierRepository
{
    private readonly DmDbContext _dbContext;

    public PostAttachmentCarrierRepository(DmDbContext dbContext) => _dbContext = dbContext;

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, Post>> GetCarriers(IReadOnlyCollection<Guid> postIds)
    {
        if (postIds.Count == 0) return new Dictionary<Guid, Post>();

        return await _dbContext.Posts
            .Where(p => postIds.Contains(p.PostId))
            .ProjectToPost()
            .ToDictionaryAsync(p => p.Id);
    }
}
