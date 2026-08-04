using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Comments;
using DM.Domain.Community.Features.UserEndorsements;
using DM.Domain.Community.Features.WebsiteTestimonials;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Uploads;
using DM.Domain.Forum.Features.Comments;
using DM.Domain.Forum.Features.Topics;
using DM.Domain.Game.Features.Characters;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Posts;
using DM.Domain.Game.Features.Rooms;
using DM.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;
using DbBoard = DM.Infrastructure.Persistence.Entities.Forum.Board;
using DbCharacter = DM.Infrastructure.Persistence.Entities.Game.Characters.Character;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbPost = DM.Infrastructure.Persistence.Entities.Game.Posts.Post;
using DbPublication = DM.Infrastructure.Persistence.Entities.Blog.Publication;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;
using DbTopic = DM.Infrastructure.Persistence.Entities.Forum.Topic;
using DbUpload = DM.Infrastructure.Persistence.Entities.Shared.Upload;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using DbUserEndorsement = DM.Infrastructure.Persistence.Entities.Community.UserEndorsement;
using DbWebsiteTestimonial = DM.Infrastructure.Persistence.Entities.Community.WebsiteTestimonial;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// A soft-deleted row says who removed it and when.
/// </summary>
/// <remarks>
/// ISoftDeletable declares both columns and the schema carries them for twenty-five tables,
/// with a foreign key under the author. Most write paths set the flag alone, so moderation
/// could not answer "who deleted this topic" for anything but blogs — and inside the Comments
/// table the answer existed for a blog comment and not for a forum one, which makes a report
/// over that column wrong rather than incomplete.
///
/// Asserted against the row rather than against the call: the services were already handing
/// the identity to the blog comment repository, which dropped it on the floor, so a test that
/// only watched the call would have been green over the defect.
/// </remarks>
public class SoftDeleteAuditShould : IntegrationTestBase
{
    public SoftDeleteAuditShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task NameWhoRemovedATopic()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<ITopicRepository>();

        var moderatorId = await AddUserAsync(dbContext);
        var (_, topicId) = await AddBoardWithTopicAsync(dbContext);

        await repository.Delete(topicId, moderatorId);

        var topic = await Removed(dbContext.Topics, t => t.TopicId == topicId);
        topic.DeletedByUserId.Should().Be(moderatorId);
        topic.DeletedUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task NameWhoRemovedAForumComment()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<ITopicCommentRepository>();

        var moderatorId = await AddUserAsync(dbContext);
        var (_, topicId) = await AddBoardWithTopicAsync(dbContext);
        var commentId = await AddCommentAsync(dbContext, topicId);
        var deletedUtc = DateTimeOffset.UtcNow;

        await repository.Delete(new DeleteTopicCommentEntity
        {
            CommentId = commentId,
            TopicId = topicId,
            DeletedByUserId = moderatorId,
            DeletedUtc = deletedUtc,
        });

        var comment = await Removed(dbContext.Comments, c => c.CommentId == commentId);
        comment.DeletedByUserId.Should().Be(moderatorId);
        comment.DeletedUtc.Should().BeCloseTo(deletedUtc, TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// The same table, the other kind of comment: both answers or the column is unusable.
    /// </summary>
    [Fact]
    public async Task NameWhoRemovedABlogComment()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IBlogCommentRepository>();

        var moderatorId = await AddUserAsync(dbContext);
        var blogId = await AddBlogAsync(dbContext);
        var commentId = await AddCommentAsync(dbContext, blogId);
        var deletedUtc = DateTimeOffset.UtcNow;

        await repository.Delete(new DeleteBlogCommentEntity
        {
            CommentId = commentId,
            BlogId = blogId,
            DeletedByUserId = moderatorId,
            DeletedUtc = deletedUtc,
        });

        var comment = await Removed(dbContext.Comments, c => c.CommentId == commentId);
        comment.DeletedByUserId.Should().Be(moderatorId);
        comment.DeletedUtc.Should().BeCloseTo(deletedUtc, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task NameWhoRemovedAGamePostAndACharacterAndTheGame()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var posts = scope.ServiceProvider.GetRequiredService<IPostRepository>();
        var characters = scope.ServiceProvider.GetRequiredService<ICharacterRepository>();
        var games = scope.ServiceProvider.GetRequiredService<IGameRepository>();

        var masterId = await AddUserAsync(dbContext);
        var (gameId, roomId) = await AddGameWithRoomAsync(dbContext, masterId);
        var (characterId, postId) = await AddCharacterWithPostAsync(dbContext, gameId, roomId, masterId);

        await posts.Delete(postId, masterId);
        await characters.Delete(characterId, masterId);
        await games.Delete(gameId, masterId);

        var post = await Removed(dbContext.Posts, p => p.PostId == postId);
        post.DeletedByUserId.Should().Be(masterId);
        post.DeletedUtc.Should().NotBeNull();

        var character = await Removed(dbContext.Characters, c => c.CharacterId == characterId);
        character.DeletedByUserId.Should().Be(masterId);
        character.DeletedUtc.Should().NotBeNull();

        var game = await Removed(dbContext.Games, g => g.GameId == gameId);
        game.DeletedByUserId.Should().Be(masterId);
        game.DeletedUtc.Should().NotBeNull();
    }

    /// <summary>
    /// Withdrawing a like is a removal too, and it goes through ExecuteUpdate rather than the
    /// change tracker, so the columns have to be named in the statement itself.
    /// </summary>
    [Fact]
    public async Task NameWhoWithdrewALike()
    {
        var reader = CustomWebApplicationFactory.CreateTestUser();

        Guid publicationId;
        using (var scope = DatabaseFixture.Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
            publicationId = await AddPublicationAsync(dbContext);
        }

        var liked = await Client.SendAsync(
            CreateAuthenticatedRequest(HttpMethod.Post, $"/v1/publications/{publicationId}/likes", reader));
        liked.IsSuccessStatusCode.Should().BeTrue(await liked.Content.ReadAsStringAsync());

        var unliked = await Client.SendAsync(
            CreateAuthenticatedRequest(HttpMethod.Delete, $"/v1/publications/{publicationId}/likes", reader));
        unliked.IsSuccessStatusCode.Should().BeTrue(await unliked.Content.ReadAsStringAsync());

        using (var scope = DatabaseFixture.Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
            var like = await dbContext.Likes
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleAsync(l => l.EntityId == publicationId && l.UserId == reader.UserId);

            like.IsRemoved.Should().BeTrue();
            like.DeletedByUserId.Should().Be(reader.UserId,
                "a like is withdrawn by the reader who left it and by nobody else");
            like.DeletedUtc.Should().NotBeNull();
        }
    }

    /// <summary>
    /// The four human-driven removals the first pass left behind.
    /// </summary>
    /// <remarks>
    /// Each of these is somebody pressing delete: the master removing a room, a
    /// moderator removing a testimonial about the site, an owner or a moderator
    /// removing an uploaded file, an author withdrawing an endorsement. All four
    /// write the flag and had nothing under the author column, and three of them
    /// were not in the list of what was left — the endorsement path even wrote
    /// ModifiedByUserId beside the empty deletion pair, which reads as an answer
    /// and is not one.
    /// </remarks>
    [Fact]
    public async Task NameWhoRemovedARoomATestimonialAnUploadAndAnEndorsement()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var rooms = scope.ServiceProvider.GetRequiredService<IRoomRepository>();
        var testimonials = scope.ServiceProvider.GetRequiredService<IWebsiteTestimonialRepository>();
        var uploads = scope.ServiceProvider.GetRequiredService<IUploadRepository>();
        var endorsements = scope.ServiceProvider.GetRequiredService<IUserEndorsementRepository>();

        var actorId = await AddUserAsync(dbContext);
        var (_, roomId) = await AddGameWithRoomAsync(dbContext, actorId);

        await rooms.Delete(roomId, actorId);

        var room = await Removed(dbContext.Rooms, r => r.RoomId == roomId);
        room.DeletedByUserId.Should().Be(actorId);
        room.DeletedUtc.Should().NotBeNull();

        var testimonialId = await AddTestimonialAsync(dbContext, actorId);
        await testimonials.Delete(testimonialId, actorId);

        var testimonial = await Removed(
            dbContext.WebsiteTestimonials, t => t.WebsiteTestimonialId == testimonialId);
        testimonial.DeletedByUserId.Should().Be(actorId);
        testimonial.DeletedUtc.Should().NotBeNull();

        var uploadId = await AddUploadAsync(dbContext, actorId);
        await uploads.SoftDeleteAsync(uploadId, actorId, DateTimeOffset.UtcNow);

        var upload = await Removed(dbContext.Uploads, u => u.UploadId == uploadId);
        upload.DeletedByUserId.Should().Be(actorId,
            "deleting a file that is not yours is a moderation action, and the request " +
            "already holds the identity that took it");
        upload.DeletedUtc.Should().NotBeNull();

        var targetId = await AddUserAsync(dbContext);
        var endorsementId = await AddEndorsementAsync(dbContext, actorId, targetId);
        await endorsements.UpdateAsync(new UpdateUserEndorsementEntity(
            endorsementId,
            IsRemoved: true,
            DeletedUtc: DateTimeOffset.UtcNow,
            DeletedByUserId: actorId));

        var endorsement = await Removed(
            dbContext.UserEndorsements, e => e.UserEndorsementId == endorsementId);
        endorsement.DeletedByUserId.Should().Be(actorId);
        endorsement.DeletedUtc.Should().NotBeNull();
    }

    private static async Task<Guid> AddTestimonialAsync(DmDbContext dbContext, Guid authorId)
    {
        var testimonialId = Guid.NewGuid();
        dbContext.WebsiteTestimonials.Add(new DbWebsiteTestimonial
        {
            WebsiteTestimonialId = testimonialId,
            AuthorId = authorId,
            Text = "Audit testimonial",
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();
        return testimonialId;
    }

    private static async Task<Guid> AddUploadAsync(DmDbContext dbContext, Guid ownerId)
    {
        var uploadId = Guid.NewGuid();
        var objectKey = $"audit/{uploadId:N}.png";
        dbContext.Uploads.Add(new DbUpload
        {
            UploadId = uploadId,
            UserId = ownerId,
            // The schema requires the target that matches the type (CK_Uploads_TypedTarget).
            TargetUserId = ownerId,
            Type = UploadType.UserAvatar,
            Status = UploadStatus.Confirmed,
            ContentType = "image/png",
            SizeBytes = 1024,
            ObjectKey = objectKey,
            FilePath = $"https://cdn.example/{objectKey}",
            CreatedUtc = DateTimeOffset.UtcNow,
            IsRemoved = false,
        });
        await dbContext.SaveChangesAsync();
        return uploadId;
    }

    private static async Task<Guid> AddEndorsementAsync(
        DmDbContext dbContext, Guid authorId, Guid targetUserId)
    {
        var endorsementId = Guid.NewGuid();
        dbContext.UserEndorsements.Add(new DbUserEndorsement
        {
            UserEndorsementId = endorsementId,
            AuthorId = authorId,
            TargetUserId = targetUserId,
            Text = "Audit endorsement",
            CreatedUtc = DateTimeOffset.UtcNow,
            IsRemoved = false,
        });
        await dbContext.SaveChangesAsync();
        return endorsementId;
    }

    private static async Task<TEntity> Removed<TEntity>(
        DbSet<TEntity> set, System.Linq.Expressions.Expression<Func<TEntity, bool>> predicate)
        where TEntity : class =>
        await set.IgnoreQueryFilters().AsNoTracking().SingleAsync(predicate);

    private static async Task<Guid> AddUserAsync(DmDbContext dbContext)
    {
        var userId = Guid.NewGuid();
        dbContext.Users.Add(new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed, so the id is truncated.
            Username = $"soft{userId:N}"[..20],
            Email = $"{userId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
        });
        await dbContext.SaveChangesAsync();
        return userId;
    }

    private static async Task<(Guid BoardId, Guid TopicId)> AddBoardWithTopicAsync(
        DmDbContext dbContext, Guid? authorId = null)
    {
        var ownerId = authorId ?? await AddUserAsync(dbContext);
        var boardId = Guid.NewGuid();
        var topicId = Guid.NewGuid();

        dbContext.Boards.Add(new DbBoard
        {
            BoardId = boardId,
            Title = $"Audit board {boardId:N}",
            Alias = $"audit-{boardId:N}",
            Order = 0,
            ViewPolicy = BoardAccessPolicy.Guest,
            CreateTopicPolicy = BoardAccessPolicy.Guest,
        });
        dbContext.Topics.Add(new DbTopic
        {
            TopicId = topicId,
            BoardId = boardId,
            TopicNumber = 1,
            AuthorId = ownerId,
            Title = "Audit topic",
            Text = "text",
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        return (boardId, topicId);
    }

    private static async Task<Guid> AddBlogAsync(DmDbContext dbContext)
    {
        var authorId = await AddUserAsync(dbContext);
        var blogId = Guid.NewGuid();

        dbContext.Blogs.Add(new DbBlog
        {
            BlogId = blogId,
            AuthorId = authorId,
            Title = "Audit blog",
            Description = string.Empty,
            CreatedUtc = DateTimeOffset.UtcNow,
            PublicId = Guid.NewGuid().ToString("N")[..10],
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
        });
        await dbContext.SaveChangesAsync();

        return blogId;
    }

    /// <summary>
    /// A published publication of an active blog, which is what a reader can like.
    /// </summary>
    private static async Task<Guid> AddPublicationAsync(DmDbContext dbContext)
    {
        var blogId = await AddBlogAsync(dbContext);
        var authorId = await dbContext.Blogs
            .Where(b => b.BlogId == blogId)
            .Select(b => b.AuthorId)
            .SingleAsync();
        var publicationId = Guid.NewGuid();

        dbContext.Publications.Add(new DbPublication
        {
            PublicationId = publicationId,
            BlogId = blogId,
            PublicationNumber = 1,
            AuthorId = authorId,
            Title = "Audit publication",
            Content = "text",
            Preview = "text",
            IsPublished = true,
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        return publicationId;
    }

    /// <summary>
    /// A comment of the polymorphic table: EntityId names the topic, blog, game or
    /// publication it belongs to, and no foreign key stands behind it.
    /// </summary>
    private static async Task<Guid> AddCommentAsync(DmDbContext dbContext, Guid entityId)
    {
        var authorId = await AddUserAsync(dbContext);
        var commentId = Guid.NewGuid();

        dbContext.Comments.Add(new DbComment
        {
            CommentId = commentId,
            EntityId = entityId,
            AuthorId = authorId,
            Text = "Audit comment",
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        return commentId;
    }

    private static async Task<(Guid GameId, Guid RoomId)> AddGameWithRoomAsync(
        DmDbContext dbContext, Guid masterId)
    {
        var gameId = Guid.NewGuid();
        var roomId = Guid.NewGuid();

        dbContext.Games.Add(new DbGame
        {
            GameId = gameId,
            PublicId = Guid.NewGuid().ToString("N")[..10],
            Title = "Game for the deletion audit",
            MasterId = masterId,
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

        return (gameId, roomId);
    }

    private static async Task<(Guid CharacterId, Guid PostId)> AddCharacterWithPostAsync(
        DmDbContext dbContext, Guid gameId, Guid roomId, Guid authorId)
    {
        var characterId = Guid.NewGuid();
        var postId = Guid.NewGuid();

        dbContext.Characters.Add(new DbCharacter
        {
            CharacterId = characterId,
            GameId = gameId,
            AuthorId = authorId,
            Status = CharacterStatus.Active,
            Name = "Char " + Guid.NewGuid().ToString("N")[..6],
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        dbContext.Posts.Add(new DbPost
        {
            PostId = postId,
            RoomId = roomId,
            CharacterId = characterId,
            AuthorId = authorId,
            GameText = "text",
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        return (characterId, postId);
    }
}
