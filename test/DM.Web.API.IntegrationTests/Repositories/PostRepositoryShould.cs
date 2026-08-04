using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Content;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Uploads;
using DM.Domain.Game.Features.Posts;
using DM.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using CreatePostEntity = DM.Domain.Game.Features.Games.CreatePostEntity;
using UpdatePostEntity = DM.Domain.Game.Features.Games.UpdatePostEntity;
using DbCharacter = DM.Infrastructure.Persistence.Entities.Game.Characters.Character;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbGameAssistant = DM.Infrastructure.Persistence.Entities.Game.Links.GameAssistant;
using DbPost = DM.Infrastructure.Persistence.Entities.Game.Posts.Post;
using DbPostReview = DM.Infrastructure.Persistence.Entities.Game.PostReview;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;
using DbSubscription = DM.Infrastructure.Persistence.Entities.Subscriptions.Subscription;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// Runs against the container Postgres. The ordering under test is produced by
/// a GroupBy the InMemory provider cannot translate, so the version of this
/// test that ran there had to stub the hydration step out — which is to say the
/// production query was never executed by anything. Here it is, joins included.
/// </summary>
public class PostRepositoryShould : IntegrationTestBase
{
    public PostRepositoryShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task DefaultSortRatedPostsByRatingDescending()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPostRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var context = await AddGameWithRoomAsync(dbContext);
        var lowRated = await AddRatedPostAsync(dbContext, context, positiveReviews: 1);
        var highRated = await AddRatedPostAsync(dbContext, context, positiveReviews: 3);

        // No SortBy: the repository default must be rating desc (doc 4.2.3.5.6).
        // Scoped to this test's own game — the fixture database is seeded and
        // shared, so an unscoped query would rank against everyone else's posts.
        var (posts, total) = await repository.GetRated(new PostsQuery
        {
            Take = 10,
            GameId = context.GameId,
        }, Guid.Empty);

        var ordered = posts.ToList();
        total.Should().Be(2);
        ordered.Should().HaveCount(2);
        ordered[0].Id.Should().Be(highRated);
        ordered[0].Rating.Should().Be(3);
        ordered[1].Id.Should().Be(lowRated);
        ordered[1].Rating.Should().Be(1);
    }

    [Fact]
    public async Task SortByLastReviewOrdersByMostRecentReview()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPostRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var context = await AddGameWithRoomAsync(dbContext);
        // The higher-rated post was reviewed earlier; the lower-rated one got a
        // more recent review. lastreview must invert the rating-desc order.
        var highRatedOlderReview = await AddRatedPostAsync(
            dbContext, context, positiveReviews: 3, reviewTime: DateTimeOffset.UtcNow.AddHours(-1));
        var lowRatedNewerReview = await AddRatedPostAsync(
            dbContext, context, positiveReviews: 1, reviewTime: DateTimeOffset.UtcNow);

        var (posts, _) = await repository.GetRated(new PostsQuery
        {
            Take = 10,
            SortBy = "lastreview",
            GameId = context.GameId,
        }, Guid.Empty);

        var ordered = posts.ToList();
        ordered.Should().HaveCount(2);
        ordered[0].Id.Should().Be(lowRatedNewerReview);
        ordered[1].Id.Should().Be(highRatedOlderReview);
    }

    /// <summary>
    /// "Лучший пост недели" is the rating board of ONE week, not of all time.
    /// </summary>
    /// <remarks>
    /// The homepage block sends sortBy=rating together with createdFromUtc=Monday,
    /// and the window has to cut on the POST's creation date. The seed used to
    /// place its top-rated filler on posts written days before that boundary and
    /// the block showed the filler instead of the showcase post — a failure the
    /// sort tests above cannot see, because the sort was never wrong.
    /// </remarks>
    [Fact]
    public async Task KeepPostsCreatedBeforeTheWeekOutOfTheWeeklyRating()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPostRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        // The same boundary the client (getWeekStartUtc) and the seeder
        // (DataSeeder.WeekStartUtc) compute: Monday 00:00 UTC.
        var now = DateTimeOffset.UtcNow;
        var weekStart = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero)
            .AddDays(-(((int)now.DayOfWeek + 6) % 7));

        var context = await AddGameWithRoomAsync(dbContext);
        // Higher rated, but written before the week started.
        var lastWeek = await AddRatedPostAsync(
            dbContext, context, positiveReviews: 12,
            postCreatedUtc: weekStart.AddSeconds(-1));
        // The showcase post: fewer reviews, written inside the window.
        var thisWeek = await AddRatedPostAsync(
            dbContext, context, positiveReviews: 7,
            postCreatedUtc: weekStart);

        var (posts, total) = await repository.GetRated(new PostsQuery
        {
            Take = 10,
            SortBy = "rating",
            HasReviews = true,
            CreatedFromUtc = weekStart,
            GameId = context.GameId,
        }, Guid.Empty);

        var ordered = posts.ToList();
        total.Should().Be(1);
        ordered.Should().ContainSingle()
            .Which.Id.Should().Be(thisWeek, "the week's best is the best OF THE WEEK");
        ordered.Should().NotContain(p => p.Id == lastWeek);
    }

    [Fact]
    public async Task ReportTheViewerAsAReaderOfTheGameTheySubscribeTo()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPostRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var context = await AddGameWithRoomAsync(dbContext);
        await AddRatedPostAsync(dbContext, context, positiveReviews: 1);
        var subscriberId = await AddSubscriberAsync(dbContext, context.GameId);

        var (forSubscriber, _) = await repository.GetRated(new PostsQuery
        {
            Take = 10,
            GameId = context.GameId,
        }, subscriberId);

        // The attached game is what GameLink builds its tooltip from, and its
        // participation list is derived from this flag. The feed itself is public,
        // so the game is loaded anonymously — filling the flag is the only thing
        // the viewer's identity is used for here.
        forSubscriber.Single().Room!.Game!.IsViewerSubscriber.Should().BeTrue();

        var (forStranger, _) = await repository.GetRated(new PostsQuery
        {
            Take = 10,
            GameId = context.GameId,
        }, Guid.NewGuid());

        forStranger.Single().Room!.Game!.IsViewerSubscriber.Should().BeFalse();

        var (forGuest, _) = await repository.GetRated(new PostsQuery
        {
            Take = 10,
            GameId = context.GameId,
        }, Guid.Empty);

        // A guest subscribes to nothing, and no lookup is made for them.
        forGuest.Single().Room!.Game!.IsViewerSubscriber.Should().BeFalse();
    }

    /// <summary>
    /// A user subscribed to the game, so the feed has somebody to report as a
    /// reader.
    /// </summary>
    private static async Task<Guid> AddSubscriberAsync(DmDbContext dbContext, Guid gameId)
    {
        var subscriberId = Guid.NewGuid();
        dbContext.Users.Add(new DbUser
        {
            UserId = subscriberId,
            Username = $"sub{subscriberId:N}"[..20],
            Email = $"{subscriberId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
        });
        dbContext.Subscriptions.Add(new DbSubscription
        {
            SubscriptionId = Guid.NewGuid(),
            SubscriberId = subscriberId,
            TargetType = SubscriptionTargetType.Game,
            TargetId = gameId,
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();
        return subscriberId;
    }

    /// <summary>
    /// A room shows the portrait the character has now, not the one it replaced.
    /// </summary>
    /// <remarks>
    /// The batch load keyed a dictionary on the target character, so a second live
    /// upload pointing at the same character made ToDictionaryAsync throw on the
    /// duplicate key — and that read is on all three post paths, so every member of
    /// the room got a 500 until somebody edited rows by hand. The pair is refused by
    /// the schema now, and the second upload here is legal only because the insert
    /// retires the row it replaces in the same transaction: it goes through the
    /// repository the endpoint calls, so a unique violation is what this test would
    /// report if that retirement were dropped.
    /// </remarks>
    [Fact]
    public async Task ShowThePortraitThatReplacedTheOneBeforeIt()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPostRepository>();
        var uploads = scope.ServiceProvider.GetRequiredService<IUploadRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var context = await AddGameWithRoomAsync(dbContext);
        var characterId = await AddPostByNewCharacterAsync(dbContext, context);

        var older = await AddCharacterPortraitAsync(
            uploads, context.UserId, characterId, DateTimeOffset.UtcNow.AddHours(-1));
        var newer = await AddCharacterPortraitAsync(
            uploads, context.UserId, characterId, DateTimeOffset.UtcNow);

        var posts = (await repository.Get(
            context.RoomId,
            new PagingData(new PagingQuery { Skip = 0, Take = 10 }, 10, 1),
            context.UserId)).ToList();

        posts.Should().ContainSingle();
        posts[0].Character.Picture.SourceObjectKey.Should().Be(newer,
            "the current portrait is the live upload pointing at the character");
        posts[0].Character.Picture.SourceObjectKey.Should().NotBe(older);
    }

    /// <summary>
    /// Reading a room's posts has to translate to SQL at all.
    /// </summary>
    /// <remarks>
    /// The DbPost projection built the lead list as
    /// <c>new[] { master }.Concat(assistants.Select(...))</c>. Npgsql cannot
    /// correlate a collection subquery concatenated onto an in-memory array, and it
    /// refuses the whole query rather than the one member, so every read that went
    /// through this projection answered 500 — the room page of every game, and the
    /// single-post read with it. Nothing covered either path, so it stayed.
    ///
    /// The assistant is what makes this a gate rather than a smoke test: with the
    /// master alone the broken shape and the fixed one agree.
    /// </remarks>
    [Fact]
    public async Task ProjectTheGameLeadsOfEveryPostInARoom()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPostRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var context = await AddGameWithRoomAsync(dbContext);
        await AddPostByNewCharacterAsync(dbContext, context);
        var assistantId = await AddAssistantAsync(dbContext, context.GameId);

        var posts = (await repository.Get(
            context.RoomId,
            new PagingData(new PagingQuery { Skip = 0, Take = 10 }, 10, 1),
            context.UserId)).ToList();

        posts.Should().ContainSingle();
        posts[0].GameLeadUserIds.Should().BeEquivalentTo(new[] { context.UserId, assistantId },
            "the leads are the master and the assistants, and mentors are not leads");

        // The single-post path shares the projection, so it shares the failure.
        var single = await repository.Get(posts[0].Id, context.UserId);
        single.Should().NotBeNull();
        single!.GameLeadUserIds.Should().BeEquivalentTo(new[] { context.UserId, assistantId });
    }

    /// <summary>
    /// The addressee snapshot has to reach its column and come back from it.
    /// </summary>
    /// <remarks>
    /// The column existed, the render path read it and the save path wrote
    /// nothing into it, so the addressee rule never fired for anybody. Everything
    /// upstream of this write can be correct and the rule still dead, which is
    /// exactly how it stayed unnoticed — hence a gate on the write itself, on
    /// both paths that perform it.
    /// </remarks>
    [Fact]
    public async Task StoreThePrivateAddresseeSnapshotOnBothWritePaths()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPostRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var context = await AddGameWithRoomAsync(dbContext);
        var annaOwner = Guid.NewGuid();
        var borisOwner = Guid.NewGuid();

        var created = await repository.Create(new CreatePostEntity
        {
            PostId = Guid.NewGuid(),
            RoomId = context.RoomId,
            AuthorId = context.UserId,
            GameText = "[private=Анна]секрет[/private]",
            PrivateAddresseeSnapshotJson = PrivateAddresseeSnapshot.Build(
                "[private=Анна]секрет[/private]",
                new[] { new PrivateAddressee("Анна", annaOwner) }),
            CreatedUtc = DateTimeOffset.UtcNow,
        });

        PrivateAddresseeSnapshot.Parse(await ReadSnapshotAsync(dbContext, created.Id))
            .Should().ContainKey("Анна")
            .WhoseValue.Should().BeEquivalentTo(new[] { annaOwner });

        await repository.Update(new UpdatePostEntity
        {
            PostId = created.Id,
            GameText = "[private=Борис]секрет[/private]",
            PrivateAddresseeSnapshotJson = PrivateAddresseeSnapshot.Build(
                "[private=Борис]секрет[/private]",
                new[] { new PrivateAddressee("Борис", borisOwner) }),
        });

        var afterEdit = PrivateAddresseeSnapshot.Parse(await ReadSnapshotAsync(dbContext, created.Id));
        afterEdit.Should().ContainKey("Борис").WhoseValue.Should().BeEquivalentTo(new[] { borisOwner });
        afterEdit.Should().NotContainKey("Анна");
    }

    /// <summary>The stored snapshot of a post, read past the change tracker.</summary>
    private static Task<string> ReadSnapshotAsync(DmDbContext dbContext, Guid postId) =>
        dbContext.Posts
            .AsNoTracking()
            .Where(p => p.PostId == postId)
            .Select(p => p.PrivateAddresseeSnapshotJson)
            .SingleAsync();

    /// <summary>An assistant on the game, so the lead list has two entries.</summary>
    private static async Task<Guid> AddAssistantAsync(DmDbContext dbContext, Guid gameId)
    {
        var assistantId = Guid.NewGuid();

        dbContext.Users.Add(new DbUser
        {
            UserId = assistantId,
            Username = $"ast{assistantId:N}"[..20],
            Email = $"{assistantId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
        });
        dbContext.GameAssistants.Add(new DbGameAssistant
        {
            GameAssistantId = Guid.NewGuid(),
            GameId = gameId,
            UserId = assistantId,
            JoinedUtc = DateTimeOffset.UtcNow,
        });

        await dbContext.SaveChangesAsync();
        return assistantId;
    }

    private sealed record GameContext(Guid UserId, Guid GameId, Guid RoomId);

    /// <summary>A character in the game with one post in the room.</summary>
    private static async Task<Guid> AddPostByNewCharacterAsync(
        DmDbContext dbContext, GameContext context)
    {
        var characterId = Guid.NewGuid();

        dbContext.Characters.Add(new DbCharacter
        {
            CharacterId = characterId,
            GameId = context.GameId,
            AuthorId = context.UserId,
            Status = CharacterStatus.Active,
            Name = "Char " + Guid.NewGuid().ToString("N")[..6],
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        dbContext.Posts.Add(new DbPost
        {
            PostId = Guid.NewGuid(),
            RoomId = context.RoomId,
            CharacterId = characterId,
            AuthorId = context.UserId,
            GameText = "text",
            CreatedUtc = DateTimeOffset.UtcNow,
        });

        await dbContext.SaveChangesAsync();
        return characterId;
    }

    /// <summary>
    /// A character portrait written the way the upload endpoint writes it, so the
    /// retirement of the previous one is part of what is under test. Returns its
    /// object key.
    /// </summary>
    private static async Task<string> AddCharacterPortraitAsync(
        IUploadRepository uploads, Guid userId, Guid characterId, DateTimeOffset createdUtc)
    {
        var objectKey = $"characters/{Guid.NewGuid():N}.png";

        await uploads.AddAsync(new NewUpload
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TargetId = characterId,
            Type = UploadType.CharacterAvatar,
            Status = UploadStatus.Confirmed,
            FileName = "portrait.png",
            ContentType = "image/png",
            SizeBytes = 1024,
            ObjectKey = objectKey,
            Original = true,
            Url = $"https://cdn.example/{objectKey}",
            CreatedUtc = createdUtc,
            ConfirmedUtc = createdUtc,
        });

        return objectKey;
    }

    /// <summary>
    /// A fresh master, game and room per test: the fixture's database is shared
    /// and seeded, so fixed identifiers would collide.
    /// </summary>
    private static async Task<GameContext> AddGameWithRoomAsync(DmDbContext dbContext)
    {
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var roomId = Guid.NewGuid();

        dbContext.Users.Add(new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed, so the id is
            // truncated rather than used whole.
            Username = $"post{userId:N}"[..20],
            Email = $"{userId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
        });
        dbContext.Games.Add(new DbGame
        {
            GameId = gameId,
            PublicId = UniquePublicId(),
            Title = "Game for the rated-post ordering check",
            MasterId = userId,
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
        });
        dbContext.Rooms.Add(new DbRoom
        {
            RoomId = roomId,
            GameId = gameId,
            RoomNumber = 1,
            Title = "Room",
            AccessType = RoomAccessType.Open,
            OrderNumber = 1,
        });
        await dbContext.SaveChangesAsync();

        return new GameContext(userId, gameId, roomId);
    }

    /// <summary>
    /// A post authored by a character, with a set of positive reviews. The
    /// post's rating is the sum of the review sign values.
    /// </summary>
    private static async Task<Guid> AddRatedPostAsync(
        DmDbContext dbContext, GameContext context, int positiveReviews,
        DateTimeOffset? reviewTime = null, DateTimeOffset? postCreatedUtc = null)
    {
        var characterId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var baseTime = reviewTime ?? DateTimeOffset.UtcNow;

        dbContext.Characters.Add(new DbCharacter
        {
            CharacterId = characterId,
            GameId = context.GameId,
            AuthorId = context.UserId,
            Status = CharacterStatus.Active,
            Name = "Char " + Guid.NewGuid().ToString("N")[..6],
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        dbContext.Posts.Add(new DbPost
        {
            PostId = postId,
            RoomId = context.RoomId,
            CharacterId = characterId,
            AuthorId = context.UserId,
            GameText = "text",
            CreatedUtc = postCreatedUtc ?? DateTimeOffset.UtcNow,
        });

        // One reviewer per review. PostReviews is uniquely indexed on
        // (AuthorId, PostId) — a rating is a count of distinct people, not of
        // one person's clicks. The InMemory provider had no such index, so the
        // version of this test that ran there built every rating out of rows the
        // database would have refused.
        for (var i = 0; i < positiveReviews; i++)
        {
            var reviewerId = Guid.NewGuid();
            dbContext.Users.Add(new DbUser
            {
                UserId = reviewerId,
                Username = $"rev{reviewerId:N}"[..20],
                Email = $"{reviewerId:N}@example.com",
                PasswordHash = "hash",
                Salt = "salt",
            });
            dbContext.PostReviews.Add(new DbPostReview
            {
                PostReviewId = Guid.NewGuid(),
                PostId = postId,
                AuthorId = reviewerId,
                PostAuthorId = context.UserId,
                GameId = context.GameId,
                SignValue = 1,
                CreatedUtc = baseTime.AddSeconds(i),
            });
        }

        await dbContext.SaveChangesAsync();
        return postId;
    }

    /// <summary>
    /// A public id no other row holds. The column is NOT NULL and uniquely
    /// indexed — a constraint the InMemory provider did not have, which is why
    /// the version of this test that ran there could omit the value entirely.
    /// </summary>
    private static string UniquePublicId() => Guid.NewGuid().ToString("N")[..10];
}
