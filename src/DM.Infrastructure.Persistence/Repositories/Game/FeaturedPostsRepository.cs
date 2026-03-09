using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Posts;
using DM.Infrastructure.Persistence.Entities.Account;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <inheritdoc cref="IFeaturedPostsRepository" />
internal class FeaturedPostsRepository : IFeaturedPostsRepository
{
    private const int TextPreviewLength = 300;

    private readonly DmDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public FeaturedPostsRepository(
        DmDbContext dbContext,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    private static GeneralUser MapUser(User user) => new()
    {
        UserId = user.UserId,
        Username = user.Username,
        Role = user.Role,
        IsHonorary = user.IsHonorary,
        AccessPolicy = user.AccessPolicy,
        LastActivityUtc = user.LastActivityUtc,
        OriginalPictureUrl = user.AvatarUpload != null ? user.AvatarUpload.FilePath : null,
        MediumPictureUrl = user.AvatarUpload != null ? (user.AvatarUpload.MediumFilePath ?? user.AvatarUpload.FilePath) : null,
        SmallPictureUrl = user.AvatarUpload != null ? (user.AvatarUpload.SmallFilePath ?? user.AvatarUpload.FilePath) : null,
        Status = user.Status,
        Name = user.Name,
        Location = user.Location,
        Gender = user.Gender,
        BirthdayDate = user.BirthdayDate,
        RatingDisabled = user.RatingDisabled,
        QualityRating = user.QualityRating,
        QuantityRating = user.QuantityRating
    };

    /// <inheritdoc />
    public async Task<FeaturedPost?> GetBestOfWeek()
    {
        var weekAgo = _dateTimeProvider.Now.AddDays(-7);

        var result = await _dbContext.Posts
            .TagWith("DM.FeaturedPosts.BestOfWeek")
            .Where(p => !p.IsRemoved)
            .Where(p => p.Room.AccessType == RoomAccessType.Open)
            .Where(p => !p.Room.IsRemoved)
            .Where(p => !p.Room.Game.IsRemoved)
            .Where(p => p.Room.Game.Status != ModuleStatus.Draft)
            .Where(p => p.Room.Game.PremoderationStatus == PremoderationStatus.Approved)
            .Select(p => new
            {
                Post = p,
                Author = p.Author,
                Character = p.Character,
                Game = p.Room.Game,
                Room = p.Room,
                Rating = _dbContext.Reviews
                    .Where(r => r.TargetType == ReviewTargetType.Post)
                    .Where(r => r.TargetId == p.PostId)
                    .Where(r => !r.IsRemoved)
                    .Where(r => r.CreatedUtc >= weekAgo)
                    .Sum(r => (int?)r.SignValue) ?? 0,
                ReviewCount = _dbContext.Reviews
                    .Where(r => r.TargetType == ReviewTargetType.Post)
                    .Where(r => r.TargetId == p.PostId)
                    .Where(r => !r.IsRemoved)
                    .Count()
            })
            .Where(x => x.Rating > 0)
            .OrderByDescending(x => x.Rating)
            .ThenByDescending(x => x.Post.CreatedUtc)
            .FirstOrDefaultAsync();

        if (result == null)
            return null;

        return new FeaturedPost
        {
            Id = result.Post.PostId,
            TextPreview = TruncateText(result.Post.Text),
            Author = MapUser(result.Author),
            CharacterName = result.Character?.Name,
            CreatedUtc = result.Post.CreatedUtc,
            GameId = result.Game.GameId,
            GameTitle = result.Game.Title,
            RoomId = result.Room.RoomId,
            RoomTitle = result.Room.Title,
            Rating = result.Rating,
            ReviewCount = result.ReviewCount
        };
    }

    /// <inheritdoc />
    public async Task<FeaturedPost?> GetLastWithPlus()
    {
        // Get post IDs from open rooms that are eligible
        var eligiblePostIds = _dbContext.Posts
            .Where(p => !p.IsRemoved)
            .Where(p => p.Room.AccessType == RoomAccessType.Open)
            .Where(p => !p.Room.IsRemoved)
            .Where(p => !p.Room.Game.IsRemoved)
            .Where(p => p.Room.Game.Status != ModuleStatus.Draft)
            .Where(p => p.Room.Game.PremoderationStatus == PremoderationStatus.Approved)
            .Select(p => p.PostId);

        // Find the latest positive review for an eligible post
        var lastPositiveReviewPostId = await _dbContext.Reviews
            .TagWith("DM.FeaturedPosts.LastWithPlus")
            .Where(r => r.TargetType == ReviewTargetType.Post)
            .Where(r => r.SignValue > 0)
            .Where(r => !r.IsRemoved)
            .Where(r => r.TargetId != null)
            .Where(r => eligiblePostIds.Contains(r.TargetId!.Value))
            .OrderByDescending(r => r.CreatedUtc)
            .Select(r => r.TargetId)
            .FirstOrDefaultAsync();

        if (lastPositiveReviewPostId == null)
            return null;

        // Get the post details
        var result = await _dbContext.Posts
            .TagWith("DM.FeaturedPosts.LastWithPlusDetails")
            .Where(p => p.PostId == lastPositiveReviewPostId)
            .Select(p => new
            {
                Post = p,
                Author = p.Author,
                Character = p.Character,
                Game = p.Room.Game,
                Room = p.Room,
                Rating = _dbContext.Reviews
                    .Where(r => r.TargetType == ReviewTargetType.Post)
                    .Where(r => r.TargetId == p.PostId)
                    .Where(r => !r.IsRemoved)
                    .Sum(r => (int?)r.SignValue) ?? 0,
                ReviewCount = _dbContext.Reviews
                    .Where(r => r.TargetType == ReviewTargetType.Post)
                    .Where(r => r.TargetId == p.PostId)
                    .Where(r => !r.IsRemoved)
                    .Count()
            })
            .FirstOrDefaultAsync();

        if (result == null)
            return null;

        return new FeaturedPost
        {
            Id = result.Post.PostId,
            TextPreview = TruncateText(result.Post.Text),
            Author = MapUser(result.Author),
            CharacterName = result.Character?.Name,
            CreatedUtc = result.Post.CreatedUtc,
            GameId = result.Game.GameId,
            GameTitle = result.Game.Title,
            RoomId = result.Room.RoomId,
            RoomTitle = result.Room.Title,
            Rating = result.Rating,
            ReviewCount = result.ReviewCount
        };
    }

    private static string TruncateText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // Strip BBCode tags for preview
        var plainText = StripBbCode(text);

        if (plainText.Length <= TextPreviewLength)
            return plainText;

        return plainText.Substring(0, TextPreviewLength) + "...";
    }

    private static string StripBbCode(string text)
    {
        // Simple BBCode stripping - remove [tag] and [/tag]
        var result = Regex.Replace(
            text,
            @"\[/?[^\]]+\]",
            string.Empty);

        return result.Trim();
    }
}
