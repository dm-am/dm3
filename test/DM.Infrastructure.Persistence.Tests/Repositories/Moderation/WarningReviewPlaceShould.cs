using System;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Moderation.Features.Warnings;
using DM.Infrastructure.Persistence.Repositories.Moderation;
using AwesomeAssertions;
using Xunit;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;
using DbPost = DM.Infrastructure.Persistence.Entities.Game.Posts.Post;
using DbGameReview = DM.Infrastructure.Persistence.Entities.Game.GameReview;
using DbPostReview = DM.Infrastructure.Persistence.Entities.Game.PostReview;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Moderation;

public class WarningReviewPlaceShould
{
    private static readonly DateTimeOffset Written = new(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);

    private static readonly Guid AuthorId = Guid.Parse("6b1f0c22-0000-4000-8000-000000000001");
    private static readonly Guid ModeratorId = Guid.Parse("6b1f0c22-0000-4000-8000-000000000002");
    private static readonly Guid WarningId = Guid.Parse("6b1f0c22-0000-4000-8000-000000000021");
    private static readonly Guid GameId = Guid.Parse("6b1f0c22-0000-4000-8000-000000000031");
    private static readonly Guid RoomId = Guid.Parse("6b1f0c22-0000-4000-8000-000000000041");
    private static readonly Guid PostId = Guid.Parse("6b1f0c22-0000-4000-8000-000000000042");
    private static readonly Guid GameReviewId = Guid.Parse("6b1f0c22-0000-4000-8000-000000000043");
    private static readonly Guid PostReviewId = Guid.Parse("6b1f0c22-0000-4000-8000-000000000044");

    private const string Offending = "Исходный текст, за который вынесено предупреждение";

    private readonly string _databaseName = Guid.NewGuid().ToString();

    private static WarningEntityResolver Resolver(DmDbContext context) => new(context);

    private static async Task<WarningEntityState> Resolve(
        DmDbContext context, Guid entityId, WarningEntityType entityType)
    {
        var states = await Resolver(context).ResolveStates(
            [new WarningEntityRequest { WarningId = WarningId, EntityId = entityId, EntityType = entityType }]);
        return states[WarningId];
    }

    private DmDbContext SeededWithReviews(ModuleStatus status, RoomAccessType roomAccess)
    {
        var context = InMemoryStore.Open(_databaseName);
        context.Users.AddRange(InMemoryStore.User(AuthorId, "author"), InMemoryStore.User(ModeratorId, "moderator"));
        context.Games.Add(new DbGame
        {
            GameId = GameId,
            PublicId = "gamew",
            Title = "Тайны Древних",
            MasterId = ModeratorId,
            Status = status,
            PremoderationStatus = PremoderationStatus.Approved,
            DraftVisibility = DraftVisibility.Private,
            CommentsAccessMode = CommentsAccessMode.Public
        });
        context.Set<DbRoom>().Add(new DbRoom
        {
            RoomId = RoomId,
            GameId = GameId,
            RoomNumber = 1,
            Title = "Комната",
            AccessType = roomAccess
        });
        context.Set<DbPost>().Add(new DbPost
        {
            PostId = PostId,
            RoomId = RoomId,
            AuthorId = ModeratorId,
            GameText = "Пост",
            CreatedUtc = Written
        });
        context.Set<DbGameReview>().Add(new DbGameReview
        {
            GameReviewId = GameReviewId,
            AuthorId = AuthorId,
            GameId = GameId,
            Text = Offending,
            CreatedUtc = Written
        });
        context.Set<DbPostReview>().Add(new DbPostReview
        {
            PostReviewId = PostReviewId,
            AuthorId = AuthorId,
            PostId = PostId,
            PostAuthorId = ModeratorId,
            GameId = GameId,
            Text = Offending,
            CreatedUtc = Written
        });
        context.SaveChanges();
        return context;
    }

    [Theory]
    [InlineData(WarningEntityType.GameReview, "/game/gamew/reviews")]
    [InlineData(WarningEntityType.PostReview, "/game/gamew/post-reviews#review-6b1f0c22-0000-4000-8000-000000000044")]
    public async Task TakeAndPlaceTheTextOfAReview(WarningEntityType entityType, string url)
    {
        using var context = SeededWithReviews(ModuleStatus.Active, RoomAccessType.Open);
        var entityId = entityType == WarningEntityType.GameReview ? GameReviewId : PostReviewId;

        var snapshot = await Resolver(context).CaptureSnapshot(entityType, entityId);
        var state = await Resolve(context, entityId, entityType);

        snapshot.Should().Be(Offending);
        state.PlaceKind.Should().Be(WarningPlaceKind.Game);
        state.PlaceTitle.Should().Be("Тайны Древних");
        state.Url.Should().Be(url);
        state.CreatedUtc.Should().Be(Written);
        state.LogClosure.Should().BeNull("a guest reads an active game and its open room");
    }

    [Theory]
    [InlineData(ModuleStatus.Draft, RoomAccessType.Open, WarningEntityType.GameReview)]
    [InlineData(ModuleStatus.Draft, RoomAccessType.Open, WarningEntityType.PostReview)]
    [InlineData(ModuleStatus.Active, RoomAccessType.Private, WarningEntityType.PostReview)]
    public async Task CloseTheLogOverAReviewAGuestCannotRead(
        ModuleStatus status, RoomAccessType roomAccess, WarningEntityType entityType)
    {
        using var context = SeededWithReviews(status, roomAccess);
        var entityId = entityType == WarningEntityType.GameReview ? GameReviewId : PostReviewId;

        var state = await Resolve(context, entityId, entityType);

        state.LogClosure.Should().Be(WarningLogClosure.PlaceNotPublic);
        // The open log asks its viewer about the place by the read the place answers with.
        state.ClosedPlace.Should().Be(entityType == WarningEntityType.GameReview
            ? new ReadablePlace(ReadablePlaceKind.Game, GameId)
            : new ReadablePlace(ReadablePlaceKind.Post, PostId));
        state.ClosureForReader.Should().BeNull();
    }

    /// <summary>The review of a game in a closed room of it is open as the game is.</summary>
    [Fact]
    public async Task LeaveTheReviewOfAGameOpenWhateverItsRooms()
    {
        using var context = SeededWithReviews(ModuleStatus.Active, RoomAccessType.Private);

        (await Resolve(context, GameReviewId, WarningEntityType.GameReview)).LogClosure.Should().BeNull();
    }
}
