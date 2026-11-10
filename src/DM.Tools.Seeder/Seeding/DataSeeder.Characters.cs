using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Game.Characters;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbGameAttributeSchema = DM.Infrastructure.Persistence.Entities.Game.GameAttributeSchema;
using DbCharacterAttribute = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.CharacterAttribute;
using DM.Infrastructure.Persistence.InitialData;
using Microsoft.EntityFrameworkCore;

namespace DM.Tools.Seeder.Seeding;

internal sealed partial class DataSeeder
{
    // Only for the games that hold none, so a second run leaves an edited sheet and its version
    // alone.
    private async Task SeedGameAttributeSchemasAsync(ComprehensiveSeedResult result)
    {
        var gamesWithoutSheet = await _dbContext.Set<DbGame>()
            .Where(g => !_dbContext.GameAttributeSchemas.Any(s => s.GameId == g.GameId))
            .Select(g => new { g.GameId, g.CreatedUtc })
            .ToListAsync();

        if (gamesWithoutSheet.Count == 0)
        {
            result.Details.Add("Every game already carries its own attribute sheet");
            return;
        }

        foreach (var game in gamesWithoutSheet)
        {
            _dbContext.GameAttributeSchemas.Add(new DbGameAttributeSchema
            {
                GameId = game.GameId,
                Specifications = DefaultAttributeSchema.Specifications(),
                Version = 0,
                UpdatedUtc = game.CreatedUtc
            });
        }

        await _dbContext.SaveChangesAsync();
        result.Details.Add($"Attribute sheets written for {gamesWithoutSheet.Count} games");
    }

    // Empty values are skipped, and so are race and class, which the sheet does not draw.
    private void AddCharacterSheetValues(
        Guid characterId,
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

        Add(DefaultAttributeSchema.AlignmentId, alignmentIndex.HasValue ? DefaultAttributeSchema.AlignmentNames[alignmentIndex.Value] : null);
        Add(DefaultAttributeSchema.AppearanceId, appearance);
        Add(DefaultAttributeSchema.TemperId, temper);
        Add(DefaultAttributeSchema.StoryId, story);
        Add(DefaultAttributeSchema.SkillsId, skills);
        Add(DefaultAttributeSchema.InventoryId, inventory);
    }

    // The nameless figure every seeded game carries: an authorless character, which is how a master
    // speaks into a room without a character of their own.
    private Character AddMysteriousStranger(DbGame game, ComprehensiveSeedResult result)
    {
        var npc = new Character
        {
            CharacterId = _guidFactory.Create(),
            GameId = game.GameId,
            AuthorId = null,
            Status = CharacterStatus.Active,
            CreatedUtc = game.CreatedUtc,
            Name = "Таинственный Незнакомец",
            IsNpc = true,
        };
        _dbContext.Set<Character>().Add(npc);
        AddCharacterSheetValues(npc.CharacterId,
            appearance: "Фигура, скрытая тенью.");
        result.CharactersCreated++;
        return npc;
    }

    private sealed record GameCast(
        List<DbUser> Players, List<Character> Characters, List<Character> RoomMembers, Character Npc);

    private static readonly string[] GameCharacterNames =
        ["Арагорн Следопыт", "Эльвира Чародейка", "Горим Железный Кулак", "Лиара Тенебраум", "Кассандра Видящая", "Торин Дубощит", "Леголас Зеленый Лист", "Гимли сын Глоина"];

    private async Task<GameCast> SeedGameCharacters(
        SeedingGame seeding, GameRooms rooms, List<DbUser> users, GameShowcase showcase,
        ComprehensiveSeedResult result)
    {
        var (game, master, _, variation, _) = seeding;

        var playersForGame = users.Where(u => u.UserId != master.UserId).OrderBy(_ => _random.Next()).Take(variation.ActiveChars + 2).ToList();
        var createdCharacters = new List<Character>();
        // Characters holding access to the closed rooms: whoever writes
        // there has to be able to read there.
        var restrictedRoomMembers = new List<Character>();

        await SeedActiveCharacters(seeding, rooms, playersForGame, createdCharacters, restrictedRoomMembers, showcase, result);
        SeedInactiveCharacters(seeding, playersForGame, createdCharacters, result);

        var npc = AddMysteriousStranger(game, result);
        createdCharacters.Add(npc);

        return new GameCast(playersForGame, createdCharacters, restrictedRoomMembers, npc);
    }

    private async Task SeedActiveCharacters(
        SeedingGame seeding, GameRooms rooms, List<DbUser> playersForGame, List<Character> createdCharacters,
        List<Character> restrictedRoomMembers, GameShowcase showcase, ComprehensiveSeedResult result)
    {
        var (game, _, template, variation, _) = seeding;

        for (var ci = 0; ci < Math.Min(variation.ActiveChars, playersForGame.Count); ci++)
        {
            var player = playersForGame[ci];

            var isDiopsideChar = !showcase.HasDiopsideCharacter && ci == 0
                && template.ClosedReason == ClosedReason.Finished;
            if (isDiopsideChar) showcase.HasDiopsideCharacter = true;

            var character = new Character
            {
                CharacterId = _guidFactory.Create(),
                GameId = game.GameId,
                AuthorId = player.UserId,
                Status = CharacterStatus.Active,
                CreatedUtc = game.CreatedUtc.AddDays(_random.Next(1, 7)),
                Name = isDiopsideChar ? "Диопсид" : GameCharacterNames[ci % GameCharacterNames.Length],
            };

            _dbContext.Set<Character>().Add(character);
            AddCharacterSheetValues(character.CharacterId,
                alignmentIndex: template.System != "Cyberpunk RED" ? ci % DefaultAttributeSchema.AlignmentNames.Count : null,
                appearance: isDiopsideChar
                    ? "Полупрозрачное существо из живого кристалла. Отростки вдоль позвоночника мерцают приглушенным светом. Тело переливается оттенками зеленого и голубого."
                    : "Высокий, крепкого телосложения, с проницательным взглядом.",
                temper: isDiopsideChar
                    ? "Древний и терпеливый. Мыслит категориями эпох, но способен к неожиданному любопытству."
                    : "Решительный и отважный, но иногда слишком упрямый.",
                story: isDiopsideChar
                    ? "Осколок кристаллического мира, упавший через разрыв между измерениями. Столетия одиночества обострили восприятие, но оставили тоску по утраченной гармонии."
                    : "Родился в маленькой деревне, с детства мечтал о приключениях...",
                skills: isDiopsideChar
                    ? "Телепатия, резонанс с магией, поглощение эссенции, кристаллическая регенерация."
                    : "Владение мечом, выживание в дикой местности, следопытство.",
                inventory: isDiopsideChar
                    ? "Нет материальных вещей — только воспоминания о родном мире."
                    : "Меч, лук, 20 стрел, рюкзак с припасами.");
            createdCharacters.Add(character);
            result.CharactersCreated++;

            if (isDiopsideChar)
            {
                await SeedDiopsidePortrait(character, player);
            }

            // Every active character gets the rooms, except the last one of the UI test data game: its closed
            // rooms then show a granted and a denied player, its open rooms a reader who may not write.
            var leaveOutForUiTest = seeding.IsUiTestDataGame &&
                ci == Math.Min(variation.ActiveChars, playersForGame.Count) - 1;
            if (!leaveOutForUiTest)
            {
                GrantRoomAccess(rooms, character);
                restrictedRoomMembers.Add(character);
            }
        }
    }

    // The portrait of the Diopside character — the real pipeline: EXIF strip + downscale, one
    // source object lands in object storage.
    private async Task SeedDiopsidePortrait(Character character, DbUser player)
    {
        var bytes = ReadEmbeddedSeedBytes(DiopsideImage);
        var upload = await SeedImageFromBytesAsync(
            bytes,
            declaredContentType: "image/jpeg",
            sourceFileName: "diopside.jpg",
            type: UploadType.CharacterAvatar,
            uploadId: _guidFactory.Create(),
            userId: player.UserId,
            entityId: character.CharacterId,
            uploadedUtc: character.CreatedUtc);
        _dbContext.Uploads.Add(upload);
    }

    // Fixed, because no list shows a declined character: the frames open its address by this
    // identifier.
    private static readonly Guid UiTestDeclinedCharacterId = FixedGuid(0xc1, 1);

    private void SeedInactiveCharacters(
        SeedingGame seeding, List<DbUser> playersForGame, List<Character> createdCharacters,
        ComprehensiveSeedResult result)
    {
        var (game, _, template, variation, _) = seeding;

        var nonActiveStatuses = new[] { CharacterStatus.UnderReview, CharacterStatus.Declined, CharacterStatus.Retired };
        for (var ci = variation.ActiveChars; ci < playersForGame.Count && ci < variation.ActiveChars + 2; ci++)
        {
            var player = playersForGame[ci];
            var status = nonActiveStatuses[(ci - variation.ActiveChars) % nonActiveStatuses.Length];
            // Drawn even where the fixed one stands, so no identifier drawn after it moves.
            var drawnId = _guidFactory.Create();
            var character = new Character
            {
                CharacterId = seeding.IsUiTestDataGame && status == CharacterStatus.Declined
                    ? UiTestDeclinedCharacterId
                    : drawnId,
                GameId = game.GameId,
                AuthorId = player.UserId,
                Status = status,
                IsDead = status == CharacterStatus.Retired,
                CreatedUtc = game.CreatedUtc.AddDays(_random.Next(1, 7)),
                Name = GameCharacterNames[ci % GameCharacterNames.Length],
            };

            _dbContext.Set<Character>().Add(character);
            AddCharacterSheetValues(character.CharacterId,
                alignmentIndex: template.System != "Cyberpunk RED" ? ci % DefaultAttributeSchema.AlignmentNames.Count : null,
                appearance: "Среднего роста, ничем не примечательный.",
                temper: "Спокойный и рассудительный.",
                story: "История еще пишется...",
                skills: "Базовые навыки выживания.",
                inventory: "Простая одежда, кошелек с монетами.");
            createdCharacters.Add(character);
            result.CharactersCreated++;
        }
    }
}
