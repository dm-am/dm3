using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Core.Exceptions;
using DM.Services.Gaming.BusinessProcesses.Posts.Reading;
using DM.Services.Gaming.Dto.Output;

namespace DM.Services.Gaming.BusinessProcesses.Votes.Reading;

/// <inheritdoc />
internal class VoteReadingService : IVoteReadingService
{
    private readonly IPostReadingService postReadingService;
    private readonly IVoteReadingRepository repository;

    /// <inheritdoc />
    public VoteReadingService(
        IPostReadingService postReadingService,
        IVoteReadingRepository repository)
    {
        this.postReadingService = postReadingService;
        this.repository = repository;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Vote>> GetByPost(Guid postId)
    {
        // Verify post exists
        await postReadingService.Get(postId);
        return await repository.GetByPost(postId);
    }

    /// <inheritdoc />
    public async Task<Vote> Get(Guid voteId)
    {
        var vote = await repository.Get(voteId);
        if (vote == null)
        {
            throw new HttpException(HttpStatusCode.Gone, "Vote not found");
        }

        return vote;
    }
}
