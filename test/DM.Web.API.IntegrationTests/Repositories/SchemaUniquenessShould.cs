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
using DbUsernameChangeRequest = DM.Infrastructure.Persistence.Entities.Account.UsernameChangeRequest;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// One subscription per (subscriber, target), one live like per (entity, reader), one live portrait per character,
/// one tag per game, one name change awaiting a moderator per account.
/// </summary>
/// <remarks>
/// Each write is check-then-insert, so the rule lives in the schema: the second insert reaches the database and the
/// database says no.
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
    /// <remarks>All three subscribe services go through this one insert, so the answer belongs here.</remarks>
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
    /// <remarks>Partial on live rows, as the portrait index is: unliking sets IsRemoved and liking again is allowed.</remarks>
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
    /// <remarks>A total index would refuse every replacement: the replaced row stays until the orphan sweeper drops it.</remarks>
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
    /// <remarks>A user avatar is collected when the profile is saved and a post carries several attachments.</remarks>
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
    /// <remarks>The required-tag filter compares a count of rows to the count of tags asked for.</remarks>
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

    /// <summary>
    /// Refuses a second name change request awaiting a moderator, and accepts the next one once the first is decided.
    /// </summary>
    /// <remarks>
    /// Partial on Status = 0: a total index would leave an account one request in its lifetime. Approvals stay out:
    /// one stops being in flight when its link runs out, and no index predicate can be written against the clock.
    /// </remarks>
    [Fact]
    public async Task RefuseASecondPendingNameChangeOfOneAccount()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var userId = await AddUserAsync(dbContext);

        var first = NewNameChange(userId);
        dbContext.UsernameChangeRequests.Add(first);
        await dbContext.SaveChangesAsync();

        var second = NewNameChange(userId);
        dbContext.UsernameChangeRequests.Add(second);
        Func<Task> duplicate = () => dbContext.SaveChangesAsync();

        var refusal = await duplicate.Should().ThrowAsync<DbUpdateException>(
            "two requests in flight are two answers a moderator gives one account");
        refusal.And.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be("23505", "that is unique_violation");

        dbContext.Entry(second).State = EntityState.Detached;
        first.Status = UsernameChangeRequestStatus.Rejected;
        first.ResolvedUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync();

        dbContext.UsernameChangeRequests.Add(second);
        Func<Task> afterDecision = () => dbContext.SaveChangesAsync();

        await afterDecision.Should().NotThrowAsync(
            "a decided request must not hold the slot of the next one");
    }

    private static DbLike NewLike(Guid userId, Guid entityId) => new()
    {
        LikeId = Guid.NewGuid(),
        UserId = userId,
        EntityId = entityId,
        EntityType = LikeEntityType.Topic,
        IsRemoved = false,
    };

    private static DbUsernameChangeRequest NewNameChange(Guid userId) => new()
    {
        RequestId = Guid.NewGuid(),
        UserId = userId,
        Reason = "Reason of the uniqueness check",
        Status = UsernameChangeRequestStatus.Pending,
        CreatedUtc = DateTimeOffset.UtcNow,
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
