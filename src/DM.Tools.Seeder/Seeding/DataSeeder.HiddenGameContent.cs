using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Content;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Game.Characters;
using DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes;
using DM.Infrastructure.Persistence.Entities.Game.Links;
using DM.Infrastructure.Persistence.Entities.Game.Posts;
using DM.Infrastructure.Persistence.InitialData;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbGameAttributeSchema = DM.Infrastructure.Persistence.Entities.Game.GameAttributeSchema;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Tools.Seeder.Seeding;

internal sealed partial class DataSeeder
{
    // The finished game whose hidden parts an outsider must not read: a hidden attribute with
    // values, a hidden dice roll, and a hidden block that mentions one of two attachments.
    internal const string HiddenContentGameTitle = "Шепот Туманной Долины";

    internal static readonly Guid HiddenClassAttributeId = FixedGuid(0xc0, 1);

    internal const string HiddenBlockAddresseeName = "Бран Молчун";

    private const string ScoutName = "Мирра Тихая";

    private static readonly string[] ClassNames = ["Воин", "Маг", "Плут", "Жрец"];

    /// <summary>
    /// The game, its sheet, its open room, two players and the nameless figure, and three posts:
    /// the opening, the scout's with a shown and a hidden roll, and the figure's note with a hidden
    /// block and two attachments.
    /// </summary>
    /// <remarks>
    /// Mastered and played by accounts no role of the stand signs in as, so no sidebar of a role
    /// changes. Their counts of games do: the community list and the games of their profiles count
    /// it. Closed months before every other finished game, which close within the last 90 days, so
    /// the five finished games of the sidebar never reach it, and the full list of finished games,
    /// latest first, ends with it.
    /// </remarks>
    private async Task SeedHiddenGameContent(List<DbUser> users, ComprehensiveSeedResult result)
    {
        if (await _dbContext.Games.AnyAsync(g => g.Title == HiddenContentGameTitle))
        {
            result.Details.Add($"Game \"{HiddenContentGameTitle}\" already exists, skipping");
            return;
        }

        var master = users.FirstOrDefault(u => u.Username == "TestHonorary");
        var scoutPlayer = users.FirstOrDefault(u => u.Username == "Player_One");
        var addresseePlayer = users.FirstOrDefault(u => u.Username == "Игрок");
        if (master == null || scoutPlayer == null || addresseePlayer == null)
        {
            result.Details.Add($"Game \"{HiddenContentGameTitle}\" skipped: its accounts are missing");
            return;
        }

        var game = await NewHiddenContentGame(master);
        var room = HiddenContentRoom(game);
        var npc = AddMysteriousStranger(game, result);
        var scout = AddHiddenGamePlayer(game, room, scoutPlayer, ScoutName, "Плут", result);
        var addressee = AddHiddenGamePlayer(game, room, addresseePlayer, HiddenBlockAddresseeName, "Жрец", result);

        AddHiddenGamePost(room, npc, master, _now.AddDays(-300),
            "Туман опускается на долину, и у костра становится тихо. Из темноты выходит незнакомец в сером плаще.");
        var scoutPost = AddHiddenGamePost(room, scout, scoutPlayer, _now.AddDays(-299).AddHours(2),
            "Мирра отходит от костра и крадется вдоль камней, чтобы зайти незнакомцу за спину.");
        _dbContext.DiceRolls.AddRange(ScoutRolls(
            scoutPost.PostId, scoutPost.CreatedUtc, _guidFactory.Create(), _guidFactory.Create()));

        await SeedStrangerNote(room, npc, master, addressee, _now.AddDays(-298));

        game.LastPostCreatedUtc = _now.AddDays(-298);
        await _dbContext.SaveChangesAsync();

        result.GamesCreated++;
        result.PostsCreated += 3;
        result.Details.Add(
            $"Game \"{HiddenContentGameTitle}\" ({game.PublicId}): a hidden attribute, a hidden roll, " +
            "a hidden block with an attachment it mentions and one it does not");
    }

    private async Task<DbGame> NewHiddenContentGame(DbUser master)
    {
        // The readable address comes from the sequence before the insert, as in the repository.
        var serialNumber = await SerialNumberAllocator.NextAsync<DbGame>(_dbContext);
        var game = new DbGame
        {
            GameId = _guidFactory.Create(),
            SerialNumber = serialNumber,
            PublicId = _publicIdService.Encode(serialNumber),
            CreatedUtc = _now.AddDays(-310),
            ActivatedUtc = _now.AddDays(-305),
            ClosedUtc = _now.AddDays(-280),
            Status = ModuleStatus.Closed,
            ClosedReason = ClosedReason.Finished,
            PremoderationStatus = PremoderationStatus.Approved,
            DraftVisibility = DraftVisibility.Public,
            MasterId = master.UserId,
            Title = HiddenContentGameTitle,
            SystemName = "D&D 5e",
            NarrativeSetting = "Homebrew",
            Info = "Короткая игра о долине, где у каждого есть тайна. Мастер открывает ее только тем, кому она адресована.",
            CommentsAccessMode = CommentsAccessMode.Public,
        };
        _dbContext.Games.Add(game);
        _dbContext.GameAttributeSchemas.Add(new DbGameAttributeSchema
        {
            GameId = game.GameId,
            Specifications = HiddenClassSchema(),
            Version = 0,
            UpdatedUtc = game.CreatedUtc,
        });
        return game;
    }

    /// <summary>
    /// The default sheet and "Класс", hidden. The descriptor stays the default one: every reader of
    /// the game page sees the descriptor, so the domain refuses a hidden one.
    /// </summary>
    internal static List<AttributeSpecification> HiddenClassSchema()
    {
        var specifications = DefaultAttributeSchema.Specifications();
        specifications.Add(new AttributeSpecification
        {
            Id = HiddenClassAttributeId,
            Title = "Класс",
            Order = specifications.Count,
            IsHidden = true,
            Constraints = new ListAttributeConstraints
            {
                Kind = ListValueKind.Text,
                Values = ClassNames.Select(name => new ListAttributeValue { Value = name }).ToList()
            }
        });
        return specifications;
    }

    private Room HiddenContentRoom(DbGame game)
    {
        var room = new Room
        {
            RoomId = _guidFactory.Create(),
            GameId = game.GameId,
            Title = "Костер в долине",
            AccessType = RoomAccessType.Open,
            Type = RoomType.Default,
            RoomNumber = 1,
            OrderNumber = 1,
            ViewPrivateText = false,
            ViewDiceResults = true,
            DiceEnabled = true,
        };
        _dbContext.Rooms.Add(room);
        return room;
    }

    private Character AddHiddenGamePlayer(
        DbGame game, Room room, DbUser player, string name, string className, ComprehensiveSeedResult result)
    {
        var character = new Character
        {
            CharacterId = _guidFactory.Create(),
            GameId = game.GameId,
            AuthorId = player.UserId,
            Status = CharacterStatus.Active,
            CreatedUtc = game.CreatedUtc.AddDays(2),
            Name = name,
        };
        _dbContext.Characters.Add(character);
        AddCharacterSheetValues(character.CharacterId,
            appearance: "Невысокий путник в плаще, промокшем от тумана.");
        _dbContext.CharacterAttributes.Add(new CharacterAttribute
        {
            CharacterAttributeId = _guidFactory.Create(),
            AttributeId = HiddenClassAttributeId,
            CharacterId = character.CharacterId,
            Value = className
        });
        _dbContext.RoomAccesses.Add(new RoomAccess
        {
            AccessId = _guidFactory.Create(),
            RoomId = room.RoomId,
            CharacterId = character.CharacterId,
            Policy = RoomAccessPolicy.Full
        });
        result.CharactersCreated++;
        return character;
    }

    private Post AddHiddenGamePost(
        Room room, Character character, DbUser author, DateTimeOffset createdUtc, string text)
    {
        var post = new Post
        {
            PostId = _guidFactory.Create(),
            RoomId = room.RoomId,
            CharacterId = character.CharacterId,
            AuthorId = author.UserId,
            CreatedUtc = createdUtc,
            GameText = text,
        };
        _dbContext.Posts.Add(post);
        return post;
    }

    internal static DiceRoll[] ScoutRolls(Guid postId, DateTimeOffset createdUtc, Guid shownRollId, Guid hiddenRollId) =>
    [
        new()
        {
            DiceRollId = shownRollId,
            PostId = postId,
            CreatedUtc = createdUtc,
            DiceCount = 1,
            EdgesCount = 20,
            Bonus = 3,
            Comment = "Внимательность",
            Result = [new RollResult { Value = 12 }]
        },
        new()
        {
            DiceRollId = hiddenRollId,
            PostId = postId,
            CreatedUtc = createdUtc.AddSeconds(1),
            IsHidden = true,
            DiceCount = 1,
            EdgesCount = 20,
            Bonus = 6,
            Comment = "Скрытность",
            Result = [new RollResult { Value = 17 }]
        }
    ];

    /// <summary>
    /// The figure's note: a hidden block for the addressee that mentions the map, and the letter
    /// attached without a word about it. The map is marked for the addressee, as the save path
    /// requires of a file the text puts only inside a block; the letter is for every reader.
    /// </summary>
    private async Task SeedStrangerNote(
        Room room, Character npc, DbUser master, Character addressee, DateTimeOffset createdUtc)
    {
        var mapUploadId = _guidFactory.Create();
        var letterUploadId = _guidFactory.Create();
        var note = AddHiddenGamePost(room, npc, master, createdUtc, StrangerNoteText(mapUploadId));
        note.PrivateAddresseeSnapshotJson =
            StrangerNoteSnapshot(note.GameText, addressee.CharacterId, addressee.AuthorId!.Value);

        var image = ReadEmbeddedSeedBytes(DiopsideImage);
        var mapAddressees = StrangerNoteMapAddressees(addressee.CharacterId, addressee.AuthorId!.Value);
        foreach (var (uploadId, fileName, addressees) in new[]
                 {
                     (mapUploadId, "карта-долины.jpg", mapAddressees),
                     (letterUploadId, "письмо.jpg", (string?)null),
                 })
        {
            var upload = await SeedImageFromBytesAsync(
                image,
                declaredContentType: "image/jpeg",
                sourceFileName: fileName,
                type: UploadType.PostAttachment,
                uploadId: uploadId,
                userId: master.UserId,
                entityId: note.PostId,
                uploadedUtc: createdUtc.AddMinutes(5));
            upload.AddresseesJson = addressees;
            _dbContext.Uploads.Add(upload);
        }
    }

    // The mark of the map: the addressee of the block, frozen as the upload path freezes it.
    internal static string StrangerNoteMapAddressees(Guid addresseeCharacterId, Guid addresseeOwnerId) =>
        PrivateAddresseeSnapshot.BuildList(
            [addresseeCharacterId],
            [new PrivateAddressee(addresseeCharacterId, HiddenBlockAddresseeName, addresseeOwnerId)]);

    // The map is mentioned by the path of its file, inside the block and nowhere else: a path is
    // text the grammar keeps, where a link to the stand's own host is refused.
    internal static string StrangerNoteText(Guid mapUploadId) =>
        "Незнакомец греет руки у огня и долго молчит.\n\n" +
        $"[private=\"{HiddenBlockAddresseeName}\"]Уходя, он незаметно вкладывает Брану в ладонь сложенный лист. " +
        $"Это карта тайника под старой мельницей: /v1/uploads/{mapUploadId:D}/content[/private]\n\n" +
        "К утру от незнакомца остаются только следы на мокрой траве.";

    // The addressee frozen as the save path freezes it, from the roster of the room.
    internal static string StrangerNoteSnapshot(string text, Guid addresseeCharacterId, Guid addresseeOwnerId) =>
        PrivateAddresseeSnapshot.Build(
            text, [new PrivateAddressee(addresseeCharacterId, HiddenBlockAddresseeName, addresseeOwnerId)]);
}
