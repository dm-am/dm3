using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Subscriptions;
using DM.Domain.Core.Uploads;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;
using DbCharacter = DM.Infrastructure.Persistence.Entities.Game.Characters.Character;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbGameTag = DM.Infrastructure.Persistence.Entities.Game.Links.GameTag;
using DbLike = DM.Infrastructure.Persistence.Entities.Shared.Like;
using DbSubscription = DM.Infrastructure.Persistence.Entities.Subscriptions.Subscription;
using DbUpload = DM.Infrastructure.Persistence.Entities.Shared.Upload;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// Three rules that used to hold only while requests did not overlap: one subscription per
/// (subscriber, target), one live like per (entity, reader), and one live portrait per
/// character.
/// </summary>
/// <remarks>
/// All three writes are check-then-insert, and a check cannot see the request running
/// beside it. None of the duplicates was something the person who caused it could undo:
/// unsubscribing removes one row per click while the button reads as subscribed
/// either way, a duplicated like inflates the counter and the award metric of somebody
/// else, and a second portrait took the whole room's post list down with it.
/// A rule about rows that do not exist yet can only be stated in the schema, so
/// what is asserted here is that the refusal comes from there — the second insert
/// reaches the database and the database is what says no.
/// </remarks>
public class SchemaUniquenessShould : IntegrationTestBase
{
    public SchemaUniquenessShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task RefuseASecondSubscriptionOnTheSameTarget()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var subscriberId = await AddUserAsync(dbContext);
        var targetId = Guid.NewGuid();

        dbContext.Subscriptions.Add(NewSubscription(subscriberId, targetId));
        await dbContext.SaveChangesAsync();

        dbContext.Subscriptions.Add(NewSubscription(subscriberId, targetId));
        Func<Task> second = () => dbContext.SaveChangesAsync();

        var refusal = await second.Should().ThrowAsync<DbUpdateException>(
            "one subscriber follows one target once, whatever identifier the row carries");
        refusal.And.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be("23505", "that is unique_violation");
    }

    /// <summary>
    /// The write answers the refusal instead of forwarding it.
    /// </summary>
    /// <remarks>
    /// The index turns the loser of the race from an extra row into an error, and
    /// an error is the worse of the two: the reader asked to subscribe and is
    /// subscribed. All three subscribe services go through this one insert, so the
    /// answer belongs here and not in each of them.
    /// </remarks>
    [Fact]
    public async Task AnswerARacingSubscribeWithTheRowThatWon()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<ISubscriptionRepository>();

        var subscriberId = await AddUserAsync(dbContext);
        var targetId = Guid.NewGuid();

        var winner = await repository.CreateAsync(NewSubscriptionRequest(subscriberId, targetId));
        // Another identifier, the same triple: the insert the loser issues after
        // its own check came back empty.
        var loser = await repository.CreateAsync(NewSubscriptionRequest(subscriberId, targetId));

        loser.Id.Should().Be(winner.Id,
            "the second request is told about the subscription that exists, not about the collision");
        (await dbContext.Subscriptions.CountAsync(
                s => s.SubscriberId == subscriberId && s.TargetId == targetId))
            .Should().Be(1);
    }

    /// <summary>
    /// Refuses a second live like of one entity by one reader, and keeps accepting the next
    /// one after it is withdrawn.
    /// </summary>
    /// <remarks>
    /// Liking is check-then-insert over a loaded navigation, so two overlapping clicks both
    /// find nothing and both insert. The counter then reports one person twice, and the same
    /// duplicate reaches the LikesReceived metric, where somebody else's double click moves the
    /// author towards an award. Partial on live rows for the reason the portrait index is:
    /// unliking sets IsRemoved and liking again has to be allowed.
    /// </remarks>
    [Fact]
    public async Task RefuseASecondLiveLikeOfOneEntityByOneReader()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var readerId = await AddUserAsync(dbContext);
        var entityId = Guid.NewGuid();

        var first = NewLike(readerId, entityId);
        dbContext.Likes.Add(first);
        await dbContext.SaveChangesAsync();

        var second = NewLike(readerId, entityId);
        dbContext.Likes.Add(second);
        Func<Task> duplicate = () => dbContext.SaveChangesAsync();

        var refusal = await duplicate.Should().ThrowAsync<DbUpdateException>(
            "one reader likes one entity once, whatever identifier the row carries");
        refusal.And.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be("23505", "that is unique_violation");

        dbContext.Entry(second).State = EntityState.Detached;
        first.IsRemoved = true;
        await dbContext.SaveChangesAsync();

        dbContext.Likes.Add(second);
        Func<Task> afterUnlike = () => dbContext.SaveChangesAsync();

        await afterUnlike.Should().NotThrowAsync(
            "withdrawing a like is what frees the reader to leave another one");
    }

    /// <summary>
    /// Refuses the second live portrait and keeps accepting the replacement.
    /// </summary>
    /// <remarks>
    /// Both halves decide the index. A rule that only refused would be satisfied by
    /// a total unique index, and a total one would refuse every portrait a
    /// character ever changes: the row it replaces stays in the table until the
    /// orphan sweeper drops it, which is a day later at the earliest.
    /// </remarks>
    [Fact]
    public async Task RefuseASecondLivePortraitOfOneCharacter()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var (userId, characterId) = await AddCharacterAsync(dbContext);

        var current = NewPortrait(userId, characterId, DateTimeOffset.UtcNow.AddHours(-1));
        dbContext.Uploads.Add(current);
        await dbContext.SaveChangesAsync();

        var replacement = NewPortrait(userId, characterId, DateTimeOffset.UtcNow);
        dbContext.Uploads.Add(replacement);
        Func<Task> second = () => dbContext.SaveChangesAsync();

        var refusal = await second.Should().ThrowAsync<DbUpdateException>(
            "two live rows pointing at one character are two answers to one question");
        refusal.And.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be("23505", "that is unique_violation");

        dbContext.Entry(replacement).State = EntityState.Detached;
        current.IsRemoved = true;
        current.DeletedUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync();

        dbContext.Uploads.Add(replacement);
        Func<Task> afterRetirement = () => dbContext.SaveChangesAsync();

        await afterRetirement.Should().NotThrowAsync(
            "retiring the current portrait is what frees the slot for the next one");
    }

    /// <summary>
    /// The rule is the character's, and only the character's.
    /// </summary>
    /// <remarks>
    /// The insert retires the previous row for a portrait because the schema leaves
    /// it no choice. A user avatar becomes current when the profile is saved and
    /// the superseded rows are collected then, and a post is meant to carry several
    /// attachments, so an insert that retired on every type would break both without
    /// any index refusing anything.
    /// </remarks>
    [Fact]
    public async Task LeaveASecondAvatarOfOneUserAlone()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var uploads = scope.ServiceProvider.GetRequiredService<IUploadRepository>();

        var userId = await AddUserAsync(dbContext);

        await uploads.AddAsync(NewAvatarRequest(userId, DateTimeOffset.UtcNow.AddHours(-1)));
        await uploads.AddAsync(NewAvatarRequest(userId, DateTimeOffset.UtcNow));

        (await dbContext.Uploads.CountAsync(u => u.TargetUserId == userId))
            .Should().Be(2, "the profile save retires a superseded avatar, the upload does not");
    }

    /// <summary>
    /// A user of this test's own: the fixture database is shared and seeded, so a
    /// fixed identifier would depend on whoever else wrote one.
    /// </summary>
    private static async Task<Guid> AddUserAsync(DmDbContext dbContext)
    {
        var userId = Guid.NewGuid();
        dbContext.Users.Add(new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed, so the id is
            // truncated rather than used whole.
            Username = $"uniq{userId:N}"[..20],
            Email = $"{userId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
        });
        await dbContext.SaveChangesAsync();
        return userId;
    }

    /// <summary>A character with the game and the master it needs to exist.</summary>
    private static async Task<(Guid UserId, Guid CharacterId)> AddCharacterAsync(DmDbContext dbContext)
    {
        var userId = await AddUserAsync(dbContext);
        var gameId = Guid.NewGuid();
        var characterId = Guid.NewGuid();

        dbContext.Games.Add(new DbGame
        {
            GameId = gameId,
            PublicId = Guid.NewGuid().ToString("N")[..10],
            Title = "Game for the portrait uniqueness check",
            MasterId = userId,
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
        });
        dbContext.Characters.Add(new DbCharacter
        {
            CharacterId = characterId,
            GameId = gameId,
            AuthorId = userId,
            Status = CharacterStatus.Active,
            Name = "Char " + Guid.NewGuid().ToString("N")[..6],
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        return (userId, characterId);
    }

    /// <summary>
    /// One tag is on one game once.
    /// </summary>
    /// <remarks>
    /// The required-tag filter of the game catalogue counts the rows a game has
    /// among the tags asked for and compares that count to how many were asked
    /// for, so a game carrying one tag twice answered a query for two tags it
    /// holds one of. The write side deduplicates its input and computes a
    /// difference on update, but neither can see the request running beside it —
    /// the rule is about rows that do not exist yet, so it belongs to the schema.
    ///
    /// The other two finding of this shape, subscriptions and likes, were given a
    /// fact each; this one was closed on the index alone and left with the
    /// migration-versus-snapshot check named as its gate, which agrees with any
    /// index the model happens to declare, unique or not.
    /// </remarks>
    [Fact]
    public async Task RefuseTheSameTagTwiceOnOneGame()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var masterId = await AddUserAsync(dbContext);
        var gameId = Guid.NewGuid();
        dbContext.Games.Add(new DbGame
        {
            GameId = gameId,
            PublicId = Guid.NewGuid().ToString("N")[..10],
            Title = "Game for the tag uniqueness check",
            MasterId = masterId,
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
        });
        await dbContext.SaveChangesAsync();

        // A tag of the seeded catalogue: a new one would need a tag group of its own,
        // and which tag it is has nothing to do with the rule.
        var tagId = await dbContext.Tags.Select(tag => tag.TagId).FirstAsync();

        dbContext.GameTags.Add(new DbGameTag
        {
            GameTagId = Guid.NewGuid(),
            GameId = gameId,
            TagId = tagId,
        });
        await dbContext.SaveChangesAsync();

        dbContext.GameTags.Add(new DbGameTag
        {
            GameTagId = Guid.NewGuid(),
            GameId = gameId,
            TagId = tagId,
        });
        Func<Task> second = () => dbContext.SaveChangesAsync();

        var refusal = await second.Should().ThrowAsync<DbUpdateException>(
            "the required-tag filter counts rows, so a duplicate row answers a query for a " +
            "tag the game does not carry");
        refusal.And.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be("23505", "that is unique_violation");
    }

    private static DbLike NewLike(Guid userId, Guid entityId) => new()
    {
        LikeId = Guid.NewGuid(),
        UserId = userId,
        EntityId = entityId,
        EntityType = LikeEntityType.Topic,
        IsRemoved = false,
    };

    private static DbSubscription NewSubscription(Guid subscriberId, Guid targetId) => new()
    {
        SubscriptionId = Guid.NewGuid(),
        SubscriberId = subscriberId,
        TargetType = SubscriptionTargetType.Game,
        TargetId = targetId,
        Settings = SubscriptionSettings.GameReaderDefault,
        CreatedUtc = DateTimeOffset.UtcNow,
    };

    private static CreateSubscription NewSubscriptionRequest(Guid subscriberId, Guid targetId) => new()
    {
        SubscriptionId = Guid.NewGuid(),
        SubscriberId = subscriberId,
        TargetType = SubscriptionTargetType.Game,
        TargetId = targetId,
        Settings = SubscriptionSettings.GameReaderDefault,
        CreatedUtc = DateTimeOffset.UtcNow,
    };

    private static DbUpload NewPortrait(Guid userId, Guid characterId, DateTimeOffset createdUtc)
    {
        var objectKey = $"characters/{Guid.NewGuid():N}.png";
        return new DbUpload
        {
            UploadId = Guid.NewGuid(),
            UserId = userId,
            TargetCharacterId = characterId,
            Type = UploadType.CharacterAvatar,
            Status = UploadStatus.Confirmed,
            ContentType = "image/png",
            SizeBytes = 1024,
            ObjectKey = objectKey,
            FilePath = $"https://cdn.example/{objectKey}",
            CreatedUtc = createdUtc,
            IsRemoved = false,
        };
    }

    private static NewUpload NewAvatarRequest(Guid userId, DateTimeOffset createdUtc)
    {
        var objectKey = $"avatars/{Guid.NewGuid():N}.png";
        return new NewUpload
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TargetId = userId,
            Type = UploadType.UserAvatar,
            Status = UploadStatus.Confirmed,
            FileName = "avatar.png",
            ContentType = "image/png",
            SizeBytes = 1024,
            ObjectKey = objectKey,
            Original = true,
            Url = $"https://cdn.example/{objectKey}",
            CreatedUtc = createdUtc,
            ConfirmedUtc = createdUtc,
        };
    }
}
