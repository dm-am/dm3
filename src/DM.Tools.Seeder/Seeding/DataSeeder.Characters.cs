using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Account.Features.Security;
using DM.Domain.Community.Features.Polls;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Identity;
using DM.Domain.Personal.Features.Profiles;
using DM.Domain.Personal.Authorization;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Uploads;
using DM.Infrastructure.Core.Storage;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.MongoIntegration;
using DM.Infrastructure.Persistence.Entities.Blog;
using DM.Infrastructure.Persistence.Entities.Forum;
using DM.Infrastructure.Persistence.Entities.Game.Characters;
using DM.Infrastructure.Persistence.Entities.Game.Links;
using DM.Infrastructure.Persistence.Entities.Game.Posts;
using DM.Infrastructure.Persistence.Entities.Messaging;
using DM.Infrastructure.Persistence.Entities.Moderation;
using DM.Infrastructure.Persistence.Entities.Personal.Notepads;
using DM.Infrastructure.Persistence.Entities.Shared;
using DM.Infrastructure.Persistence.Entities.Community;
using DM.Infrastructure.Persistence.Entities.Subscriptions;
using Microsoft.Extensions.Options;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbAttributeSchema = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.AttributeSchema;
using DbAttributeSpecification = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.AttributeSpecification;
using DbStringConstraints = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.StringAttributeConstraints;
using DbBbCodeConstraints = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.BbCodeAttributeConstraints;
using DbListConstraints = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.ListAttributeConstraints;
using DbListValueKind = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.ListValueKind;
using DbListAttributeValue = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.ListAttributeValue;
using DbCharacterAttribute = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.CharacterAttribute;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using DbUsernameHistory = DM.Infrastructure.Persistence.Entities.Account.UsernameHistory;
using DbUserContact = DM.Infrastructure.Persistence.Entities.Account.UserContact;
using Microsoft.EntityFrameworkCore;

namespace DM.Tools.Seeder.Seeding;

internal sealed partial class DataSeeder
{
    // ─────────────────────────────────────────────────────────────────────
    // System attribute schema ("Классическая схема")
    //
    // A single well-known Public schema (Author=null) that mirrors the eight
    // legacy character fields. Its Id and every specification Id are PINNED so
    // that seeded games (Game.AttributeSchemaId) and character attribute rows
    // (CharacterAttribute.AttributeId) keep referencing the same document even
    // though Mongo survives a Postgres reseed. The upsert below is idempotent —
    // a regenerated Id would orphan those references.
    // ─────────────────────────────────────────────────────────────────────
    private static readonly Guid SystemSchemaId = new("b1a5c0de-0000-4000-8000-000000000001");
    private static readonly Guid SpecRaceId = new("b1a5c0de-0000-4000-8000-000000000101");
    private static readonly Guid SpecClassId = new("b1a5c0de-0000-4000-8000-000000000102");
    private static readonly Guid SpecAlignmentId = new("b1a5c0de-0000-4000-8000-000000000103");
    private static readonly Guid SpecAppearanceId = new("b1a5c0de-0000-4000-8000-000000000104");
    private static readonly Guid SpecTemperId = new("b1a5c0de-0000-4000-8000-000000000105");
    private static readonly Guid SpecStoryId = new("b1a5c0de-0000-4000-8000-000000000106");
    private static readonly Guid SpecSkillsId = new("b1a5c0de-0000-4000-8000-000000000107");
    private static readonly Guid SpecInventoryId = new("b1a5c0de-0000-4000-8000-000000000108");

    /// <summary>
    /// The nine D&amp;D alignments, used both as the Мировоззрение list options
    /// and as the stored value of a character's alignment attribute. Order is
    /// the classic law/chaos by good/evil grid, and callers index into it.
    /// </summary>
    private static readonly string[] AlignmentNames =
    {
        "Законопослушный добрый",
        "Нейтральный добрый",
        "Хаотичный добрый",
        "Законопослушный нейтральный",
        "Нейтральный",
        "Хаотичный нейтральный",
        "Законопослушный злой",
        "Нейтральный злой",
        "Хаотичный злой"
    };

    /// <summary>
    /// Idempotently upserts the pinned system attribute schema into Mongo. Uses
    /// ReplaceOneAsync with IsUpsert so a reseed keeps the same Id (games would
    /// otherwise orphan). Every seeded game is attached to <see cref="SystemSchemaId"/>.
    /// </summary>
    private async Task SeedSystemAttributeSchemaAsync(ComprehensiveSeedResult result)
    {
        var schema = new DbAttributeSchema
        {
            Id = SystemSchemaId,
            UserId = null,
            Type = SchemaType.Public,
            Title = "Классическая схема",
            IsRemoved = false,
            Specifications = new List<DbAttributeSpecification>
            {
                new() { Id = SpecRaceId, Title = "Раса", Order = 0, IsDescriptor = false, IsHidden = false,
                    Constraints = new DbStringConstraints { Required = false, MaxLength = 30 } },
                new() { Id = SpecClassId, Title = "Класс", Order = 1, IsDescriptor = true, IsHidden = false,
                    Constraints = new DbStringConstraints { Required = false, MaxLength = 30 } },
                new() { Id = SpecAlignmentId, Title = "Мировоззрение", Order = 2, IsDescriptor = false, IsHidden = false,
                    Constraints = new DbListConstraints
                    {
                        Required = false,
                        Kind = DbListValueKind.Text,
                        Values = AlignmentNames.Select(a => new DbListAttributeValue { Value = a, Modifier = null }).ToList()
                    } },
                new() { Id = SpecAppearanceId, Title = "Внешность", Order = 3, IsDescriptor = false, IsHidden = false,
                    Constraints = new DbBbCodeConstraints { Required = false, MaxLength = 20000 } },
                new() { Id = SpecTemperId, Title = "Характер", Order = 4, IsDescriptor = false, IsHidden = false,
                    Constraints = new DbBbCodeConstraints { Required = false, MaxLength = 20000 } },
                new() { Id = SpecStoryId, Title = "История", Order = 5, IsDescriptor = false, IsHidden = false,
                    Constraints = new DbBbCodeConstraints { Required = false, MaxLength = 20000 } },
                new() { Id = SpecSkillsId, Title = "Навыки", Order = 6, IsDescriptor = false, IsHidden = false,
                    Constraints = new DbBbCodeConstraints { Required = false, MaxLength = 20000 } },
                new() { Id = SpecInventoryId, Title = "Инвентарь", Order = 7, IsDescriptor = false, IsHidden = false,
                    Constraints = new DbBbCodeConstraints { Required = false, MaxLength = 20000 } }
            }
        };

        var collection = _mongoClient.GetCollection<DbAttributeSchema>();
        await collection.ReplaceOneAsync(
            MongoDB.Driver.Builders<DbAttributeSchema>.Filter.Eq(s => s.Id, SystemSchemaId),
            schema,
            new MongoDB.Driver.ReplaceOptions { IsUpsert = true });

        result.Details.Add("System attribute schema 'Классическая схема' upserted");
    }

    /// <summary>
    /// Writes <see cref="DbCharacterAttribute"/> rows for the legacy per-field
    /// character values against the pinned system schema. Empty/null values are
    /// skipped (e.g. a Cyberpunk character with no alignment gets no Мировоззрение
    /// row, mirroring the legacy games that had no alignment at all).
    /// </summary>
    private void AddLegacyCharacterAttributes(
        Guid characterId,
        string? race = null,
        string? @class = null,
        int? alignmentIndex = null,
        string? appearance = null,
        string? temper = null,
        string? story = null,
        string? skills = null,
        string? inventory = null)
    {
        void Add(Guid attributeId, string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            _dbContext.Set<DbCharacterAttribute>().Add(new DbCharacterAttribute
            {
                CharacterAttributeId = _guidFactory.Create(),
                AttributeId = attributeId,
                CharacterId = characterId,
                Value = value
            });
        }

        Add(SpecRaceId, race);
        Add(SpecClassId, @class);
        Add(SpecAlignmentId, alignmentIndex.HasValue ? AlignmentNames[alignmentIndex.Value] : null);
        Add(SpecAppearanceId, appearance);
        Add(SpecTemperId, temper);
        Add(SpecStoryId, story);
        Add(SpecSkillsId, skills);
        Add(SpecInventoryId, inventory);
    }

    private void SeedCharactersAndPostsForGames(List<DbGame> games, List<DbUser> users, ComprehensiveSeedResult result)
    {
        // The one place that read the clock again instead of the run's epoch, so
        // its timestamps drifted from every other aggregate by the runtime of the
        // seed and could not be pinned at all.
        var now = _now;
        var characterNames = new[] { "Арагорн Следопыт", "Эльвира Чародейка", "Горим Железный Кулак", "Лиара Тенебраум", "Кассандра Видящая", "Торин Дубощит" };
        var races = new[] { "Человек", "Эльф", "Дварф", "Полуэльф", "Тифлинг", "Гном" };
        var classes = new[] { "Следопыт", "Маг", "Воин", "Плут", "Жрец", "Паладин" };
        var postTexts = new[]
        {
            "Я осторожно оглядываюсь по сторонам, держа руку на рукояти меча.",
            "Произношу заклинание обнаружения магии, пытаясь понять природу этого места.",
            "Проверяю следы на земле. Кто-то здесь был совсем недавно.",
            "Подхожу к двери и прислушиваюсь.",
            "Готовлю щит и занимаю оборонительную позицию.",
            "Внимательно осматриваю комнату в поисках скрытых проходов."
        };

        foreach (var game in games)
        {
            var room = game.Rooms.FirstOrDefault(r => r.AccessType == RoomAccessType.Open);
            if (room == null)
            {
                // Create a room if none exists
                var maxRoomNumber = game.Rooms.Count > 0 ? game.Rooms.Max(r => r.RoomNumber) : 0;
                room = new Room
                {
                    RoomId = _guidFactory.Create(),
                    GameId = game.GameId,
                    Title = "Главная локация",
                    AccessType = RoomAccessType.Open,
                    Type = RoomType.Default,
                    RoomNumber = maxRoomNumber + 1,
                    OrderNumber = 1.0,
                    ViewPrivateText = false,
                    ViewDiceResults = true,
                    DiceEnabled = true,
                    IsRemoved = false
                };
                _dbContext.Set<Room>().Add(room);
            }

            // Exclude "OnlyReader" from playing games (for testing 0 gamesPlaying tooltip).
            // Take(2) player characters + the single NPC below = 3 active characters per
            // game/room. Keeps demo tooltips (game.activeCharacters, room participants)
            // concise — earlier Take(3) + NPC = 4 characters felt overloaded in UI.
            var playersForGame = users.Where(u => u.UserId != game.MasterId && u.Username != "OnlyReader").OrderBy(_ => _random.Next()).Take(2).ToList();
            var createdCharacters = new List<Character>();

            // Create characters
            for (var ci = 0; ci < playersForGame.Count; ci++)
            {
                var player = playersForGame[ci];
                var character = new Character
                {
                    CharacterId = _guidFactory.Create(),
                    GameId = game.GameId,
                    AuthorId = player.UserId,
                    Status = CharacterStatus.Active,
                    IsDead = false,
                    IsPlayerLeft = false,
                    IsPlayerExiled = false,
                    CreatedUtc = game.CreatedUtc.AddDays(_random.Next(1, 7)),
                    Name = characterNames[ci % characterNames.Length],
                    IsNpc = false,
                    AccessPolicy = CharacterAccessPolicy.NoAccess,
                    IsRemoved = false
                };

                _dbContext.Set<Character>().Add(character);
                AddLegacyCharacterAttributes(character.CharacterId,
                    race: races[ci % races.Length],
                    @class: classes[ci % classes.Length],
                    alignmentIndex: ci % AlignmentNames.Length,
                    appearance: "Высокий, крепкого телосложения.",
                    temper: "Решительный и отважный.",
                    story: "Родился в маленькой деревне...",
                    skills: "Владение мечом, выживание.",
                    inventory: "Меч, лук, рюкзак.");
                createdCharacters.Add(character);
                result.CharactersCreated++;
            }

            // Create NPC
            var npc = new Character
            {
                CharacterId = _guidFactory.Create(),
                GameId = game.GameId,
                AuthorId = null,
                Status = CharacterStatus.Active,
                CreatedUtc = game.CreatedUtc,
                Name = "Таинственный Незнакомец",
                IsNpc = true,
                AccessPolicy = CharacterAccessPolicy.NoAccess,
                IsRemoved = false
            };
            _dbContext.Set<Character>().Add(npc);
            AddLegacyCharacterAttributes(npc.CharacterId,
                race: "Неизвестно", @class: "Неизвестно", appearance: "Фигура, скрытая тенью.");
            createdCharacters.Add(npc);
            result.CharactersCreated++;

            // Create posts
            var activeChars = createdCharacters.Where(c => c.Status == CharacterStatus.Active).ToList();
            if (activeChars.Count > 0 && game.ActivatedUtc.HasValue)
            {
                var postsToCreate = 10;
                var baseTime = game.ActivatedUtc.Value;
                // Spread posts between activation and now (or closed date)
                var endTime = game.ClosedUtc ?? now;
                var totalHoursAvailable = (int)(endTime - baseTime).TotalHours;
                var hoursPerPost = postsToCreate > 0 ? Math.Max(1, totalHoursAvailable / postsToCreate) : 1;

                for (var pi = 0; pi < postsToCreate; pi++)
                {
                    var character = activeChars[pi % activeChars.Count];
                    var postOffsetHours = Math.Min(pi * hoursPerPost + _random.Next(0, hoursPerPost), Math.Max(1, totalHoursAvailable - 1));
                    var post = new Post
                    {
                        PostId = _guidFactory.Create(),
                        RoomId = room.RoomId,
                        CharacterId = character.CharacterId,
                        AuthorId = character.AuthorId ?? game.MasterId,
                        CreatedUtc = baseTime.AddHours(postOffsetHours),
                        GameText = postTexts[pi % postTexts.Length],
                        IsRemoved = false
                    };
                    _dbContext.Set<Post>().Add(post);
                    result.PostsCreated++;
                }
            }
        }

        result.Details.Add($"Seeded {result.CharactersCreated} characters and {result.PostsCreated} posts for {games.Count} existing games");
    }

    /// <summary>
    /// Creates 10 "drop" characters for SolohinLex — Retired with
    /// <see cref="DM.Infrastructure.Persistence.Entities.Game.Characters.Character.IsPlayerLeft"/>=true
    /// in existing games. Idempotent: if there are already 10+ drops, does nothing.
    /// We pick arbitrary games (not ones created by SolohinLex as a master, to
    /// avoid breaking the logic), without the "one player — one active role
    /// per game" uniqueness check: the Retired status rules out competing with Active.
    /// </summary>
    private async Task SeedSolohinLexDrops(List<DbUser> users, DateTimeOffset now, ComprehensiveSeedResult result)
    {
        var solohin = users.FirstOrDefault(u => u.Username == "SolohinLex");
        if (solohin == null)
        {
            result.Details.Add("SolohinLex drops skipped: target user not found");
            return;
        }

        var existing = await _dbContext.Set<Character>()
            .Where(c => c.AuthorId == solohin.UserId
                && c.Status == CharacterStatus.Retired
                && c.IsPlayerLeft
                && !c.IsRemoved)
            .CountAsync();
        if (existing >= 10)
        {
            result.Details.Add($"SolohinLex already has {existing} drops, skipping drop seed");
            return;
        }

        const int targetCount = 10;
        var toCreate = targetCount - existing;

        // Take existing games from the ChangeTracker (created in step 6) —
        // more convenient than a repeated SELECT; and these games are definitely valid.
        var availableGames = _dbContext.ChangeTracker.Entries<DbGame>()
            .Select(e => e.Entity)
            .Where(g => !g.IsRemoved && g.MasterId != solohin.UserId)
            .Take(toCreate * 2)
            .ToList();

        if (availableGames.Count < toCreate)
        {
            // Fallback: pick from the DB (in case the ChangeTracker is empty,
            // e.g. after a restart between seed steps).
            availableGames = await _dbContext.Set<DbGame>()
                .Where(g => !g.IsRemoved && g.MasterId != solohin.UserId)
                .Take(toCreate * 2)
                .ToListAsync();
        }

        if (availableGames.Count == 0)
        {
            result.Details.Add("SolohinLex drops skipped: no eligible games");
            return;
        }

        var characterNames = new[]
        {
            "Алхимик", "Лучник", "Странствующий бард", "Бывший рыцарь",
            "Чародей-отшельник", "Авантюрист", "Наемник", "Картограф",
            "Бывший монах", "Путник",
        };
        var races = new[] { "человек", "эльф", "гном", "полуэльф", "тифлинг" };
        var classes = new[] { "воин", "маг", "следопыт", "бард", "плут" };

        for (var i = 0; i < toCreate; i++)
        {
            var game = availableGames[i % availableGames.Count];
            var createdAgo = (existing + i + 1) * 45;
            var dropCharacter = new Character
            {
                CharacterId = _guidFactory.Create(),
                GameId = game.GameId,
                AuthorId = solohin.UserId,
                Status = CharacterStatus.Retired,
                IsDead = false,
                IsPlayerLeft = true,
                IsPlayerExiled = false,
                CreatedUtc = now.AddDays(-createdAgo),
                Name = characterNames[(existing + i) % characterNames.Length],
                IsNpc = false,
                AccessPolicy = CharacterAccessPolicy.NoAccess,
                IsRemoved = false,
            };
            _dbContext.Set<Character>().Add(dropCharacter);
            AddLegacyCharacterAttributes(dropCharacter.CharacterId,
                race: races[(existing + i) % races.Length],
                @class: classes[(existing + i) % classes.Length],
                story: "Покинул игру по личным обстоятельствам.");
        }
        await _dbContext.SaveChangesAsync();
        result.Details.Add($"SolohinLex drops seeded: {toCreate} created (total {targetCount}) for \"дропы\" demo");
    }

    /// <summary>
    /// Seeds deterministic player characters for SolohinLex so the profile
    /// games table ("Игрок" mode) can demonstrate the character status
    /// column: two games where he has an active character (one alongside a
    /// dead character, the other alongside one that left), one game where he
    /// has ONLY former characters (dead + left, no active one) and one game
    /// where he has ONLY an application under review. The profile table
    /// queries the games player filter with PlayerParticipation.Any, so the
    /// two active-less games stay visible there while the public /games
    /// player filter (active-only scope) keeps excluding them. Games are
    /// picked among non-draft games not mastered by SolohinLex where he has
    /// no characters yet, keeping the "one active character per player per
    /// game" rule intact. Idempotent per marker name: re-running skips the
    /// characters that already exist, so extending the plan list back-fills
    /// only the new ones.
    /// </summary>
    private async Task SeedSolohinLexPlayerCharacters(List<DbUser> users, DateTimeOffset now, ComprehensiveSeedResult result)
    {
        var solohin = users.FirstOrDefault(u => u.Username == "SolohinLex");
        if (solohin == null)
        {
            result.Details.Add("SolohinLex player characters skipped: target user not found");
            return;
        }

        var plans = new (int GameIndex, string Name, CharacterStatus Status, bool IsDead, bool IsPlayerLeft, int DaysAgo, string Race, string Class, string Story)[]
        {
            // Game 0: active character + a dead one.
            (0, "Ролан Странник", CharacterStatus.Active, false, false, 30, "Человек", "Следопыт", "Вышел на тракт за новыми историями."),
            (0, "Торвальд Смелый", CharacterStatus.Retired, true, false, 120, "Дварф", "Воин", "Пал в бою, прикрывая отход отряда."),
            // Game 1: active character + one that left the game.
            (1, "Мираэль Тихая", CharacterStatus.Active, false, false, 20, "Эльф", "Плут", "Держится в тени и слушает больше, чем говорит."),
            (1, "Каспар Непоседа", CharacterStatus.Retired, false, true, 90, "Полуэльф", "Бард", "Покинул игру: дорога позвала дальше."),
            // Game 2: former participation only (dead + left, no active
            // character) - visible only via PlayerParticipation.Any.
            (2, "Эйнар Хмурый", CharacterStatus.Retired, true, false, 200, "Человек", "Варвар", "Погиб, не отступив ни на шаг."),
            (2, "Лира Певунья", CharacterStatus.Retired, false, true, 150, "Полуэльф", "Бард", "Покинула игру ради новой баллады."),
            // Game 3: application under review only - visible only via
            // PlayerParticipation.Any.
            (3, "Дориан Пытливый", CharacterStatus.UnderReview, false, false, 3, "Человек", "Маг", "Заявка ожидает решения мастера."),
        };

        // Per-name idempotency: every plan name is a marker. A database
        // seeded by an older version of this step gets only the missing
        // characters back-filled; a fully seeded one is skipped entirely.
        var planNames = plans.Select(p => p.Name).ToList();
        var existingNames = await _dbContext.Set<Character>()
            .Where(c => c.AuthorId == solohin.UserId && !c.IsRemoved && planNames.Contains(c.Name))
            .Select(c => c.Name)
            .ToListAsync();
        var pending = plans.Where(p => !existingNames.Contains(p.Name)).ToList();
        if (pending.Count == 0)
        {
            result.Details.Add("SolohinLex player characters already seeded, skipping");
            return;
        }

        // Games where SolohinLex already has any character (random seed picks
        // or the drop characters from the previous step) are excluded so we
        // never create a second active character in the same game.
        var occupiedGameIds = await _dbContext.Set<Character>()
            .Where(c => c.AuthorId == solohin.UserId && !c.IsRemoved)
            .Select(c => c.GameId)
            .Distinct()
            .ToListAsync();

        var pendingGameIndexes = pending.Select(p => p.GameIndex).Distinct().OrderBy(i => i).ToList();
        var gamesNeeded = pendingGameIndexes.Count;

        var eligibleGames = _dbContext.ChangeTracker.Entries<DbGame>()
            .Select(e => e.Entity)
            .Where(g => !g.IsRemoved &&
                        g.Status != ModuleStatus.Draft &&
                        g.MasterId != solohin.UserId &&
                        !occupiedGameIds.Contains(g.GameId))
            .Take(gamesNeeded)
            .ToList();

        if (eligibleGames.Count < gamesNeeded)
        {
            // Fallback to the database (e.g. when seed steps run against an
            // already-populated database and the ChangeTracker is empty).
            eligibleGames = await _dbContext.Set<DbGame>()
                .Where(g => !g.IsRemoved &&
                            g.Status != ModuleStatus.Draft &&
                            g.MasterId != solohin.UserId &&
                            !occupiedGameIds.Contains(g.GameId))
                .Take(gamesNeeded)
                .ToListAsync();
        }

        if (eligibleGames.Count == 0)
        {
            result.Details.Add("SolohinLex player characters skipped: no eligible games");
            return;
        }

        // Each distinct plan game index gets its own eligible game. Folding
        // several plan indexes into one game is NOT allowed: it could create
        // a second active character of the same player in one game (invariant
        // violation) and would blur the Any-vs-Active scope demo (a
        // former-players-only game would gain an active character). With
        // fewer eligible games than needed the unlucky plan indexes are
        // skipped and reported instead.
        var gameByPlanIndex = pendingGameIndexes
            .Take(eligibleGames.Count)
            .Select((planIndex, i) => (PlanIndex: planIndex, Game: eligibleGames[i]))
            .ToDictionary(x => x.PlanIndex, x => x.Game);

        var skippedCount = pending.Count(p => !gameByPlanIndex.ContainsKey(p.GameIndex));
        if (skippedCount > 0)
        {
            pending = pending.Where(p => gameByPlanIndex.ContainsKey(p.GameIndex)).ToList();
            result.Details.Add(
                $"SolohinLex player characters partially skipped ({skippedCount}): not enough eligible games");
        }

        foreach (var plan in pending)
        {
            var planCharacter = new Character
            {
                CharacterId = _guidFactory.Create(),
                GameId = gameByPlanIndex[plan.GameIndex].GameId,
                AuthorId = solohin.UserId,
                Status = plan.Status,
                IsDead = plan.IsDead,
                IsPlayerLeft = plan.IsPlayerLeft,
                IsPlayerExiled = false,
                CreatedUtc = now.AddDays(-plan.DaysAgo),
                Name = plan.Name,
                IsNpc = false,
                AccessPolicy = CharacterAccessPolicy.NoAccess,
                IsRemoved = false,
            };
            _dbContext.Set<Character>().Add(planCharacter);
            AddLegacyCharacterAttributes(planCharacter.CharacterId,
                race: plan.Race, @class: plan.Class, story: plan.Story);
            result.CharactersCreated++;
        }

        await _dbContext.SaveChangesAsync();
        result.Details.Add(
            $"SolohinLex player characters seeded: {pending.Count} across {gameByPlanIndex.Count} game(s) for profile games table status column");
    }
}
