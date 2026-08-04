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
    private async Task<List<Guid>> CreateGames(List<DbUser> users, DateTimeOffset now, ComprehensiveSeedResult result)
    {
        var gameIds = new List<Guid>();

        // Get existing seed games (excluding migration test games)
        var existingGames = await _dbContext.Set<DbGame>()
            .Include(g => g.Characters)
            .Include(g => g.Rooms)
            .Where(g => !g.Title.StartsWith("Тест:"))
            .ToListAsync();

        var existingTitles = existingGames.Select(g => g.Title).ToHashSet();

        var mentor = users.First(u => u.Role == UserRole.Mentor);
        var experiencedUsers = users.Where(u => !ProbationPolicy.IsNewbie(u.QuantityRating)).ToList();
        var newbieUsers = users.Where(u => ProbationPolicy.IsNewbie(u.QuantityRating)).ToList();

        if (experiencedUsers.Count == 0)
        {
            experiencedUsers = users.Take(3).ToList();
        }

        // Well-known tag GUIDs (from InitialCreate migration)
        // Helper for compact tag ID generation
        static Guid T(int id) => Guid.Parse($"00000000-0000-0000-0000-{id:x12}");

        // System (01-19)
        var (tagBlackBirdPie, tagDnD, tagDnD5e, tagD100, tagDawnOfWorlds) = (T(0x01), T(0x02), T(0x03), T(0x04), T(0x05));
        var (tagFallout, tagFate, tagFudge, tagGURPS, tagInterlock) = (T(0x06), T(0x08), T(0x09), T(0x0a), T(0x0b));
        var (tagPathfinder1e, tagPathfinder2e, tagPbtA, tagRisus, tagSavageWorlds) = (T(0x0d), T(0x0e), T(0x0f), T(0x10), T(0x11));
        var (tagStarfinder1e, tagStarfinder2e, tagWarhammer, tagWoD, tagAuthor) = (T(0x12), T(0x13), T(0x14), T(0x15), T(0x16));
        var (tagMafia, tagSloveski, tagEraVodolea) = (T(0x17), T(0x18), T(0x19));

        // Genre (1a-2b)
        var (tagAltHistory, tagAction, tagDetective, tagZombie, tagHistorical) = (T(0x1a), T(0x1b), T(0x1c), T(0x1d), T(0x1e));
        var (tagCyberpunk, tagComedy, tagKosmoopera, tagMystic, tagModern) = (T(0x1f), T(0x20), T(0x21), T(0x22), T(0x23));
        var (tagPostApoc, tagPsychedelic, tagSteampunk, tagThriller, tagTrash) = (T(0x24), T(0x25), T(0x26), T(0x27), T(0x28));
        var (tagHorror, tagSciFi, tagFantasy) = (T(0x29), T(0x2a), T(0x2b));

        // Game format (2c-32)
        var (tagDungeonCrawl, tagPvP, tagSurvival, tagSandbox, tagStrategy) = (T(0x2c), T(0x2d), T(0x2e), T(0x2f), T(0x30));
        var (tagPlotDriven, tagTactics) = (T(0x31), T(0x32));

        // Post format (33-34), Pace (35-36, 3e), Restrictions (37-3b), Newcomers (3c-3d), Sensitive (3f-41)
        var (tagShortPost, tagLiterary, tagSlowPace, tagFastPace, tagDrySeasons) = (T(0x33), T(0x34), T(0x35), T(0x36), T(0x3e));
        var (tagNoSwearing, tagNoViolence, tagGrammarNazi, tagPrivateGroup, tagMessengerRequired) = (T(0x37), T(0x38), T(0x39), T(0x3a), T(0x3b));
        var (tagForNewbies, tagNewbieMaster, tagErotica, tagShockContent, tagSensitiveTopics) = (T(0x3c), T(0x3d), T(0x3f), T(0x40), T(0x41));

        // Local aliases for repeated enum values (reduces verbosity by ~60%)
        var (Active, Draft, Closed) = (ModuleStatus.Active, ModuleStatus.Draft, ModuleStatus.Closed);
        var (Private, Public) = (DraftVisibility.Private, DraftVisibility.Public);
        var (NoReason, Finished, Frozen) = (ClosedReason.None, ClosedReason.Finished, ClosedReason.Frozen);
        var (Approved, Awaiting) = (PremoderationStatus.Approved, PremoderationStatus.AwaitingApproval);

        var gameTemplates = new[]
        {
            // Active with recruitment (15) - more than sidebar limit of 10
            new { Title = "Хроники Забытых Королевств", System = "D&D 5e", Setting = "Forgotten Realms", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = true, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagDnD5e, tagDnD, tagFantasy, tagPlotDriven, tagLiterary, tagSlowPace, tagDungeonCrawl, tagAction } },
            new { Title = "Врата Бездны", System = "Pathfinder 2e", Setting = "Golarion", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = true, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagPathfinder2e, tagFantasy, tagPlotDriven, tagTactics, tagAction, tagLiterary, tagSlowPace } },
            new { Title = "Последний Рубеж", System = "Savage Worlds", Setting = "Deadlands", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = true, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagSavageWorlds, tagForNewbies, tagFantasy, tagAltHistory, tagHorror, tagAction, tagShortPost, tagFastPace, tagMystic } },
            new { Title = "Путь Самурая", System = "Legend of the Five Rings", Setting = "Rokugan", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = true, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagAuthor, tagFantasy, tagPlotDriven, tagHistorical, tagLiterary, tagSlowPace, tagNoSwearing } },
            new { Title = "Королевство Теней", System = "D&D 5e", Setting = "Ravenloft", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = true, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagDnD5e, tagDnD, tagFantasy, tagForNewbies, tagHorror, tagMystic, tagPlotDriven, tagLiterary } },
            new { Title = "Колонисты Марса", System = "Stars Without Number", Setting = "Solar System 2350", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = true, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagStarfinder1e, tagSciFi, tagKosmoopera, tagSurvival, tagPlotDriven, tagShortPost, tagFastPace } },
            new { Title = "Пески Времени", System = "D&D 5e", Setting = "Al-Qadim", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = true, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagDnD5e, tagDnD, tagFantasy, tagHistorical, tagPlotDriven, tagDungeonCrawl, tagLiterary, tagMystic } },
            new { Title = "Зов Ктулху", System = "Call of Cthulhu", Setting = "1920s Arkham", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = true, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagD100, tagPlotDriven, tagLiterary, tagHorror, tagMystic, tagDetective, tagHistorical, tagSlowPace, tagGrammarNazi } },
            new { Title = "Механикум", System = "Warhammer 40k", Setting = "Dark Millennium", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = true, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagWarhammer, tagSciFi, tagPlotDriven, tagAction, tagTactics, tagLiterary, tagSlowPace } },
            new { Title = "Эльдорадо", System = "Savage Worlds", Setting = "Age of Sail", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = true, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagSavageWorlds, tagForNewbies, tagHistorical, tagAction, tagSandbox, tagShortPost, tagFastPace, tagComedy } },
            new { Title = "Наследие Драконов", System = "Pathfinder 2e", Setting = "Homebrew", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = true, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagPathfinder2e, tagFantasy, tagPlotDriven, tagAction, tagDungeonCrawl, tagLiterary, tagSlowPace } },
            new { Title = "Стальные Небеса", System = "Stars Without Number", Setting = "Cyberpunk Future", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = true, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagStarfinder2e, tagSciFi, tagCyberpunk, tagAction, tagPlotDriven, tagShortPost, tagFastPace, tagThriller } },
            new { Title = "Туманы Авалона", System = "Fate Core", Setting = "Arthurian", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = true, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagFate, tagFantasy, tagLiterary, tagMystic, tagHistorical, tagPlotDriven, tagSlowPace, tagGrammarNazi, tagNoViolence } },
            new { Title = "Проклятие Фараона", System = "GURPS", Setting = "Ancient Egypt", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = true, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagGURPS, tagPlotDriven, tagHistorical, tagMystic, tagDetective, tagLiterary, tagSlowPace } },
            new { Title = "Охотники за Тенями", System = "World of Darkness", Setting = "Modern Nights", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = true, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagWoD, tagLiterary, tagForNewbies, tagModern, tagHorror, tagMystic, tagAction, tagPlotDriven } },
            // Active without recruitment (15)
            new { Title = "Тени Киберпанка", System = "Cyberpunk RED", Setting = "Night City 2077", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagInterlock, tagCyberpunk, tagSciFi, tagLiterary, tagAction, tagPlotDriven, tagThriller, tagSlowPace, tagMessengerRequired } },
            new { Title = "Империя Звезд", System = "Stars Without Number", Setting = "Far Future", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagStarfinder2e, tagSciFi, tagKosmoopera, tagPlotDriven, tagStrategy, tagLiterary, tagSlowPace, tagDrySeasons } },
            new { Title = "Клинки и Колдовство", System = "D&D 5e", Setting = "Dark Sun", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagDnD5e, tagDnD, tagFantasy, tagLiterary, tagSurvival, tagPostApoc, tagAction, tagSlowPace } },
            new { Title = "Сага о Викингах", System = "Fate Core", Setting = "Mythic Scandinavia", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagFate, tagFantasy, tagLiterary, tagHistorical, tagMystic, tagPlotDriven, tagAction, tagSlowPace } },
            new { Title = "Пираты Карибского Моря", System = "7th Sea", Setting = "Theah", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagAuthor, tagFantasy, tagPlotDriven, tagHistorical, tagAction, tagComedy, tagSandbox, tagShortPost } },
            new { Title = "Город Грехов", System = "World of Darkness", Setting = "Modern Gothic", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagWoD, tagLiterary, tagModern, tagHorror, tagMystic, tagThriller, tagPlotDriven, tagSlowPace, tagPrivateGroup, tagSensitiveTopics } },
            new { Title = "Война Гильдий", System = "D&D 5e", Setting = "Ravnica", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagDnD5e, tagDnD, tagFantasy, tagPvP, tagStrategy, tagPlotDriven, tagTactics, tagSlowPace } },
            new { Title = "Метро 2033", System = "GURPS", Setting = "Post-Apocalypse Moscow", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagGURPS, tagPostApoc, tagHorror, tagSurvival, tagAction, tagPlotDriven, tagSlowPace, tagThriller } },
            new { Title = "Ведьмак: Дикая Охота", System = "Словеска", Setting = "The Witcher", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagSloveski, tagFantasy, tagLiterary, tagAction, tagMystic, tagPlotDriven, tagDetective, tagSlowPace } },
            new { Title = "Странники Пустоши", System = "Savage Worlds", Setting = "Fallout", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagFallout, tagSavageWorlds, tagPostApoc, tagSurvival, tagAction, tagSandbox, tagShortPost, tagFastPace } },
            new { Title = "Рыцари Круглого Стола", System = "Pendragon", Setting = "Camelot", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagAuthor, tagFantasy, tagPlotDriven, tagHistorical, tagLiterary, tagMystic, tagSlowPace, tagGrammarNazi } },
            new { Title = "Космические Волки", System = "Stars Without Number", Setting = "Military Sci-Fi", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagStarfinder1e, tagSciFi, tagKosmoopera, tagAction, tagTactics, tagPlotDriven, tagSlowPace } },
            new { Title = "Бегущий по Лезвию", System = "Cyberpunk RED", Setting = "Neo Tokyo", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagInterlock, tagCyberpunk, tagSciFi, tagAction, tagDetective, tagPlotDriven, tagThriller, tagLiterary } },
            new { Title = "Темное Средневековье", System = "D&D 5e", Setting = "Dark Ages Europe", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagDnD5e, tagDnD, tagFantasy, tagHistorical, tagHorror, tagMystic, tagPlotDriven, tagLiterary } },
            new { Title = "Час Волка", System = "World of Darkness", Setting = "Werewolf", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagWoD, tagLiterary, tagPlotDriven, tagModern, tagHorror, tagAction, tagMystic, tagSlowPace } },
            // Draft (experienced master) - PUBLIC (3)
            new { Title = "Проект: Звездные Врата", System = "Savage Worlds", Setting = "Sci-Fi", Status = Draft, DraftVisibility = Public, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagSavageWorlds, tagSciFi, tagKosmoopera, tagAction, tagPlotDriven, tagShortPost } },
            new { Title = "Тайны Древних", System = "Call of Cthulhu", Setting = "1920s", Status = Draft, DraftVisibility = Public, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagD100, tagPlotDriven, tagLiterary, tagHorror, tagMystic, tagDetective, tagHistorical } },
            new { Title = "Затерянный Континент", System = "D&D 5e", Setting = "Lost World", Status = Draft, DraftVisibility = Public, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagDnD5e, tagDnD, tagFantasy, tagSandbox, tagAction, tagDungeonCrawl } },
            // Draft (newbie master - needs premoderation) - PRIVATE (2)
            new { Title = "Моя первая игра", System = "Словеска", Setting = "Фэнтези", Status = Draft, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Awaiting, Tags = new[] { tagSloveski, tagFantasy, tagForNewbies, tagNewbieMaster, tagPlotDriven, tagShortPost, tagNoSwearing } },
            new { Title = "Приключения начинаются", System = "D&D 5e", Setting = "Homebrew", Status = Draft, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Awaiting, Tags = new[] { tagDnD5e, tagDnD, tagFantasy, tagForNewbies, tagNewbieMaster, tagDungeonCrawl, tagShortPost } },
            // Closed - Finished (15)
            new { Title = "Легенда о Драконе", System = "D&D 3.5", Setting = "Homebrew", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagDnD, tagFantasy, tagPlotDriven, tagDungeonCrawl, tagAction, tagLiterary, tagSlowPace } },
            new { Title = "Падение Империи", System = "GURPS", Setting = "Roman Empire", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagGURPS, tagPlotDriven, tagLiterary, tagHistorical, tagAltHistory, tagStrategy, tagSlowPace, tagGrammarNazi } },
            new { Title = "Огни Неона", System = "Cyberpunk 2020", Setting = "Night City", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagInterlock, tagCyberpunk, tagSciFi, tagAction, tagPlotDriven, tagThriller, tagLiterary, tagSlowPace } },
            new { Title = "Эпоха Легенд", System = "D&D 5e", Setting = "Forgotten Realms", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagDnD5e, tagDnD, tagFantasy, tagLiterary, tagPlotDriven, tagDungeonCrawl, tagAction, tagSlowPace } },
            new { Title = "Звездный Крейсер", System = "Stars Without Number", Setting = "Space Opera", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagStarfinder1e, tagSciFi, tagKosmoopera, tagPlotDriven, tagAction, tagStrategy, tagLiterary, tagSlowPace } },
            new { Title = "Темная Башня", System = "Fate Core", Setting = "Post-Apocalypse Fantasy", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagFate, tagFantasy, tagPostApoc, tagPlotDriven, tagMystic, tagHorror, tagLiterary, tagSlowPace } },
            new { Title = "Песнь Льда и Пламени", System = "A Song of Ice and Fire RPG", Setting = "Westeros", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagAuthor, tagFantasy, tagLiterary, tagPlotDriven, tagStrategy, tagPvP, tagSlowPace, tagGrammarNazi } },
            new { Title = "Властелин Колец", System = "The One Ring", Setting = "Middle-earth", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagAuthor, tagFantasy, tagPlotDriven, tagLiterary, tagAction, tagMystic, tagSlowPace } },
            new { Title = "Первая Колония", System = "Stars Without Number", Setting = "Alien Planet", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagStarfinder2e, tagSciFi, tagKosmoopera, tagSurvival, tagPlotDriven, tagHorror, tagSlowPace } },
            new { Title = "Тень Мордора", System = "D&D 5e", Setting = "Middle-earth", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagDnD5e, tagDnD, tagFantasy, tagAction, tagPlotDriven, tagDungeonCrawl, tagLiterary } },
            new { Title = "Кровь и Вино", System = "Словеска", Setting = "The Witcher", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagSloveski, tagFantasy, tagLiterary, tagAction, tagMystic, tagDetective, tagPlotDriven, tagSlowPace } },
            new { Title = "Рассвет Империи", System = "GURPS", Setting = "Alternate History", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagGURPS, tagPlotDriven, tagAltHistory, tagHistorical, tagStrategy, tagLiterary, tagSlowPace } },
            new { Title = "Последняя Надежда", System = "Savage Worlds", Setting = "Zombie Apocalypse", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagSavageWorlds, tagPostApoc, tagZombie, tagSurvival, tagHorror, tagAction, tagShortPost, tagFastPace } },
            new { Title = "Хроники Нарнии", System = "Fate Core", Setting = "Narnia", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagFate, tagFantasy, tagForNewbies, tagPlotDriven, tagMystic, tagNoSwearing, tagNoViolence } },
            new { Title = "Город Ангелов", System = "World of Darkness", Setting = "Los Angeles", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagWoD, tagLiterary, tagPlotDriven, tagModern, tagHorror, tagMystic, tagThriller, tagSlowPace } },
            // Closed - Frozen (5)
            new { Title = "Заброшенный Мир", System = "GURPS", Setting = "Post-Apocalypse", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Frozen, Premod = Approved, Tags = new[] { tagGURPS, tagPostApoc, tagSurvival, tagPlotDriven, tagHorror, tagAction, tagDrySeasons } },
            new { Title = "Эхо Войны", System = "Savage Worlds", Setting = "WW2", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Frozen, Premod = Approved, Tags = new[] { tagSavageWorlds, tagPlotDriven, tagHistorical, tagAction, tagTactics, tagShortPost, tagDrySeasons } },
            new { Title = "Забытые Руины", System = "D&D 5e", Setting = "Homebrew", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Frozen, Premod = Approved, Tags = new[] { tagDnD5e, tagDnD, tagFantasy, tagDungeonCrawl, tagAction, tagPlotDriven, tagDrySeasons } },
            new { Title = "Тихий Омут", System = "Call of Cthulhu", Setting = "Lovecraft Country", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Frozen, Premod = Approved, Tags = new[] { tagD100, tagPlotDriven, tagHorror, tagMystic, tagDetective, tagLiterary, tagDrySeasons } },
            new { Title = "Остров Сокровищ", System = "7th Sea", Setting = "Caribbean", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Frozen, Premod = Approved, Tags = new[] { tagAuthor, tagFantasy, tagHistorical, tagAction, tagSandbox, tagComedy, tagDrySeasons } },
            // Closed - None (3)
            new { Title = "Потерянные Хроники", System = "D&D 5e", Setting = "Homebrew", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagDnD5e, tagDnD, tagFantasy, tagPlotDriven, tagDungeonCrawl, tagAction, tagSlowPace } },
            new { Title = "Закат Эпохи", System = "Pathfinder 1e", Setting = "Golarion", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagPathfinder1e, tagFantasy, tagPlotDriven, tagAction, tagTactics, tagLiterary, tagSlowPace } },
            new { Title = "Конец Света", System = "GURPS", Setting = "Apocalypse", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagGURPS, tagPostApoc, tagSurvival, tagHorror, tagAction, tagPlotDriven, tagSlowPace } },
            // Additional games for remaining tags (10)
            new { Title = "Сотворение Миров", System = "Dawn of Worlds", Setting = "Homebrew Universe", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagDawnOfWorlds, tagFantasy, tagStrategy, tagSandbox, tagLiterary, tagSlowPace } },
            new { Title = "Апокалипсис: День Ноль", System = "Apocalypse World", Setting = "Post-Apocalypse", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagPbtA, tagPostApoc, tagSurvival, tagPlotDriven, tagAction, tagShortPost, tagFastPace } },
            new { Title = "Клуб Анонимных Убийц", System = "Мафия", Setting = "Modern Noir", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagMafia, tagModern, tagDetective, tagThriller, tagPvP, tagShortPost, tagFastPace } },
            new { Title = "Безумные Приключения", System = "Risus", Setting = "Сomedy Fantasy", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagRisus, tagFantasy, tagComedy, tagTrash, tagAction, tagShortPost, tagFastPace } },
            new { Title = "Эра Водолея: Пробуждение", System = "Эра Водолея", Setting = "Post-Soviet Fantasy", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagEraVodolea, tagFantasy, tagMystic, tagModern, tagPlotDriven, tagLiterary, tagSlowPace } },
            new { Title = "FUDGE: Универсум", System = "FUDGE", Setting = "Multigenre", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagFudge, tagFantasy, tagSciFi, tagSandbox, tagPlotDriven, tagShortPost, tagFastPace } },
            new { Title = "Паровая Империя", System = "Savage Worlds", Setting = "Victorian Steampunk", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagSavageWorlds, tagSteampunk, tagAltHistory, tagDetective, tagAction, tagPlotDriven, tagLiterary } },
            new { Title = "За Гранью Реальности", System = "FUDGE", Setting = "Dreamscape", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagFudge, tagPsychedelic, tagMystic, tagHorror, tagPlotDriven, tagLiterary, tagSlowPace } },
            new { Title = "Black Bird: Сказки Старого Города", System = "Black Bird Pie", Setting = "Urban Fantasy", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagBlackBirdPie, tagFantasy, tagMystic, tagModern, tagForNewbies, tagShortPost, tagFastPace } },
            new { Title = "Темные Страсти", System = "World of Darkness", Setting = "Gothic Romance", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagWoD, tagHorror, tagMystic, tagPlotDriven, tagLiterary, tagSlowPace, tagErotica, tagPrivateGroup } },
            new { Title = "Кровавый Карнавал", System = "Словеска", Setting = "Extreme Horror", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagSloveski, tagHorror, tagTrash, tagPlotDriven, tagLiterary, tagSlowPace, tagShockContent, tagPrivateGroup } },
        };

        // Variation configs MUST match templates order 1:1 (58 total)
        // Templates: 15 recruiting, 15 active, 3 draft public, 2 draft private, 15 finished, 5 frozen, 3 closed
        var gameVariations = new[]
        {
            // Active with recruitment (15) - spread 0-30
            (readers: 28, pcLimit: 8, activeChars: 3, comments: 12, posts: 25),
            (readers: 22, pcLimit: 6, activeChars: 2, comments: 10, posts: 20),
            (readers: 18, pcLimit: 5, activeChars: 2, comments: 8, posts: 18),
            (readers: 15, pcLimit: 4, activeChars: 2, comments: 7, posts: 15),
            (readers: 12, pcLimit: 6, activeChars: 2, comments: 6, posts: 14),
            (readers: 10, pcLimit: 5, activeChars: 1, comments: 5, posts: 12),
            (readers: 8, pcLimit: 4, activeChars: 1, comments: 4, posts: 10),
            (readers: 6, pcLimit: 6, activeChars: 1, comments: 4, posts: 9),
            (readers: 5, pcLimit: 5, activeChars: 1, comments: 3, posts: 8),
            (readers: 4, pcLimit: 4, activeChars: 1, comments: 3, posts: 7),
            (readers: 3, pcLimit: 6, activeChars: 1, comments: 2, posts: 6),
            (readers: 2, pcLimit: 5, activeChars: 1, comments: 2, posts: 5),
            (readers: 1, pcLimit: 4, activeChars: 1, comments: 2, posts: 5),
            (readers: 1, pcLimit: 6, activeChars: 1, comments: 1, posts: 4),
            (readers: 0, pcLimit: 5, activeChars: 1, comments: 1, posts: 3),
            // Active without recruitment (15) - spread 0-30
            (readers: 30, pcLimit: 6, activeChars: 6, comments: 45, posts: 180),
            (readers: 25, pcLimit: 5, activeChars: 5, comments: 38, posts: 140),
            (readers: 20, pcLimit: 5, activeChars: 5, comments: 32, posts: 120),
            (readers: 16, pcLimit: 4, activeChars: 4, comments: 28, posts: 100),
            (readers: 13, pcLimit: 5, activeChars: 4, comments: 25, posts: 90),
            (readers: 10, pcLimit: (int?)null, activeChars: 4, comments: 22, posts: 80),
            (readers: 8, pcLimit: 4, activeChars: 3, comments: 18, posts: 70),
            (readers: 7, pcLimit: 5, activeChars: 3, comments: 16, posts: 65),
            (readers: 5, pcLimit: 4, activeChars: 3, comments: 14, posts: 55),
            (readers: 4, pcLimit: 5, activeChars: 3, comments: 12, posts: 50),
            (readers: 3, pcLimit: 4, activeChars: 2, comments: 10, posts: 45),
            (readers: 2, pcLimit: 5, activeChars: 2, comments: 9, posts: 40),
            (readers: 2, pcLimit: 4, activeChars: 2, comments: 8, posts: 35),
            (readers: 1, pcLimit: 5, activeChars: 2, comments: 7, posts: 30),
            (readers: 0, pcLimit: 4, activeChars: 2, comments: 6, posts: 25),
            // Drafts public (3)
            (readers: 0, pcLimit: 6, activeChars: 0, comments: 0, posts: 0),
            (readers: 0, pcLimit: (int?)null, activeChars: 0, comments: 0, posts: 0),
            (readers: 0, pcLimit: 5, activeChars: 0, comments: 0, posts: 0),
            // Drafts private/AwaitingApproval (2)
            (readers: 0, pcLimit: 4, activeChars: 0, comments: 0, posts: 0),
            (readers: 0, pcLimit: 6, activeChars: 0, comments: 0, posts: 0),
            // Closed - Finished (15) - spread 0-30
            // First finished game: 3 chars from loop + 1 Maximilian added separately = 4 total
            (readers: 30, pcLimit: 6, activeChars: 3, comments: 85, posts: 420),
            (readers: 25, pcLimit: 5, activeChars: 3, comments: 70, posts: 350),
            (readers: 20, pcLimit: 5, activeChars: 3, comments: 55, posts: 280),
            (readers: 16, pcLimit: 4, activeChars: 3, comments: 45, posts: 220),
            (readers: 13, pcLimit: 5, activeChars: 2, comments: 40, posts: 190),
            (readers: 10, pcLimit: 4, activeChars: 2, comments: 35, posts: 160),
            (readers: 8, pcLimit: 5, activeChars: 2, comments: 30, posts: 140),
            (readers: 6, pcLimit: 4, activeChars: 2, comments: 28, posts: 130),
            (readers: 5, pcLimit: 5, activeChars: 2, comments: 25, posts: 120),
            (readers: 4, pcLimit: 4, activeChars: 2, comments: 22, posts: 110),
            (readers: 3, pcLimit: 5, activeChars: 2, comments: 20, posts: 100),
            (readers: 2, pcLimit: 4, activeChars: 2, comments: 18, posts: 90),
            (readers: 2, pcLimit: 5, activeChars: 2, comments: 15, posts: 80),
            (readers: 1, pcLimit: 4, activeChars: 2, comments: 12, posts: 70),
            (readers: 0, pcLimit: 5, activeChars: 2, comments: 10, posts: 60),
            // Closed - Frozen (5) - spread 0-15
            (readers: 14, pcLimit: 6, activeChars: 0, comments: 12, posts: 65),
            (readers: 8, pcLimit: 4, activeChars: 0, comments: 8, posts: 40),
            (readers: 4, pcLimit: 5, activeChars: 0, comments: 5, posts: 25),
            (readers: 2, pcLimit: 4, activeChars: 0, comments: 4, posts: 20),
            (readers: 0, pcLimit: 5, activeChars: 0, comments: 3, posts: 15),
            // Closed - None (3) - spread 0-8
            (readers: 7, pcLimit: (int?)null, activeChars: 0, comments: 8, posts: 40),
            (readers: 3, pcLimit: 4, activeChars: 0, comments: 5, posts: 25),
            (readers: 0, pcLimit: 5, activeChars: 0, comments: 3, posts: 15),
            // Additional games for remaining tags (11) - varied spread
            (readers: 12, pcLimit: (int?)null, activeChars: 0, comments: 10, posts: 30), // Сотворение Миров
            (readers: 6, pcLimit: 4, activeChars: 0, comments: 6, posts: 20), // Апокалипсис: День Ноль
            (readers: 18, pcLimit: (int?)null, activeChars: 0, comments: 15, posts: 0), // Клуб Анонимных Убийц (Mafia)
            (readers: 8, pcLimit: 5, activeChars: 0, comments: 8, posts: 35), // Безумные Приключения
            (readers: 4, pcLimit: 4, activeChars: 0, comments: 5, posts: 25), // Эра Водолея: Пробуждение
            (readers: 2, pcLimit: 5, activeChars: 0, comments: 4, posts: 20), // FUDGE: Универсум
            (readers: 15, pcLimit: 5, activeChars: 0, comments: 12, posts: 60), // Паровая Империя
            (readers: 3, pcLimit: 4, activeChars: 0, comments: 5, posts: 25), // За Гранью Реальности
            (readers: 5, pcLimit: 4, activeChars: 0, comments: 6, posts: 30), // Black Bird
            (readers: 1, pcLimit: 4, activeChars: 0, comments: 3, posts: 15), // Темные Страсти
            (readers: 0, pcLimit: 3, activeChars: 0, comments: 2, posts: 12), // Кровавый Карнавал
        };

        var createdLargePost = false; // Track if we've used the large Diopside post
        var createdDiopsideCharacter = false; // Track if we've created the Diopside character

        for (var gi = 0; gi < gameTemplates.Length; gi++)
        {
            var template = gameTemplates[gi];

            // Skip if game with this title already exists
            if (existingTitles.Contains(template.Title))
            {
                var existingGame = existingGames.First(g => g.Title == template.Title);
                gameIds.Add(existingGame.GameId);
                continue;
            }

            var variation = gameVariations[gi % gameVariations.Length];
            var isNewbieMaster = template.Premod == PremoderationStatus.AwaitingApproval;
            var master = isNewbieMaster && newbieUsers.Count > 0
                ? newbieUsers[_random.Next(newbieUsers.Count)]
                : experiencedUsers[gi % experiencedUsers.Count];

            // First game carries the game-zone UI test fixtures (archived room,
            // private-room access split, post pendency, unread counter, chat
            // messages, master notepad) so a reseed always has one deterministic
            // game to verify those UI states against. Gated on the template
            // title (not just gi==0) so the fixtures stay attached to the same
            // game if templates are reordered.
            var isUiTestDataGame = template.Title == "Хроники Забытых Королевств";

            // Generate realistic dates based on game status
            // Draft: recent (1-30 days ago)
            // Active recruiting: medium age (30-90 days), activated recently
            // Active established: older (60-180 days), running for a while
            // Closed Finished: old (180-365 days), ran their full course
            // Closed Frozen: medium-old (90-300 days), abandoned mid-way
            DateTimeOffset gameCreatedUtc;
            DateTimeOffset? gameActivatedUtc;
            DateTimeOffset? gameClosedUtc;

            if (template.Status == ModuleStatus.Draft)
            {
                // Drafts are recent - people working on them
                gameCreatedUtc = now.AddDays(-_random.Next(1, 30));
                gameActivatedUtc = null;
                gameClosedUtc = null;
            }
            else if (template.Status == ModuleStatus.Active)
            {
                if (gi < 4)
                {
                    // First 4 active games are "new" (activated within 7 days)
                    var daysAgoCreated = _random.Next(7, 30);
                    gameCreatedUtc = now.AddDays(-daysAgoCreated);
                    gameActivatedUtc = now.AddDays(-(gi + 1)); // 1-4 days ago
                }
                else if (template.IsRecruitmentOpen)
                {
                    // Recruiting games: recently started, looking for players
                    var daysAgoCreated = _random.Next(30, 90);
                    gameCreatedUtc = now.AddDays(-daysAgoCreated);
                    var activationDelay = _random.Next(3, Math.Min(15, daysAgoCreated - 1));
                    gameActivatedUtc = gameCreatedUtc.AddDays(activationDelay);
                }
                else
                {
                    // Established games: running for a while
                    var daysAgoCreated = _random.Next(60, 180);
                    gameCreatedUtc = now.AddDays(-daysAgoCreated);
                    var activationDelay = _random.Next(5, Math.Min(30, daysAgoCreated - 1));
                    gameActivatedUtc = gameCreatedUtc.AddDays(activationDelay);
                }
                gameClosedUtc = null;
            }
            else // Closed
            {
                if (template.ClosedReason == ClosedReason.Finished)
                {
                    // Finished games: old, ran their full course
                    var daysAgoCreated = _random.Next(180, 365);
                    gameCreatedUtc = now.AddDays(-daysAgoCreated);
                    var activationDelay = _random.Next(5, 20);
                    gameActivatedUtc = gameCreatedUtc.AddDays(activationDelay);
                    // Closed recently (within last 90 days)
                    var daysAgoSinceActivated = (int)(now - gameActivatedUtc.Value).TotalDays;
                    var minDaysRunning = Math.Min(60, daysAgoSinceActivated - 1);
                    var closedDaysAgo = _random.Next(1, Math.Max(2, Math.Min(90, daysAgoSinceActivated - minDaysRunning)));
                    gameClosedUtc = now.AddDays(-closedDaysAgo);
                }
                else // Frozen
                {
                    // Frozen games: abandoned mid-way
                    var daysAgoCreated = _random.Next(90, 300);
                    gameCreatedUtc = now.AddDays(-daysAgoCreated);
                    var activationDelay = _random.Next(3, 15);
                    gameActivatedUtc = gameCreatedUtc.AddDays(activationDelay);
                    // Frozen some time ago (30-180 days)
                    var daysAgoSinceActivated = (int)(now - gameActivatedUtc.Value).TotalDays;
                    var closedDaysAgo = _random.Next(30, Math.Max(31, Math.Min(180, daysAgoSinceActivated - 10)));
                    gameClosedUtc = now.AddDays(-closedDaysAgo);
                }
            }

            // Calculate recruitment start date (if recruiting, started after activation)
            DateTimeOffset? recruitmentStartedUtc = null;
            if (template.IsRecruitmentOpen && gameActivatedUtc.HasValue)
            {
                var daysSinceActivation = (int)(now - gameActivatedUtc.Value).TotalDays;
                var recruitmentStartDaysAgo = _random.Next(1, Math.Max(2, Math.Min(30, daysSinceActivation)));
                recruitmentStartedUtc = now.AddDays(-recruitmentStartDaysAgo);
            }

            var game = new DbGame
            {
                GameId = _guidFactory.Create(),
                CreatedUtc = gameCreatedUtc,
                ActivatedUtc = gameActivatedUtc,
                Status = template.Status,
                PremoderationStatus = template.Premod,
                ClosedReason = template.ClosedReason,
                DraftVisibility = template.DraftVisibility,
                IsRecruitmentOpen = template.IsRecruitmentOpen,
                RecruitmentCount = template.IsRecruitmentOpen
                    ? (gi % 3 == 1 ? 2 : gi % 5 == 0 ? 3 : 1) // Mix of initial (1) and subsequent (2-3) recruitment
                    : 0,
                RecruitmentPcLimit = variation.pcLimit,
                RecruitmentStartedUtc = recruitmentStartedUtc,
                ClosedUtc = gameClosedUtc,
                MasterId = master.UserId,
                MentorId = isNewbieMaster ? mentor.UserId : null,
                AttributeSchemaId = SystemSchemaId,
                Title = template.Title,
                SystemName = template.System,
                NarrativeSetting = template.Setting,
                Info = $"Добро пожаловать в игру '{template.Title}'! Система: {template.System}, сеттинг: {template.Setting}. Здесь вас ждут захватывающие приключения и интересные персонажи.",
                HideDiceResult = false,
                ShowPrivateMessages = false,
                HidePostStats = false,
                CommentsAccessMode = CommentsAccessMode.Public,
                CommentCount = 0,
                // Temporary placeholder - will be updated after SaveChanges
                PublicId = $"t{_guidFactory.Create():N}"[..10]
            };

            _dbContext.Set<DbGame>().Add(game);
            gameIds.Add(game.GameId);
            result.GamesCreated++;

            // Add tags to game
            foreach (var tagId in template.Tags)
            {
                _dbContext.Set<GameTag>().Add(new GameTag
                {
                    GameTagId = _guidFactory.Create(),
                    GameId = game.GameId,
                    TagId = tagId
                });
            }

            // Skip detailed content for drafts
            if (template.Status == ModuleStatus.Draft) continue;

            // Add assistants (realistic distribution: ~20% have 1, ~5% have 2)
            // Using modulo for deterministic distribution: every 5th game gets 1 assistant, every 20th gets 2
            // Half the newbie threshold: an assistant is somebody with a track record,
            // and the fixture says so through the rule rather than through a number of
            // its own, which would answer differently the day the rule moves.
            const int assistantPostFloor = ProbationPolicy.NewbiePostThreshold / 2;
            var availableForAssistant = users
                .Where(u => u.UserId != master.UserId && u.QuantityRating >= assistantPostFloor)
                .ToList();
            var assistantCount = gi % 20 == 0 && availableForAssistant.Count >= 2 ? 2
                : gi % 5 == 0 && availableForAssistant.Count >= 1 ? 1
                : 0;
            if (assistantCount > 0)
            {
                var selectedAssistants = availableForAssistant.OrderBy(_ => _random.Next()).Take(assistantCount).ToList();
                foreach (var assistant in selectedAssistants)
                {
                    _dbContext.Set<GameAssistant>().Add(new GameAssistant
                    {
                        GameAssistantId = _guidFactory.Create(),
                        GameId = game.GameId,
                        UserId = assistant.UserId,
                        JoinedUtc = game.CreatedUtc.AddDays(1 + selectedAssistants.IndexOf(assistant))
                    });
                }
            }

            // Room names derived from the game setting so the seed reads as
            // a real tabletop session, not a messenger app. Generic names
            // like "Общий чат" / "Главная локация" were explicitly rejected
            // — they break immersion when browsing the seeded game list.
            // Falls back to the TRPG-neutral "За ширмой" pool when the
            // setting has no bespoke entry.
            var (mainRoomTitle, chatRoomTitle, secretRoomTitle) = template.Setting switch
            {
                "Forgotten Realms" => ("Таверна 'Полумесяц'", "Кулуары авантюристов", "Тайный алтарь"),
                "Golarion" => ("Постоялый двор", "Беседка мастера", "Тайная библиотека"),
                "Ravenloft" => ("Замок Равенлофт", "Кабинет у камина", "Крипта"),
                "Deadlands" => ("Салун 'Кровавая Мэри'", "Крыльцо салуна", "Заброшенная шахта"),
                "Rokugan" => ("Чайная комната", "Сад камней", "Тайная келья"),
                "1920s Arkham" => ("Гостиная профессора", "Читальный зал", "Запретный архив"),
                "Dark Millennium" => ("Рубка 'Справедливости'", "Казарма экипажа", "Криптекс"),
                "Night City 2077" => ("Бар 'Афтерлайф'", "Переулок NC", "Темная клиника"),
                "Mythic Scandinavia" => ("Длинный дом", "Костер йотунов", "Руны предков"),
                "Theah" => ("Капитанская каюта", "Нижняя палуба", "Тайник капитана"),
                "The Witcher" => ("Трактир 'Серебряный медведь'", "Лагерь у костра", "Подземелье знахаря"),
                "Fallout" => ("Убежище", "Радиорубка", "Тайный склад"),
                "Post-Apocalypse Moscow" => ("Станция 'ВДНХ'", "Костер в туннеле", "Забытый бункер"),
                "Modern Nights" => ("Клуб 'Эль Дорадо'", "Задний двор", "Тайное убежище"),
                "Modern Gothic" => ("Особняк у кладбища", "Галерея портретов", "Подвал хозяина"),
                "Camelot" => ("Большой зал Камелота", "Часовня Грааля", "Тайная палата короля"),
                "Arthurian" => ("Скрипторий", "Дубрава друидов", "Обитель отшельника"),
                "Ancient Egypt" => ("Храм Ра", "Двор пирамиды", "Саркофаг"),
                "Dark Sun" => ("Оазис Балик", "Тень скалы", "Пещера джинна"),
                "Al-Qadim" => ("Базар Хуззузы", "Сад визиря", "Потайной проход"),
                "Solar System 2350" => ("Рубка 'Ареса'", "Кают-компания", "Грузовой трюм"),
                "Far Future" => ("Мостик крейсера", "Обсервационная палуба", "Трюм"),
                "Military Sci-Fi" => ("Рубка десантного бота", "Кают-компания", "Оружейная"),
                "Cyberpunk Future" => ("Бар 'Неон'", "Задворки сети", "Серверная комната"),
                "Ravnica" => ("Зал гильдии", "Уличный рынок", "Подвалы Ордрувьяра"),
                _ => ("Главная сцена", "За ширмой", "Тайная комната"),
            };

            // Create rooms (without linking - links will be set after SaveChanges)
            var mainRoom = new Room
            {
                RoomId = _guidFactory.Create(),
                GameId = game.GameId,
                Title = mainRoomTitle,
                AccessType = RoomAccessType.Open,
                Type = RoomType.Default,
                RoomNumber = 1,
                OrderNumber = 1,
                ViewPrivateText = false,
                ViewDiceResults = true,
                DiceEnabled = true,
                IsRemoved = false
            };
            _dbContext.Set<Room>().Add(mainRoom);

            var chatRoom = new Room
            {
                RoomId = _guidFactory.Create(),
                GameId = game.GameId,
                Title = chatRoomTitle,
                AccessType = RoomAccessType.Open,
                Type = RoomType.Chat,
                RoomNumber = 2,
                OrderNumber = 2,
                ViewPrivateText = true,
                ViewDiceResults = true,
                DiceEnabled = false,
                IsRemoved = false
            };
            _dbContext.Set<Room>().Add(chatRoom);

            // Private room
            var privateRoom = new Room
            {
                RoomId = _guidFactory.Create(),
                GameId = game.GameId,
                Title = secretRoomTitle,
                AccessType = RoomAccessType.Private,
                Type = RoomType.Default,
                RoomNumber = 3,
                OrderNumber = 3,
                ViewPrivateText = false,
                ViewDiceResults = true,
                DiceEnabled = true,
                IsRemoved = false
            };
            _dbContext.Set<Room>().Add(privateRoom);

            // Every closed room of a game grants access to the same characters,
            // so the closed examples differ by room type and by nothing else.
            var restrictedRooms = new List<Room> { privateRoom };

            // UI test fixtures for the room list. The archived room gives the
            // "archived rooms" spoiler something to hide and reveal here while
            // every other seeded game keeps zero archived rooms. The closed chat
            // room completes the four examples the game menu is read against:
            // posts and messages, open and closed. It is seeded only here
            // because only this game gets a Chat with messages behind it, and a
            // chat room without one opens on "chat not found".
            Room? privateChatRoom = null;
            if (isUiTestDataGame)
            {
                var archivedRoom = new Room
                {
                    RoomId = _guidFactory.Create(),
                    GameId = game.GameId,
                    Title = "Пролог",
                    AccessType = RoomAccessType.Open,
                    Type = RoomType.Default,
                    RoomNumber = 4,
                    OrderNumber = 4,
                    ViewPrivateText = false,
                    ViewDiceResults = true,
                    DiceEnabled = true,
                    IsArchived = true,
                    IsRemoved = false
                };
                _dbContext.Set<Room>().Add(archivedRoom);

                // The open chat room field for field, access aside: the pair is
                // there to be compared. The title echoes the closed post room of
                // this setting ("Тайный алтарь"), the same way "Пролог" is
                // written for this game rather than taken from the table.
                privateChatRoom = new Room
                {
                    RoomId = _guidFactory.Create(),
                    GameId = game.GameId,
                    Title = "Шепот у алтаря",
                    AccessType = RoomAccessType.Private,
                    Type = RoomType.Chat,
                    RoomNumber = 5,
                    OrderNumber = 5,
                    ViewPrivateText = true,
                    ViewDiceResults = true,
                    DiceEnabled = false,
                    IsRemoved = false
                };
                _dbContext.Set<Room>().Add(privateChatRoom);
                restrictedRooms.Add(privateChatRoom);
            }

            // Create characters - use variation.activeChars for active character count
            var characterNames = new[] { "Арагорн Следопыт", "Эльвира Чародейка", "Горим Железный Кулак", "Лиара Тенебраум", "Кассандра Видящая", "Торин Дубощит", "Леголас Зеленый Лист", "Гимли сын Глоина" };
            var races = new[] { "Человек", "Эльф", "Дварф", "Полуэльф", "Тифлинг", "Гном", "Полуорк", "Драконорожденный" };
            var classes = new[] { "Следопыт", "Маг", "Воин", "Плут", "Жрец", "Паладин", "Бард", "Варвар" };

            var playersForGame = users.Where(u => u.UserId != master.UserId).OrderBy(_ => _random.Next()).Take(variation.activeChars + 2).ToList();
            var createdCharacters = new List<Character>();
            // Characters holding access to the closed rooms: whoever writes
            // there has to be able to read there.
            var restrictedRoomMembers = new List<Character>();

            // Create active characters based on variation
            for (var ci = 0; ci < Math.Min(variation.activeChars, playersForGame.Count); ci++)
            {
                var player = playersForGame[ci];

                // Create Diopside character for first active char of first finished game (for large post test)
                var isDiopsideChar = !createdDiopsideCharacter && ci == 0
                    && template.ClosedReason == ClosedReason.Finished;
                if (isDiopsideChar) createdDiopsideCharacter = true;

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
                    Name = isDiopsideChar ? "Диопсид" : characterNames[ci % characterNames.Length],
                    IsNpc = false,
                    AccessPolicy = CharacterAccessPolicy.NoAccess,
                    IsRemoved = false
                };

                _dbContext.Set<Character>().Add(character);
                AddLegacyCharacterAttributes(character.CharacterId,
                    race: isDiopsideChar ? "Кристаллическая сущность" : races[ci % races.Length],
                    @class: isDiopsideChar ? "Аберрация" : classes[ci % classes.Length],
                    alignmentIndex: template.System != "Cyberpunk RED" ? ci % AlignmentNames.Length : null,
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

                // Add avatar for the Diopside character — the real pipeline:
                // EXIF strip + downscale, one source object lands in MinIO.
                if (isDiopsideChar)
                {
                    var bytes = ReadEmbeddedSeedBytes("DM.Tools.Seeder.Assets.Seed.diopside.jpg");
                    var upload = await SeedAvatarFromBytesAsync(
                        bytes,
                        declaredContentType: "image/jpeg",
                        sourceFileName: "diopside.jpg",
                        type: UploadType.CharacterAvatar,
                        uploadId: _guidFactory.Create(),
                        userId: player.UserId,
                        entityId: character.CharacterId,
                        now: character.CreatedUtc);
                    _dbContext.Set<DM.Infrastructure.Persistence.Entities.Shared.Upload>().Add(upload);
                }

                // Add room access for active characters — except the last active
                // character of the UI test data game, so that game's closed
                // rooms show both a granted player (green lock) and a denied
                // one (grey lock) instead of everyone having access.
                var denyPrivateAccessForUiTest = isUiTestDataGame &&
                    ci == Math.Min(variation.activeChars, playersForGame.Count) - 1;
                if (!denyPrivateAccessForUiTest)
                {
                    foreach (var restrictedRoom in restrictedRooms)
                    {
                        _dbContext.Set<RoomAccess>().Add(new RoomAccess
                        {
                            AccessId = _guidFactory.Create(),
                            RoomId = restrictedRoom.RoomId,
                            CharacterId = character.CharacterId,
                            ReaderUserId = null,
                            // The enum defaults to NoAccess and the policy is what
                            // admits writing: without it the members of the closed
                            // rooms read them and cannot post in them.
                            Policy = RoomAccessPolicy.Full
                        });
                    }

                    restrictedRoomMembers.Add(character);
                }
            }

            // Add a few non-active characters for variety (if we have more players)
            var nonActiveStatuses = new[] { CharacterStatus.UnderReview, CharacterStatus.Declined, CharacterStatus.Retired };
            for (var ci = variation.activeChars; ci < playersForGame.Count && ci < variation.activeChars + 2; ci++)
            {
                var player = playersForGame[ci];
                var status = nonActiveStatuses[(ci - variation.activeChars) % nonActiveStatuses.Length];
                var character = new Character
                {
                    CharacterId = _guidFactory.Create(),
                    GameId = game.GameId,
                    AuthorId = player.UserId,
                    Status = status,
                    IsDead = status == CharacterStatus.Retired,
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
                    alignmentIndex: template.System != "Cyberpunk RED" ? ci % AlignmentNames.Length : null,
                    appearance: "Среднего роста, ничем не примечательный.",
                    temper: "Спокойный и рассудительный.",
                    story: "История еще пишется...",
                    skills: "Базовые навыки выживания.",
                    inventory: "Простая одежда, кошелек с монетами.");
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

            // Create posts in rooms - use variation count
            var activeCharacters = createdCharacters.Where(c => c.Status == CharacterStatus.Active && !c.IsNpc).ToList();
            var allCharactersForPosts = createdCharacters.Where(c => c.Status == CharacterStatus.Active).ToList(); // Include NPC
            // Large post for testing content expansion on homepage (~2000 chars)
            // Character: Diopside - a crystalline creature from D&D
            const string largePostText = """
                Кристаллы вдоль позвоночника резонировали с магией этого места. Диопсид замер, позволяя своим многочисленным отросткам ощутить потоки силы, пронизывающие древние стены. Столетия ожидания в темных глубинах не прошли даром — его восприятие обострилось до предела.

                "Они близко", — мысль эхом прокатилась по кристаллической структуре его сознания.

                Существо бесшумно переместилось в тень ниши, позволяя полупрозрачному телу слиться с темнотой. Только тусклое мерцание выдавало его присутствие — да и то лишь тем, кто знал, куда смотреть.

                Группа авантюристов вошла в зал, не подозревая, что их изучают. Диопсид мог бы атаковать сразу — разорвать их на части щупальцами, впитать их эссенцию. Но нет. Эти смертные несли что-то интересное.

                Человеческая женщина в мантии держала посох с камнем на навершии. Камень пульсировал знакомой энергией. Осколок. Осколок того самого кристалла, который когда-то был частью родного мира Диопсида.

                Воспоминания нахлынули волной: бескрайние кристаллические поля под фиолетовым небом, симфония резонанса миллионов собратьев, вечный покой и гармония. А потом — разрыв, падение через пустоту, одиночество в этом чужом мире.

                Существо приняло решение.

                Диопсид отделился от тени, его отростки развернулись в жесте, который смертные могли интерпретировать как миролюбивый. Авантюристы отпрянули, хватаясь за оружие. Но Диопсид уже транслировал образы напрямую в их разумы: предложение сотрудничества, обмена. Знания этого мира в обмен на осколок дома.

                Первой поняла волшебница. Ее глаза расширились — не от страха, а от удивления.

                "Ты... ты разумен?" — ее голос дрожал.

                Диопсид позволил своим кристаллам зазвучать в подобии смеха. Разумен? Он помнил эпохи, когда предки этих существ еще не спустились с деревьев. Но объяснять все это было бы слишком долго.

                Вместо этого он сформировал простой образ: рукопожатие.
                """;

            var postTexts = new[]
            {
                "Я осторожно оглядываюсь по сторонам, держа руку на рукояти меча. Что-то здесь не так...",
                "Произношу заклинание обнаружения магии, пытаясь понять природу этого места.",
                "Проверяю следы на земле. Кто-то здесь был совсем недавно.",
                "Подхожу к двери и прислушиваюсь. За ней слышны приглушенные голоса.",
                "Готовлю щит и занимаю оборонительную позицию.",
                "Внимательно осматриваю комнату в поисках скрытых проходов.",
                "Пытаюсь вспомнить, что я знаю об этом месте из старых легенд.",
                "Достаю факел и освещаю темный угол помещения.",
                "Жестом показываю спутникам, чтобы они были настороже.",
                "Прислоняюсь к стене и перевожу дух после долгого пути.",
                "Изучаю странные символы на стенах - похоже на древний язык.",
                "Проверяю свои запасы и пересчитываю оставшиеся стрелы.",
            };
            var masterTexts = new[]
            {
                "Таинственная фигура выходит из тени. 'Вы пришли за ответами? Возможно, я могу помочь... за определенную цену.'",
                "Внезапно раздается громкий скрежет - каменная дверь начинает медленно опускаться!",
                "Свет факелов мерцает, и на мгновение вам кажется, что тени на стенах двигаются сами по себе.",
                "Издалека доносится приглушенный рев - что-то большое бродит в этих коридорах.",
                "На полу вы замечаете свежие следы крови, ведущие вглубь подземелья.",
            };

            var postsToCreate = variation.posts;
            if (postsToCreate > 0 && allCharactersForPosts.Count > 0)
            {
                var baseTime = game.ActivatedUtc!.Value;
                // Spread posts evenly between activation and now (or closed date)
                var endTime = game.ClosedUtc ?? now;
                var totalHoursAvailable = (int)(endTime - baseTime).TotalHours;
                var hoursPerPost = postsToCreate > 0 ? Math.Max(1, totalHoursAvailable / postsToCreate) : 1;

                for (var pi = 0; pi < postsToCreate; pi++)
                {
                    var character = allCharactersForPosts[pi % allCharactersForPosts.Count];
                    var isMasterPost = character.IsNpc;
                    // Calculate post time, ensuring it doesn't exceed end time
                    var postOffsetHours = Math.Min(pi * hoursPerPost + _random.Next(0, hoursPerPost), totalHoursAvailable - 1);
                    // Use large Diopside post for first post of first finished game
                    var useLargePost = !createdLargePost && pi == 0
                        && template.ClosedReason == ClosedReason.Finished && !isMasterPost;
                    if (useLargePost) createdLargePost = true;

                    var post = new Post
                    {
                        PostId = _guidFactory.Create(),
                        RoomId = mainRoom.RoomId,
                        CharacterId = character.CharacterId,
                        AuthorId = character.AuthorId ?? master.UserId,
                        CreatedUtc = baseTime.AddHours(postOffsetHours),
                        GameText = useLargePost
                            ? largePostText
                            : isMasterPost
                                ? masterTexts[_random.Next(masterTexts.Length)]
                                : postTexts[_random.Next(postTexts.Length)],
                        MetagameText = useLargePost
                            ? "Пробный пост для тестирования отображения на главной. Вроде норм получилось!"
                            : _random.Next(4) == 0 ? "Интересный поворот!" : null,
                        IsRemoved = false
                    };
                    _dbContext.Set<Post>().Add(post);
                    result.PostsCreated++;

                    // Add dice rolls for the large Diopside post
                    if (useLargePost)
                    {
                        var diceCollection = _mongoClient.GetCollection<DiceRoll>();
                        var diceRolls = new[]
                        {
                            new DiceRoll
                            {
                                Id = _guidFactory.Create(),
                                PostId = post.PostId,
                                CreatedUtc = post.CreatedUtc.UtcDateTime,
                                DiceCount = 1,
                                EdgesCount = 20,
                                Bonus = 7,
                                Comment = "Восприятие",
                                Result = [new RollResult { Value = 18, IsCritical = false, IsExploded = false }]
                            },
                            new DiceRoll
                            {
                                Id = _guidFactory.Create(),
                                PostId = post.PostId,
                                CreatedUtc = post.CreatedUtc.UtcDateTime.AddSeconds(1),
                                DiceCount = 1,
                                EdgesCount = 20,
                                Bonus = 5,
                                Comment = "Убеждение",
                                Result = [new RollResult { Value = 14, IsCritical = false, IsExploded = false }]
                            }
                        };
                        await diceCollection.InsertManyAsync(diceRolls);
                    }

                    // Add edit history for ~10% of posts (including the large Diopside post)
                    if (useLargePost || _random.Next(10) == 0)
                    {
                        _dbContext.Set<PostEdit>().Add(new PostEdit
                        {
                            PostEditId = _guidFactory.Create(),
                            PostId = post.PostId,
                            EditorUserId = post.AuthorId,
                            ModifiedUtc = post.CreatedUtc.AddMinutes(_random.Next(5, 60))
                        });
                    }

                    // Increment author's QuantityRating
                    var authorId = character.AuthorId ?? master.UserId;
                    var author = users.First(u => u.UserId == authorId);
                    author.QuantityRating++;
                }
            }

            // UI test fixtures for the designated game: post pendency, unread
            // counter, closed room posts, chat room messages and master notepad
            // entries. Kept in one place so a reseed always gives QA the same
            // deterministic game to check these UI states against.
            // The LastMessageId of a chat is applied only after the batch save:
            // setting it while both Chat and Message are still Added would make
            // EF detect a circular FK dependency (Chat.LastMessageId <->
            // Message.ChatId) and fail the whole seed. It is a list because this
            // game seeds two chats, the open one and the closed one.
            var chatsAwaitingLastMessage = new List<(Chat Chat, Guid LastMessageId)>();
            if (isUiTestDataGame)
            {
                var waitingPlayer = playersForGame[0];
                var waitingCharacter = createdCharacters[0];

                // Open post pendency awaiting a seeded player - shows the red
                // waiting-star with tooltip until they post or it's fulfilled.
                _dbContext.Set<PostPendency>().Add(new PostPendency
                {
                    PendencyId = _guidFactory.Create(),
                    RoomId = mainRoom.RoomId,
                    CharacterId = waitingCharacter.CharacterId,
                    WaitingForUserId = waitingPlayer.UserId,
                    CreatedById = master.UserId,
                    CreatedUtc = now.AddDays(-2),
                    FulfilledUtc = null,
                    LastReminderUtc = null
                });

                // Baseline unread counter for the main room so any user who
                // never visited it (i.e. everyone but its own post authors)
                // sees the (N) unread badge instead of a silent zero.
                if (postsToCreate > 0)
                {
                    var unreadCounters = _mongoClient.GetCollection<UnreadCounter>();
                    await unreadCounters.InsertOneAsync(new UnreadCounter
                    {
                        UserId = Guid.Empty,
                        EntityId = mainRoom.RoomId,
                        ParentId = game.GameId,
                        EntryType = UnreadEntryType.Message,
                        LastReadUtc = game.ActivatedUtc!.Value.UtcDateTime,
                        Counter = Math.Min(postsToCreate, 5),
                        IsRemoved = false
                    });
                }

                // A closed room with a lock and an empty page is not an example
                // of a closed room, so the private post room gets a scene of its
                // own. It is written by the NPC the master speaks through and by
                // the characters that hold access, so nobody posts where nobody
                // can read.
                var closedRoomPostTexts = new[]
                {
                    "Тяжелая дверь закрывается за вашими спинами. Камень на алтаре светится ровным холодным светом.",
                    "Подхожу ближе и осматриваю алтарь, стараясь ничего не задеть.",
                    "Встаю у двери и слушаю коридор. Если кто-то пойдет следом, услышу первым.",
                };
                var closedRoomAuthors = new[] { npc }.Concat(restrictedRoomMembers).ToList();
                for (var pi = 0; pi < closedRoomPostTexts.Length; pi++)
                {
                    var closedRoomCharacter = closedRoomAuthors[pi % closedRoomAuthors.Count];
                    var closedRoomAuthorId = closedRoomCharacter.AuthorId ?? master.UserId;
                    _dbContext.Set<Post>().Add(new Post
                    {
                        PostId = _guidFactory.Create(),
                        RoomId = privateRoom.RoomId,
                        CharacterId = closedRoomCharacter.CharacterId,
                        AuthorId = closedRoomAuthorId,
                        CreatedUtc = now.AddDays(-(closedRoomPostTexts.Length - pi)),
                        GameText = closedRoomPostTexts[pi],
                        IsRemoved = false
                    });
                    result.PostsCreated++;
                    users.First(u => u.UserId == closedRoomAuthorId).QuantityRating++;
                }

                // Both chat rooms are filled through one helper: the open and the
                // closed example differ by who may open them and by what is said
                // in them, not by how the chat behind them is built.
                void SeedRoomChat(Room room, IReadOnlyList<Guid> speakerIds, IReadOnlyList<string> texts)
                {
                    var roomChat = new Chat
                    {
                        ChatId = _guidFactory.Create(),
                        Type = ChatType.GameRoom,
                        Title = room.Title,
                        RoomId = room.RoomId
                    };
                    _dbContext.Set<Chat>().Add(roomChat);
                    room.ChatId = roomChat.ChatId;

                    Message? lastRoomMessage = null;
                    for (var mi = 0; mi < texts.Count; mi++)
                    {
                        var roomMessage = new Message
                        {
                            MessageId = _guidFactory.Create(),
                            UserId = speakerIds[mi % speakerIds.Count],
                            ChatId = roomChat.ChatId,
                            CreatedUtc = now.AddHours(-(texts.Count - mi) * 3),
                            Text = texts[mi],
                            IsRemoved = false
                        };
                        _dbContext.Set<Message>().Add(roomMessage);
                        lastRoomMessage = roomMessage;
                        result.MessagesCreated++;
                    }

                    if (lastRoomMessage != null)
                    {
                        chatsAwaitingLastMessage.Add((roomChat, lastRoomMessage.MessageId));
                    }
                }

                // Open chat room: a short OOC exchange between the master and a
                // couple of players, so the chat page and its cursor pagination
                // have something to render.
                SeedRoomChat(
                    chatRoom,
                    new[] { master.UserId }.Concat(playersForGame.Take(2).Select(p => p.UserId)).ToList(),
                    new[]
                    {
                        "Народ, всем удобно новое время постинга?",
                        "Да, вроде норм, буду успевать чаще писать.",
                        "Класс! Тогда продолжаем в том же духе.",
                        "Кстати, кто-нибудь помнит, где мы в прошлый раз остановились?",
                        "Я вроде помню - у ворот перед встречей с торговцем.",
                        "Точно, спасибо! Сейчас напишу пост.",
                    });

                // Closed chat room, created above under the same flag: the same
                // kind of exchange, but only between the master and the players
                // holding access, so the two chat examples differ in the lock
                // and not in what is behind it.
                SeedRoomChat(
                    privateChatRoom!,
                    new[] { master.UserId }
                        .Concat(restrictedRoomMembers.Take(2).Select(c => c.AuthorId ?? master.UserId))
                        .ToList(),
                    new[]
                    {
                        "Тут только те, у кого есть доступ. Про алтарь при остальных не пишем.",
                        "Принято. Мой персонаж делает вид, что ничего не заметил.",
                        "А жрец успеет добежать до алтаря за один ход?",
                        "Успеет, если не потратите ход на спор у двери.",
                    });

                // Master notepad entries (game "Заметки")
                var masterNotepadEntries = new[]
                {
                    ("Зацепки сюжета", "Торговец на въезде в город знает больше, чем говорит - потянуть за эту нить через пару постов."),
                    ("NPC на подхвате", "Таинственный Незнакомец - держать интригу, не раскрывать личность раньше времени."),
                    ("Заметка для себя", "Не забыть напомнить группе про открытую заявку на персонажа - висит уже несколько дней."),
                };
                for (var ni = 0; ni < masterNotepadEntries.Length; ni++)
                {
                    var (noteTitle, noteContent) = masterNotepadEntries[ni];
                    _dbContext.Set<NotepadEntry>().Add(new NotepadEntry
                    {
                        EntryId = _guidFactory.Create(),
                        NotepadType = NotepadType.Master,
                        ContainerId = game.GameId,
                        OwnerId = null,
                        AuthorId = master.UserId,
                        Title = noteTitle,
                        Content = noteContent,
                        SortOrder = ni,
                        CreatedUtc = now.AddDays(-(masterNotepadEntries.Length - ni)),
                        IsRemoved = false
                    });
                }

                // Player notepad entries owned by the first active character,
                // so the player scope of the game "Заметки" page has data too.
                var playerNotepadEntries = new[]
                {
                    ("План на арку", "Разговорить торговца у ворот и выяснить, что он скрывает - мой персонаж ему не доверяет."),
                    ("Список долгов", "Должен трактирщику 12 золотых. Вернуть после следующей вылазки, пока он сам не вспомнил."),
                };
                for (var ni = 0; ni < playerNotepadEntries.Length; ni++)
                {
                    var (noteTitle, noteContent) = playerNotepadEntries[ni];
                    _dbContext.Set<NotepadEntry>().Add(new NotepadEntry
                    {
                        EntryId = _guidFactory.Create(),
                        NotepadType = NotepadType.Player,
                        ContainerId = game.GameId,
                        OwnerId = waitingCharacter.CharacterId,
                        AuthorId = waitingCharacter.AuthorId ?? waitingPlayer.UserId,
                        Title = noteTitle,
                        Content = noteContent,
                        SortOrder = ni,
                        CreatedUtc = now.AddDays(-(playerNotepadEntries.Length - ni)).AddHours(2),
                        IsRemoved = false
                    });
                }
            }

            // Add readers (subscriptions) - use variation count
            var potentialReaders = users.Where(u => u.UserId != master.UserId && !playersForGame.Contains(u)).ToList();
            var readersToAdd = Math.Min(variation.readers, potentialReaders.Count);
            var readers = potentialReaders.OrderBy(_ => _random.Next()).Take(readersToAdd).ToList();
            foreach (var reader in readers)
            {
                _dbContext.Set<Subscription>().Add(new Subscription
                {
                    SubscriptionId = _guidFactory.Create(),
                    SubscriberId = reader.UserId,
                    TargetType = SubscriptionTargetType.Game,
                    TargetId = game.GameId,
                    Settings = SubscriptionSettings.None,
                    CreatedUtc = game.CreatedUtc.AddDays(_random.Next(1, 14))
                });
            }

            // Add game comments
            var gameCommentTexts = new[]
            {
                "Отличная игра, мастер молодец!",
                "Сюжет очень интересный, жду продолжения.",
                "Спасибо за атмосферу, чувствуется погружение.",
                "Персонажи прописаны очень хорошо.",
                "Хорошая динамика, не скучно.",
            };

            // Use variation for comment count
            var gameCommentsToCreate = variation.comments;
            var commentBaseTime = game.ActivatedUtc ?? game.CreatedUtc;
            var commentEndTime = game.ClosedUtc ?? now;
            var commentHoursAvailable = (int)(commentEndTime - commentBaseTime).TotalHours;
            var hoursPerComment = gameCommentsToCreate > 0 ? Math.Max(1, commentHoursAvailable / gameCommentsToCreate) : 1;

            for (var j = 0; j < gameCommentsToCreate; j++)
            {
                var commentOffsetHours = Math.Min(j * hoursPerComment + _random.Next(0, hoursPerComment), Math.Max(1, commentHoursAvailable - 1));
                var commentAuthor = users[_random.Next(users.Count)];
                var gameComment = new DbComment
                {
                    CommentId = _guidFactory.Create(),
                    EntityId = game.GameId,
                    AuthorId = commentAuthor.UserId,
                    CreatedUtc = commentBaseTime.AddHours(commentOffsetHours),
                    Text = gameCommentTexts[_random.Next(gameCommentTexts.Length)],
                    IsRemoved = false
                };

                _dbContext.Set<DbComment>().Add(gameComment);
                game.CommentCount++;
                game.LastCommentId = gameComment.CommentId;
            }

            // Batch save after each game to avoid memory pressure from thousands of tracked entities
            await _dbContext.SaveChangesAsync();

            // Now that every chat and its messages exist, link the last message
            // of each (deferred to break the Chat <-> Message FK cycle). The
            // PublicId save below persists them.
            foreach (var (chat, lastMessageId) in chatsAwaitingLastMessage)
            {
                chat.LastMessageId = lastMessageId;
            }

            // Reload the game to get the auto-generated SerialNumber
            await _dbContext.Entry(game).ReloadAsync();

            // Update PublicId from SerialNumber (which was auto-generated on insert)
            game.PublicId = _publicIdService.Encode(game.SerialNumber);
            await _dbContext.SaveChangesAsync();
        }

        var skippedGames = existingTitles.Count;
        if (result.GamesCreated > 0)
        {
            result.Details.Add($"Created {result.GamesCreated} new games (skipped {skippedGames} existing)");
        }
        else if (skippedGames > 0)
        {
            result.Details.Add($"All {skippedGames} games already exist");
        }

        // Check if any existing games need characters/posts seeded
        var gamesWithoutCharacters = existingGames.Where(g => g.Characters.Count == 0 && g.Status != ModuleStatus.Draft).ToList();
        if (gamesWithoutCharacters.Count > 0)
        {
            result.Details.Add($"Seeding characters/posts for {gamesWithoutCharacters.Count} existing games without characters");
            SeedCharactersAndPostsForGames(gamesWithoutCharacters, users, result);
            await _dbContext.SaveChangesAsync();
        }

        if (result.CharactersCreated > 0 || result.PostsCreated > 0)
        {
            result.Details.Add($"Created {result.CharactersCreated} characters and {result.PostsCreated} posts total");
        }

        return gameIds;
    }
}
