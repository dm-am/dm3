using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Game.Features.Games;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Posts;
using DM.Infrastructure.Persistence.Repositories.Game;
using DM.Infrastructure.Persistence.Shared.Users;
using DM.Testing;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using DbCharacter = DM.Infrastructure.Persistence.Entities.Game.Characters.Character;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbPost = DM.Infrastructure.Persistence.Entities.Game.Posts.Post;
using DbPostReview = DM.Infrastructure.Persistence.Entities.Game.PostReview;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using GameDto = DM.Domain.Game.Features.Games.Game;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Game;

public class PostRepositoryShould : UnitTestBase
{
    private readonly DmDbContext _dbContext;
    private readonly PostRepository _repository;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Guid _roomId = Guid.NewGuid();

    public PostRepositoryShould()
    {
        _dbContext = new DmDbContext(new DbContextOptionsBuilder<DmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<GameMappingProfile>();
            cfg.AddProfile<GeneralUserMappingProfile>();
        }).CreateMapper();

        // The post→room→game hydration step (GameRepository.GetByIds) is out of
        // scope for the sort assertion and relies on GroupBy shapes the EF Core
        // InMemory provider cannot translate, so it is stubbed. The rating-desc
        // ordering happens before that step, entirely on the InMemory query.
        var gameRepository = Mock<IGameRepository>();
        gameRepository
            .Setup(r => r.GetByIds(It.IsAny<IEnumerable<Guid>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GameDto>());

        _repository = new PostRepository(_dbContext, mapper, gameRepository.Object);
    }

    /// <summary>
    /// Seed a post authored by a character together with a set of positive
    /// reviews. The post's rating is the sum of the review sign values.
    /// </summary>
    private async Task<Guid> SeedRatedPostAsync(int positiveReviews, DateTimeOffset? reviewTime = null)
    {
        var characterId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var baseTime = reviewTime ?? DateTimeOffset.UtcNow;

        _dbContext.Characters.Add(new DbCharacter
        {
            CharacterId = characterId,
            GameId = _gameId,
            AuthorId = _userId,
            Status = CharacterStatus.Active,
            Name = "Char " + Guid.NewGuid().ToString("N")[..6],
            CreatedUtc = DateTimeOffset.UtcNow
        });

        _dbContext.Posts.Add(new DbPost
        {
            PostId = postId,
            RoomId = _roomId,
            CharacterId = characterId,
            AuthorId = _userId,
            GameText = "text",
            CreatedUtc = DateTimeOffset.UtcNow
        });

        for (var i = 0; i < positiveReviews; i++)
        {
            _dbContext.PostReviews.Add(new DbPostReview
            {
                PostReviewId = Guid.NewGuid(),
                PostId = postId,
                AuthorId = _userId,
                PostAuthorId = _userId,
                GameId = _gameId,
                SignValue = 1,
                CreatedUtc = baseTime.AddSeconds(i)
            });
        }

        await _dbContext.SaveChangesAsync();
        return postId;
    }

    private async Task SeedGameAndRoomAsync()
    {
        _dbContext.Users.Add(new DbUser
        {
            UserId = _userId,
            Username = "master",
            Email = "master@example.com",
            PasswordHash = "hash",
            Salt = "salt"
        });
        _dbContext.Games.Add(new DbGame
        {
            GameId = _gameId,
            PublicId = "abcde",
            Title = "Test Game",
            MasterId = _userId,
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved
        });
        _dbContext.Rooms.Add(new DbRoom
        {
            RoomId = _roomId,
            GameId = _gameId,
            RoomNumber = 1,
            Title = "Room",
            AccessType = RoomAccessType.Open,
            OrderNumber = 1
        });
        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task DefaultSortRatedPostsByRatingDescending()
    {
        await SeedGameAndRoomAsync();
        var lowRatedPost = await SeedRatedPostAsync(positiveReviews: 1);
        var highRatedPost = await SeedRatedPostAsync(positiveReviews: 3);

        // No SortBy → repository default must be rating desc (doc 4.2.3.5.6).
        var (posts, total) = await _repository.GetRated(new PostsQuery { Take = 10 });

        var ordered = posts.ToList();
        total.Should().Be(2);
        ordered.Should().HaveCount(2);
        ordered[0].Id.Should().Be(highRatedPost);
        ordered[0].Rating.Should().Be(3);
        ordered[1].Id.Should().Be(lowRatedPost);
        ordered[1].Rating.Should().Be(1);
    }

    [Fact]
    public async Task SortByLastReviewOrdersByMostRecentReview()
    {
        await SeedGameAndRoomAsync();
        // The higher-rated post was reviewed earlier; the lower-rated post got
        // a more recent review. lastreview must invert the rating-desc order.
        var highRatedOlderReview = await SeedRatedPostAsync(
            positiveReviews: 3, reviewTime: DateTimeOffset.UtcNow.AddHours(-1));
        var lowRatedNewerReview = await SeedRatedPostAsync(
            positiveReviews: 1, reviewTime: DateTimeOffset.UtcNow);

        var (posts, _) = await _repository.GetRated(new PostsQuery { Take = 10, SortBy = "lastreview" });

        var ordered = posts.ToList();
        ordered.Should().HaveCount(2);
        ordered[0].Id.Should().Be(lowRatedNewerReview);
        ordered[1].Id.Should().Be(highRatedOlderReview);
    }
}
