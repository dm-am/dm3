using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Game.Characters;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using Microsoft.EntityFrameworkCore;

namespace DM.Tools.Seeder.Seeding;

internal sealed partial class DataSeeder
{
    /// <summary>
    /// Creates 10 retired characters of SolohinLex that left their game
    /// (<see cref="Character.IsPlayerLeft"/>), in games he
    /// does not master. Retired does not compete with an active character. Idempotent.
    /// </summary>
    private async Task SeedSolohinLexDrops(List<DbUser> users, ComprehensiveSeedResult result)
    {
        var solohin = users.FirstOrDefault(u => u.Username == PrimaryLogin);
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

        var availableGames = await SolohinLexDropGames(solohin, toCreate);

        var characterNames = new[]
        {
            "Алхимик", "Лучник", "Странствующий бард", "Бывший рыцарь",
            "Чародей-отшельник", "Авантюрист", "Наемник", "Картограф",
            "Бывший монах", "Путник",
        };

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
                IsPlayerLeft = true,
                CreatedUtc = _now.AddDays(-createdAgo),
                Name = characterNames[(existing + i) % characterNames.Length],
            };
            _dbContext.Set<Character>().Add(dropCharacter);
            AddCharacterSheetValues(dropCharacter.CharacterId,
                story: "Покинул игру по личным обстоятельствам.");
        }
        await _dbContext.SaveChangesAsync();
        result.Details.Add($"SolohinLex drops seeded: {toCreate} created (total {targetCount}) for \"дропы\" demo");
    }

    /// <summary>
    /// The games SolohinLex drops out of: any games he does not master.
    /// </summary>
    private async Task<List<DbGame>> SolohinLexDropGames(DbUser solohin, int toCreate)
    {
        // The games of this run from the change tracker
        var availableGames = _dbContext.ChangeTracker.Entries<DbGame>()
            .Select(e => e.Entity)
            .Where(g => !g.IsRemoved && g.MasterId != solohin.UserId)
            .Take(toCreate * 2)
            .ToList();

        if (availableGames.Count < toCreate)
        {
            // From the database when the change tracker is empty
            availableGames = await _dbContext.Set<DbGame>()
                .Where(g => !g.IsRemoved && g.MasterId != solohin.UserId)
                .OrderBy(g => g.SerialNumber)
                .Take(toCreate * 2)
                .ToListAsync();
        }

        return availableGames;
    }

    /// <summary>
    /// The player characters of SolohinLex, by the index of the game they are
    /// placed in (see <see cref="SeedSolohinLexPlayerCharacters"/>).
    /// </summary>
    private static readonly (int GameIndex, string Name, CharacterStatus Status, bool IsDead, bool IsPlayerLeft, int DaysAgo, string Story)[] SolohinLexCharacterPlans =
    [
        // Game 0: active character + a dead one.
        (0, "Ролан Странник", CharacterStatus.Active, false, false, 30, "Вышел на тракт за новыми историями."),
        (0, "Торвальд Смелый", CharacterStatus.Retired, true, false, 120, "Пал в бою, прикрывая отход отряда."),
        // Game 1: active character + one that left the game.
        (1, "Мираэль Тихая", CharacterStatus.Active, false, false, 20, "Держится в тени и слушает больше, чем говорит."),
        (1, "Каспар Непоседа", CharacterStatus.Retired, false, true, 90, "Покинул игру: дорога позвала дальше."),
        // Game 2: former participation only (dead + left, no active
        // character) - visible only via PlayerParticipation.Any.
        (2, "Эйнар Хмурый", CharacterStatus.Retired, true, false, 200, "Погиб, не отступив ни на шаг."),
        (2, "Лира Певунья", CharacterStatus.Retired, false, true, 150, "Покинула игру ради новой баллады."),
        // Game 3: application under review only - visible only via
        // PlayerParticipation.Any.
        (3, "Дориан Пытливый", CharacterStatus.UnderReview, false, false, 3, "Заявка ожидает решения мастера."),
    ];

    /// <summary>
    /// Seeds player characters of SolohinLex for the "Игрок" mode of the profile games table: two games with
    /// an active character (beside a dead one and one that left), one with former characters only and one with
    /// an application under review only. The last two are visible through PlayerParticipation.Any alone.
    /// Idempotent by name.
    /// </summary>
    /// <remarks>
    /// One eligible game per plan index: folding two into one game could give a player two active characters.
    /// </remarks>
    private async Task SeedSolohinLexPlayerCharacters(List<DbUser> users, ComprehensiveSeedResult result)
    {
        var solohin = users.FirstOrDefault(u => u.Username == PrimaryLogin);
        if (solohin == null)
        {
            result.Details.Add("SolohinLex player characters skipped: target user not found");
            return;
        }

        // Every plan name is a marker, so only the missing characters are written.
        var planNames = SolohinLexCharacterPlans.Select(p => p.Name).ToList();
        var existingNames = await _dbContext.Set<Character>()
            .Where(c => c.AuthorId == solohin.UserId && !c.IsRemoved && planNames.Contains(c.Name))
            .Select(c => c.Name)
            .ToListAsync();
        var pending = SolohinLexCharacterPlans.Where(p => !existingNames.Contains(p.Name)).ToList();
        if (pending.Count == 0)
        {
            result.Details.Add("SolohinLex player characters already seeded, skipping");
            return;
        }

        var pendingGameIndexes = pending.Select(p => p.GameIndex).Distinct().OrderBy(i => i).ToList();
        var gamesNeeded = pendingGameIndexes.Count;
        var eligibleGames = await SolohinLexPlayerGames(solohin, gamesNeeded);

        var gameByPlanIndex = pendingGameIndexes
            .Select((planIndex, i) => (PlanIndex: planIndex, Game: eligibleGames[i]))
            .ToDictionary(x => x.PlanIndex, x => x.Game);

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
                CreatedUtc = _now.AddDays(-plan.DaysAgo),
                Name = plan.Name,
            };
            _dbContext.Set<Character>().Add(planCharacter);
            AddCharacterSheetValues(planCharacter.CharacterId,
                story: plan.Story);
            result.CharactersCreated++;
        }

        await _dbContext.SaveChangesAsync();
        result.Details.Add(
            $"SolohinLex player characters seeded: {pending.Count} across {gameByPlanIndex.Count} game(s) for profile games table status column");
    }

    /// <summary>
    /// The games SolohinLex gets his player characters in: non-draft games he
    /// does not master and holds no character in yet.
    /// </summary>
    private async Task<List<DbGame>> SolohinLexPlayerGames(DbUser solohin, int gamesNeeded)
    {
        // Not where SolohinLex already has a character, so he never gets two active ones in a game.
        var occupiedGameIds = await _dbContext.Set<Character>()
            .Where(c => c.AuthorId == solohin.UserId && !c.IsRemoved)
            .Select(c => c.GameId)
            .Distinct()
            .ToListAsync();

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
            // From the database when the change tracker is empty
            eligibleGames = await _dbContext.Set<DbGame>()
                .Where(g => !g.IsRemoved &&
                            g.Status != ModuleStatus.Draft &&
                            g.MasterId != solohin.UserId &&
                            !occupiedGameIds.Contains(g.GameId))
                .OrderBy(g => g.SerialNumber)
                .Take(gamesNeeded)
                .ToListAsync();
        }

        return eligibleGames;
    }
}
