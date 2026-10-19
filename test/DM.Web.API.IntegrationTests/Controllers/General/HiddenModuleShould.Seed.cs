using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DM.Web.API.IntegrationTests.Helpers;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;
using DbChat = DM.Infrastructure.Persistence.Entities.Messaging.Chat;
using DbCharacter = DM.Infrastructure.Persistence.Entities.Game.Characters.Character;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbGameAttributeSchema = DM.Infrastructure.Persistence.Entities.Game.GameAttributeSchema;
using DbGameReview = DM.Infrastructure.Persistence.Entities.Game.GameReview;
using DbLike = DM.Infrastructure.Persistence.Entities.Shared.Like;
using DbNotepadEntry = DM.Infrastructure.Persistence.Entities.Personal.Notepads.NotepadEntry;
using DbPost = DM.Infrastructure.Persistence.Entities.Game.Posts.Post;
using DbPostReview = DM.Infrastructure.Persistence.Entities.Game.PostReview;
using DbPublication = DM.Infrastructure.Persistence.Entities.Blog.Publication;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Controllers.General;

public partial class HiddenModuleShould
{
    private sealed record GameWorld(
        GeneralUser Master,
        GeneralUser Player,
        GeneralUser Stranger,
        GameContent Hidden,
        GameContent Open,
        string Marker);

    private sealed record BlogWorld(GeneralUser Owner, GeneralUser Stranger, BlogContent Hidden, BlogContent Open);

    /// <summary>
    /// Two games of one master, one hidden as the case says and one open, each holding the same things; fresh identifiers.
    /// </summary>
    private async Task<GameWorld> SeedGamesAsync(Hiding hiding)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var master = AddUser(db, "hmm");
        var player = AddUser(db, "hmp");
        var stranger = AddUser(db, "hms");
        var marker = new string(Enumerable.Range(0, 12).Select(_ => (char)('a' + Random.Shared.Next(26))).ToArray());

        var hidden = AddGame(db, master.UserId, player.UserId, marker, hiding);
        var open = AddGame(db, master.UserId, player.UserId, marker, hiding: null);
        await db.SaveChangesAsync();

        return new GameWorld(Caller(master), Caller(player), Caller(stranger), hidden, open, marker);
    }

    private static GameContent AddGame(DmDbContext db, Guid masterId, Guid playerId, string marker, Hiding? hiding)
    {
        var now = DateTimeOffset.UtcNow;
        var content = GameContent.Nothing();
        db.Games.Add(new DbGame
        {
            GameId = content.Game,
            PublicId = Guid.NewGuid().ToString("N")[..10],
            MasterId = masterId,
            Title = hiding == null ? "Открытая игра" : "Скрытая игра",
            Status = hiding == Hiding.PrivateDraft ? ModuleStatus.Draft : ModuleStatus.Active,
            PremoderationStatus = hiding == Hiding.AwaitingEdits
                ? PremoderationStatus.AwaitingEdits
                : PremoderationStatus.Approved,
            DraftVisibility = hiding == Hiding.PrivateDraft ? DraftVisibility.Private : DraftVisibility.Public,
            CommentsAccessMode = CommentsAccessMode.Public,
            CreatedUtc = now.AddDays(-3),
            ActivatedUtc = hiding == Hiding.PrivateDraft ? null : now.AddDays(-2)
        });
        db.Set<DbGameAttributeSchema>().Add(GameSheetTestHelper.Sheet(content.Game));
        db.Set<DbCharacter>().Add(new DbCharacter
        {
            CharacterId = content.Character,
            GameId = content.Game,
            AuthorId = playerId,
            Name = "Персонаж " + content.Character.ToString("N")[..6],
            Status = CharacterStatus.Active,
            AccessPolicy = CharacterAccessPolicy.NoAccess,
            CreatedUtc = now.AddDays(-2)
        });
        AddRooms(db, content);
        AddPost(db, content, masterId, playerId, marker, now);
        db.GameReviews.Add(new DbGameReview
        {
            GameReviewId = content.GameReview,
            AuthorId = playerId,
            GameId = content.Game,
            CreatedUtc = now.AddHours(-1),
            Text = "Рецензия игрока"
        });
        db.Comments.Add(new DbComment
        {
            CommentId = content.Comment,
            EntityId = content.Game,
            AuthorId = playerId,
            CreatedUtc = now.AddHours(-1),
            Text = "Комментарий игрока"
        });
        db.Set<DbNotepadEntry>().Add(new DbNotepadEntry
        {
            EntryId = content.NotepadEntry,
            NotepadType = NotepadType.Master,
            ContainerId = content.Game,
            AuthorId = masterId,
            Title = "Заметка мастера",
            Content = "Текст заметки",
            CreatedUtc = now.AddHours(-1)
        });
        return content;
    }

    /// <summary>An open room, and a chat room with its chat.</summary>
    private static void AddRooms(DmDbContext db, GameContent content)
    {
        var chatId = Guid.NewGuid();
        db.Set<DbRoom>().AddRange(
            new DbRoom
            {
                RoomId = content.Room,
                GameId = content.Game,
                RoomNumber = 1,
                Title = "Открытая комната",
                AccessType = RoomAccessType.Open,
                Type = RoomType.Default,
                OrderNumber = 1.0
            },
            new DbRoom
            {
                RoomId = content.ChatRoom,
                GameId = content.Game,
                RoomNumber = 2,
                Title = "Чат-комната",
                AccessType = RoomAccessType.Open,
                Type = RoomType.Chat,
                OrderNumber = 2.0,
                ChatId = chatId
            });
        db.Chats.Add(new DbChat
        {
            ChatId = chatId,
            Type = ChatType.GameRoom,
            Title = "Чат-комната",
            RoomId = content.ChatRoom,
            PublicId = $"h{chatId:N}"[..10]
        });
    }

    /// <summary>The player's post with the marker in the open room, rated by the master.</summary>
    private static void AddPost(
        DmDbContext db, GameContent content, Guid masterId, Guid playerId, string marker, DateTimeOffset now)
    {
        db.Set<DbPost>().Add(new DbPost
        {
            PostId = content.Post,
            RoomId = content.Room,
            CharacterId = content.Character,
            AuthorId = playerId,
            CreatedUtc = now.AddHours(-2),
            GameText = $"Пост со словом {marker}",
            PrivateAddresseeSnapshotJson = "{}"
        });
        db.PostReviews.Add(new DbPostReview
        {
            PostReviewId = content.PostReview,
            AuthorId = masterId,
            PostId = content.Post,
            PostAuthorId = playerId,
            GameId = content.Game,
            CreatedUtc = now.AddHours(-1),
            SignValue = 1
        });
    }

    /// <summary>
    /// Two blogs of one owner, one hidden as the case says and one open, each holding the same things.
    /// </summary>
    private async Task<BlogWorld> SeedBlogsAsync(Hiding hiding)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        var owner = AddUser(db, "hbo");
        var stranger = AddUser(db, "hbs");

        var hidden = AddBlog(db, owner.UserId, hiding);
        var open = AddBlog(db, owner.UserId, hiding: null);
        await db.SaveChangesAsync();

        return new BlogWorld(Caller(owner), Caller(stranger), hidden, open);
    }

    private static BlogContent AddBlog(DmDbContext db, Guid ownerId, Hiding? hiding)
    {
        var now = DateTimeOffset.UtcNow;
        var content = BlogContent.Nothing();
        db.Blogs.Add(new DbBlog
        {
            BlogId = content.Blog,
            PublicId = Guid.NewGuid().ToString("N")[..10],
            AuthorId = ownerId,
            Title = hiding == null ? "Открытый блог" : "Скрытый блог",
            Description = "Блог для проверки видимости",
            Status = hiding == Hiding.PrivateDraft ? ModuleStatus.Draft : ModuleStatus.Active,
            PremoderationStatus = hiding == Hiding.AwaitingEdits
                ? PremoderationStatus.AwaitingEdits
                : PremoderationStatus.Approved,
            DraftVisibility = hiding == Hiding.PrivateDraft ? DraftVisibility.Private : DraftVisibility.Public,
            CreatedUtc = now.AddDays(-3),
            ActivatedUtc = hiding == Hiding.PrivateDraft ? null : now.AddDays(-2)
        });
        db.Publications.Add(Publication(content.Publication, content.Blog, ownerId, 1,
            now.AddHours(-2)));
        db.Comments.AddRange(
            new DbComment
            {
                CommentId = content.BlogComment,
                EntityId = content.Blog,
                AuthorId = ownerId,
                CreatedUtc = now.AddHours(-1),
                Text = "Комментарий в блоге"
            },
            new DbComment
            {
                CommentId = content.PublicationComment,
                EntityId = content.Publication,
                AuthorId = ownerId,
                CreatedUtc = now.AddHours(-1),
                Text = "Комментарий под публикацией"
            });
        db.Set<DbNotepadEntry>().Add(new DbNotepadEntry
        {
            EntryId = content.NotepadEntry,
            NotepadType = NotepadType.Blog,
            ContainerId = content.Blog,
            AuthorId = ownerId,
            Title = "Заметка блога",
            Content = "Текст заметки",
            CreatedUtc = now.AddHours(-1)
        });
        return content;
    }

    private static DbPublication Publication(
        Guid publicationId, Guid blogId, Guid authorId, int number, DateTimeOffset createdUtc) => new()
        {
            PublicationId = publicationId,
            BlogId = blogId,
            PublicationNumber = number,
            AuthorId = authorId,
            Title = "Публикация",
            Content = "Текст публикации",
            Preview = "Текст",
            CreatedUtc = createdUtc
        };

    /// <summary>
    /// The hidden game and blog outwrite and outrate the open ones inside the period.
    /// </summary>
    private async Task DateIntoPeriodAsync(GameWorld games, BlogWorld blogs, DateTimeOffset period)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        foreach (var (content, posts) in new[] { (games.Hidden, 3), (games.Open, 1) })
        {
            for (var i = 0; i < posts; i++)
            {
                db.Set<DbPost>().Add(new DbPost
                {
                    PostId = Guid.NewGuid(),
                    RoomId = content.Room,
                    CharacterId = content.Character,
                    AuthorId = games.Player.UserId,
                    CreatedUtc = period.AddMinutes(i),
                    GameText = "Пост прошлого месяца",
                    PrivateAddresseeSnapshotJson = "{}"
                });
            }
        }

        await db.PostReviews.Where(r => r.PostReviewId == games.Hidden.PostReview || r.PostReviewId == games.Open.PostReview)
            .ExecuteUpdateAsync(r => r.SetProperty(x => x.CreatedUtc, period));
        foreach (var (content, publications) in new[] { (blogs.Hidden, 3), (blogs.Open, 1) })
        {
            for (var i = 0; i < publications; i++)
            {
                var publicationId = Guid.NewGuid();
                db.Publications.Add(Publication(publicationId, content.Blog, blogs.Owner.UserId, 10 + i,
                    period.AddMinutes(i)));
                db.Likes.Add(new DbLike
                {
                    LikeId = Guid.NewGuid(),
                    EntityId = publicationId,
                    EntityType = LikeEntityType.Publication,
                    UserId = blogs.Stranger.UserId
                });
            }
        }

        await db.SaveChangesAsync();
    }

    /// <summary>The post of the game rated up by as many fresh readers as asked.</summary>
    private async Task<IReadOnlyList<Guid>> RateAsync(GameContent game, Guid postAuthorId, int count)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        var ids = new List<Guid>();
        for (var i = 0; i < count; i++)
        {
            var rater = AddUser(db, "hmr");
            var id = Guid.NewGuid();
            db.PostReviews.Add(new DbPostReview
            {
                PostReviewId = id,
                AuthorId = rater.UserId,
                PostId = game.Post,
                PostAuthorId = postAuthorId,
                GameId = game.Game,
                CreatedUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
                SignValue = 1
            });
            ids.Add(id);
        }

        await db.SaveChangesAsync();
        return ids;
    }

    private static GeneralUser Caller(DbUser user) =>
        new() { UserId = user.UserId, Username = user.Username, Role = UserRole.RegularUser };

    private static DbUser AddUser(DmDbContext db, string prefix)
    {
        var userId = Guid.NewGuid();
        var user = new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed, so the id pads the prefix.
            Username = $"{prefix}{userId:N}"[..20],
            Email = $"{userId:N}@hiddenmodule.example",
            PasswordHash = "hash",
            Salt = "salt"
        };
        db.Users.Add(user);
        return user;
    }
}
