using System;
using System.Net;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Exceptions;
using DM.Services.DataAccess;
using DM.Services.Gaming.Authorization;
using DM.Services.Gaming.BusinessProcesses.Posts.Reading;
using DM.Services.Gaming.BusinessProcesses.Votes.Reading;
using DM.Services.Gaming.Dto.Input;
using DM.Services.Gaming.Dto.Output;
using DM.Services.MessageQueuing.GeneralBus;
using Microsoft.EntityFrameworkCore;
using DalVote = DM.Services.DataAccess.BusinessObjects.Games.Rating.Vote;

namespace DM.Services.Gaming.BusinessProcesses.Votes.Creating;

/// <inheritdoc />
internal class VoteCreatingService : IVoteCreatingService
{
    private readonly IPostReadingService postReadingService;
    private readonly IVoteReadingRepository voteReadingRepository;
    private readonly IIntentionManager intentionManager;
    private readonly IVoteCreatingRepository repository;
    private readonly IIdentityProvider identityProvider;
    private readonly IInvokedEventProducer producer;
    private readonly DmDbContext dbContext;
    private readonly IMapper mapper;

    /// <inheritdoc />
    public VoteCreatingService(
        IPostReadingService postReadingService,
        IVoteReadingRepository voteReadingRepository,
        IIntentionManager intentionManager,
        IVoteCreatingRepository repository,
        IIdentityProvider identityProvider,
        IInvokedEventProducer producer,
        DmDbContext dbContext,
        IMapper mapper)
    {
        this.postReadingService = postReadingService;
        this.voteReadingRepository = voteReadingRepository;
        this.intentionManager = intentionManager;
        this.repository = repository;
        this.identityProvider = identityProvider;
        this.producer = producer;
        this.dbContext = dbContext;
        this.mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Vote> Create(CreateVote createVote)
    {
        // Get the post and verify it exists
        var post = await postReadingService.Get(createVote.PostId);

        // Check authorization - can't vote on own posts
        intentionManager.ThrowIfForbidden(VoteIntention.Create, post);

        // Check if user has already voted
        var userId = identityProvider.Current.User.UserId;
        if (await voteReadingRepository.HasVoted(createVote.PostId, userId))
        {
            throw new HttpException(HttpStatusCode.Conflict, "You have already voted on this post");
        }

        // Get the GameId from the post's room
        var postWithRoom = await dbContext.Posts
            .Include(p => p.Room)
            .FirstOrDefaultAsync(p => p.PostId == createVote.PostId);

        if (postWithRoom == null)
        {
            throw new HttpException(HttpStatusCode.Gone, "Post not found");
        }

        // Create the vote
        var vote = new DalVote
        {
            VoteId = Guid.NewGuid(),
            PostId = createVote.PostId,
            GameId = postWithRoom.Room.GameId,
            UserId = userId,
            TargetUserId = post.Author.UserId,
            CreateDate = DateTimeOffset.UtcNow,
            Type = createVote.Type,
            SignValue = (short)createVote.Sign
        };

        var createdVote = await repository.Create(vote);
        await producer.Send(EventType.PostVoted, vote.VoteId);
        return mapper.Map<Vote>(createdVote);
    }
}
