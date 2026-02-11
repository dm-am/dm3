using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Services.Community.BusinessProcesses.Reviews.Reading;
using DM.Services.Core.Configuration;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DM.Services.Community.BusinessProcesses.Reviews.Validation;

/// <inheritdoc />
internal class ReviewEligibilityService : IReviewEligibilityService
{
    private static readonly TimeSpan EditWindow = TimeSpan.FromDays(1);
    private static readonly TimeSpan PostReviewCooldownPerGame = TimeSpan.FromDays(3);

    private readonly DmDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ProbationConfiguration _probationConfig;

    /// <inheritdoc />
    public ReviewEligibilityService(
        DmDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        IOptions<ProbationConfiguration> probationConfig)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _probationConfig = probationConfig.Value;
    }

    /// <inheritdoc />
    public async Task<bool> HavePlayedTogether(Guid userId1, Guid userId2)
    {
        if (userId1 == userId2)
        {
            return false; // Can't review yourself
        }

        // Check if users participated in the same game
        // A user participates if they are:
        // 1. The game master (MasterId)
        // 2. The assistant (AssistantId)
        // 3. Have a valid character (Status != Declined, not removed)

        // Get all game IDs where userId1 participated
        var user1GameIds = await GetUserGameIds(userId1);

        if (!user1GameIds.Any())
        {
            return false;
        }

        // Check if userId2 participated in any of those games
        var user2ParticipatedInSharedGame = await _dbContext.Games
            .Where(g => user1GameIds.Contains(g.GameId) && !g.IsRemoved)
            .Where(g =>
                g.MasterId == userId2 ||
                g.AssistantId == userId2 ||
                g.Characters.Any(c =>
                    c.UserId == userId2 &&
                    c.Status != CharacterStatus.Declined &&
                    !c.IsRemoved))
            .AnyAsync();

        return user2ParticipatedInSharedGame;
    }

    /// <inheritdoc />
    public async Task<bool> CanReviewGame(Guid userId, Guid gameId)
    {
        // User can review a game only if they have at least one post in it
        var hasPostInGame = await _dbContext.Posts
            .Where(p => p.Room.GameId == gameId &&
                        p.UserId == userId &&
                        !p.IsRemoved)
            .AnyAsync();

        return hasPostInGame;
    }

    /// <inheritdoc />
    public Task<bool> HasUserReview(Guid authorId, Guid targetUserId)
    {
        return _dbContext.Reviews
            .Where(r => r.UserId == authorId &&
                        r.TargetType == ReviewTargetType.User &&
                        r.TargetId == targetUserId &&
                        !r.IsRemoved)
            .AnyAsync();
    }

    /// <inheritdoc />
    public Task<bool> HasGameReview(Guid authorId, Guid gameId)
    {
        return _dbContext.Reviews
            .Where(r => r.UserId == authorId &&
                        r.TargetType == ReviewTargetType.Game &&
                        r.TargetId == gameId &&
                        !r.IsRemoved)
            .AnyAsync();
    }

    /// <inheritdoc />
    public async Task<bool> IsNewbie(Guid userId)
    {
        var postCount = await _dbContext.Posts
            .Where(p => p.UserId == userId)
            .CountAsync();

        return postCount < _probationConfig.NewbiePostThreshold;
    }

    /// <inheritdoc />
    public bool CanEditReview(Review review)
    {
        // Platform reviews can always be edited (they are managed by moderators)
        if (review.TargetType == ReviewTargetType.Platform)
        {
            return true;
        }

        // Non-platform reviews can only be edited within 24 hours
        var now = _dateTimeProvider.Now;
        var editDeadline = review.CreatedUtc + EditWindow;

        return now <= editDeadline;
    }

    /// <inheritdoc />
    public async Task<bool> HasRecentPostReviewInGame(Guid authorId, Guid gameId)
    {
        var cutoffDate = _dateTimeProvider.Now - PostReviewCooldownPerGame;

        // Check if user has any post review in this game within the last 3 days
        // Post reviews have TargetType = Post, and we need to find posts in the specified game
        var hasRecentReview = await _dbContext.Reviews
            .Where(r => r.UserId == authorId &&
                        r.TargetType == ReviewTargetType.Post &&
                        r.TargetId.HasValue &&
                        !r.IsRemoved &&
                        r.CreatedUtc >= cutoffDate)
            .Join(_dbContext.Posts,
                review => review.TargetId,
                post => post.PostId,
                (review, post) => new { review, post })
            .Where(x => x.post.Room.GameId == gameId)
            .AnyAsync();

        return hasRecentReview;
    }

    private async Task<Guid[]> GetUserGameIds(Guid userId)
    {
        // Games where user is GM or Assistant
        var gmGames = await _dbContext.Games
            .Where(g => !g.IsRemoved && (g.MasterId == userId || g.AssistantId == userId))
            .Select(g => g.GameId)
            .ToArrayAsync();

        // Games where user has a valid character
        var playerGames = await _dbContext.Characters
            .Where(c => c.UserId == userId &&
                        c.Status != CharacterStatus.Declined &&
                        !c.IsRemoved &&
                        !c.Game.IsRemoved)
            .Select(c => c.GameId)
            .Distinct()
            .ToArrayAsync();

        return gmGames.Union(playerGames).ToArray();
    }
}
