using System;
using DM.Domain.Core.Enums;

namespace DM.Tools.Seeder.Seeding;

internal sealed partial class DataSeeder
{
    private sealed record GameTemplate
    {
        public required string Title { get; init; }

        public required string System { get; init; }

        public required string Setting { get; init; }

        public required ModuleStatus Status { get; init; }

        public DraftVisibility DraftVisibility { get; init; } = DraftVisibility.Private;

        public bool IsRecruitmentOpen { get; init; }

        public ClosedReason ClosedReason { get; init; } = ClosedReason.None;

        public PremoderationStatus Premod { get; init; } = PremoderationStatus.Approved;

        public required Guid[] Tags { get; init; }

        public required GameVariation Variation { get; init; }

        public bool IsUiTestData { get; init; }
    }

    private sealed record GameVariation(int Readers, int? PcLimit, int ActiveChars, int Comments, int Posts);

    private static class GameCatalog
    {
        // Tag identifiers from the InitialCreate migration
        private static Guid T(int id) => FixedGuid(0, id);

        // System (01-19)
        private static readonly Guid tagBlackBirdPie = T(0x01), tagDnD = T(0x02), tagDnD5e = T(0x03), tagD100 = T(0x04), tagDawnOfWorlds = T(0x05);
        private static readonly Guid tagFallout = T(0x06), tagFate = T(0x08), tagFudge = T(0x09), tagGURPS = T(0x0a), tagInterlock = T(0x0b);
        private static readonly Guid tagPathfinder1e = T(0x0d), tagPathfinder2e = T(0x0e), tagPbtA = T(0x0f), tagRisus = T(0x10), tagSavageWorlds = T(0x11);
        private static readonly Guid tagStarfinder1e = T(0x12), tagStarfinder2e = T(0x13), tagWarhammer = T(0x14), tagWoD = T(0x15), tagAuthor = T(0x16);
        private static readonly Guid tagMafia = T(0x17), tagSloveski = T(0x18), tagEraVodolea = T(0x19);

        // Genre (1a-2b)
        private static readonly Guid tagAltHistory = T(0x1a), tagAction = T(0x1b), tagDetective = T(0x1c), tagZombie = T(0x1d), tagHistorical = T(0x1e);
        private static readonly Guid tagCyberpunk = T(0x1f), tagComedy = T(0x20), tagKosmoopera = T(0x21), tagMystic = T(0x22), tagModern = T(0x23);
        private static readonly Guid tagPostApoc = T(0x24), tagPsychedelic = T(0x25), tagSteampunk = T(0x26), tagThriller = T(0x27), tagTrash = T(0x28);
        private static readonly Guid tagHorror = T(0x29), tagSciFi = T(0x2a), tagFantasy = T(0x2b);

        // Game format (2c-32)
        private static readonly Guid tagDungeonCrawl = T(0x2c), tagPvP = T(0x2d), tagSurvival = T(0x2e), tagSandbox = T(0x2f), tagStrategy = T(0x30);
        private static readonly Guid tagPlotDriven = T(0x31), tagTactics = T(0x32);

        // Post format (33-34), Pace (35-36, 3e), Restrictions (37-3b), Newcomers (3c-3d), Sensitive (3f-41)
        private static readonly Guid tagShortPost = T(0x33), tagLiterary = T(0x34), tagSlowPace = T(0x35), tagFastPace = T(0x36), tagDrySeasons = T(0x3e);
        private static readonly Guid tagNoSwearing = T(0x37), tagNoViolence = T(0x38), tagGrammarNazi = T(0x39), tagPrivateGroup = T(0x3a), tagMessengerRequired = T(0x3b);
        private static readonly Guid tagForNewbies = T(0x3c), tagNewbieMaster = T(0x3d), tagErotica = T(0x3f), tagShockContent = T(0x40), tagSensitiveTopics = T(0x41);

        private const ModuleStatus Active = ModuleStatus.Active, Draft = ModuleStatus.Draft, Closed = ModuleStatus.Closed;
        private const DraftVisibility Public = DraftVisibility.Public;
        private const ClosedReason Finished = ClosedReason.Finished, Frozen = ClosedReason.Frozen;
        private const PremoderationStatus Awaiting = PremoderationStatus.AwaitingApproval;

        internal static readonly GameTemplate[] Templates =
        [
            // Active with recruitment: more than the sidebar limit of 10
            new() { Title = "Хроники Забытых Королевств", System = "D&D 5e", Setting = "Forgotten Realms", Status = Active, IsRecruitmentOpen = true, IsUiTestData = true, Tags = new[] { tagDnD5e, tagDnD, tagFantasy, tagPlotDriven, tagLiterary, tagSlowPace, tagDungeonCrawl, tagAction }, Variation = new(Readers: 28, PcLimit: 8, ActiveChars: 3, Comments: 12, Posts: 25) },
            new() { Title = "Врата Бездны", System = "Pathfinder 2e", Setting = "Golarion", Status = Active, IsRecruitmentOpen = true, Tags = new[] { tagPathfinder2e, tagFantasy, tagPlotDriven, tagTactics, tagAction, tagLiterary, tagSlowPace }, Variation = new(Readers: 22, PcLimit: 6, ActiveChars: 2, Comments: 10, Posts: 20) },
            new() { Title = "Последний Рубеж", System = "Savage Worlds", Setting = "Deadlands", Status = Active, IsRecruitmentOpen = true, Tags = new[] { tagSavageWorlds, tagForNewbies, tagFantasy, tagAltHistory, tagHorror, tagAction, tagShortPost, tagFastPace, tagMystic }, Variation = new(Readers: 18, PcLimit: 5, ActiveChars: 2, Comments: 8, Posts: 18) },
            new() { Title = "Путь Самурая", System = "Legend of the Five Rings", Setting = "Rokugan", Status = Active, IsRecruitmentOpen = true, Tags = new[] { tagAuthor, tagFantasy, tagPlotDriven, tagHistorical, tagLiterary, tagSlowPace, tagNoSwearing }, Variation = new(Readers: 15, PcLimit: 4, ActiveChars: 2, Comments: 7, Posts: 15) },
            new() { Title = "Королевство Теней", System = "D&D 5e", Setting = "Ravenloft", Status = Active, IsRecruitmentOpen = true, Tags = new[] { tagDnD5e, tagDnD, tagFantasy, tagForNewbies, tagHorror, tagMystic, tagPlotDriven, tagLiterary }, Variation = new(Readers: 12, PcLimit: 6, ActiveChars: 2, Comments: 6, Posts: 14) },
            new() { Title = "Колонисты Марса", System = "Stars Without Number", Setting = "Solar System 2350", Status = Active, IsRecruitmentOpen = true, Tags = new[] { tagStarfinder1e, tagSciFi, tagKosmoopera, tagSurvival, tagPlotDriven, tagShortPost, tagFastPace }, Variation = new(Readers: 10, PcLimit: 5, ActiveChars: 1, Comments: 5, Posts: 12) },
            new() { Title = "Пески Времени", System = "D&D 5e", Setting = "Al-Qadim", Status = Active, IsRecruitmentOpen = true, Tags = new[] { tagDnD5e, tagDnD, tagFantasy, tagHistorical, tagPlotDriven, tagDungeonCrawl, tagLiterary, tagMystic }, Variation = new(Readers: 8, PcLimit: 4, ActiveChars: 1, Comments: 4, Posts: 10) },
            new() { Title = "Зов Ктулху", System = "Call of Cthulhu", Setting = "1920s Arkham", Status = Active, IsRecruitmentOpen = true, Tags = new[] { tagD100, tagPlotDriven, tagLiterary, tagHorror, tagMystic, tagDetective, tagHistorical, tagSlowPace, tagGrammarNazi }, Variation = new(Readers: 6, PcLimit: 6, ActiveChars: 1, Comments: 4, Posts: 9) },
            new() { Title = "Механикум", System = "Warhammer 40k", Setting = "Dark Millennium", Status = Active, IsRecruitmentOpen = true, Tags = new[] { tagWarhammer, tagSciFi, tagPlotDriven, tagAction, tagTactics, tagLiterary, tagSlowPace }, Variation = new(Readers: 5, PcLimit: 5, ActiveChars: 1, Comments: 3, Posts: 8) },
            new() { Title = "Эльдорадо", System = "Savage Worlds", Setting = "Age of Sail", Status = Active, IsRecruitmentOpen = true, Tags = new[] { tagSavageWorlds, tagForNewbies, tagHistorical, tagAction, tagSandbox, tagShortPost, tagFastPace, tagComedy }, Variation = new(Readers: 4, PcLimit: 4, ActiveChars: 1, Comments: 3, Posts: 7) },
            new() { Title = "Наследие Драконов", System = "Pathfinder 2e", Setting = "Homebrew", Status = Active, IsRecruitmentOpen = true, Tags = new[] { tagPathfinder2e, tagFantasy, tagPlotDriven, tagAction, tagDungeonCrawl, tagLiterary, tagSlowPace }, Variation = new(Readers: 3, PcLimit: 6, ActiveChars: 1, Comments: 2, Posts: 6) },
            new() { Title = "Стальные Небеса", System = "Stars Without Number", Setting = "Cyberpunk Future", Status = Active, IsRecruitmentOpen = true, Tags = new[] { tagStarfinder2e, tagSciFi, tagCyberpunk, tagAction, tagPlotDriven, tagShortPost, tagFastPace, tagThriller }, Variation = new(Readers: 2, PcLimit: 5, ActiveChars: 1, Comments: 2, Posts: 5) },
            new() { Title = "Туманы Авалона", System = "Fate Core", Setting = "Arthurian", Status = Active, IsRecruitmentOpen = true, Tags = new[] { tagFate, tagFantasy, tagLiterary, tagMystic, tagHistorical, tagPlotDriven, tagSlowPace, tagGrammarNazi, tagNoViolence }, Variation = new(Readers: 1, PcLimit: 4, ActiveChars: 1, Comments: 2, Posts: 5) },
            new() { Title = "Проклятие Фараона", System = "GURPS", Setting = "Ancient Egypt", Status = Active, IsRecruitmentOpen = true, Tags = new[] { tagGURPS, tagPlotDriven, tagHistorical, tagMystic, tagDetective, tagLiterary, tagSlowPace }, Variation = new(Readers: 1, PcLimit: 6, ActiveChars: 1, Comments: 1, Posts: 4) },
            new() { Title = "Охотники за Тенями", System = "World of Darkness", Setting = "Modern Nights", Status = Active, IsRecruitmentOpen = true, Tags = new[] { tagWoD, tagLiterary, tagForNewbies, tagModern, tagHorror, tagMystic, tagAction, tagPlotDriven }, Variation = new(Readers: 0, PcLimit: 5, ActiveChars: 1, Comments: 1, Posts: 3) },
            // Active without recruitment
            new() { Title = "Тени Киберпанка", System = "Cyberpunk RED", Setting = "Night City 2077", Status = Active, Tags = new[] { tagInterlock, tagCyberpunk, tagSciFi, tagLiterary, tagAction, tagPlotDriven, tagThriller, tagSlowPace, tagMessengerRequired }, Variation = new(Readers: 30, PcLimit: 6, ActiveChars: 6, Comments: 45, Posts: 180) },
            new() { Title = "Империя Звезд", System = "Stars Without Number", Setting = "Far Future", Status = Active, Tags = new[] { tagStarfinder2e, tagSciFi, tagKosmoopera, tagPlotDriven, tagStrategy, tagLiterary, tagSlowPace, tagDrySeasons }, Variation = new(Readers: 25, PcLimit: 5, ActiveChars: 5, Comments: 38, Posts: 140) },
            new() { Title = "Клинки и Колдовство", System = "D&D 5e", Setting = "Dark Sun", Status = Active, Tags = new[] { tagDnD5e, tagDnD, tagFantasy, tagLiterary, tagSurvival, tagPostApoc, tagAction, tagSlowPace }, Variation = new(Readers: 20, PcLimit: 5, ActiveChars: 5, Comments: 32, Posts: 120) },
            new() { Title = "Сага о Викингах", System = "Fate Core", Setting = "Mythic Scandinavia", Status = Active, Tags = new[] { tagFate, tagFantasy, tagLiterary, tagHistorical, tagMystic, tagPlotDriven, tagAction, tagSlowPace }, Variation = new(Readers: 16, PcLimit: 4, ActiveChars: 4, Comments: 28, Posts: 100) },
            new() { Title = "Пираты Карибского Моря", System = "7th Sea", Setting = "Theah", Status = Active, Tags = new[] { tagAuthor, tagFantasy, tagPlotDriven, tagHistorical, tagAction, tagComedy, tagSandbox, tagShortPost }, Variation = new(Readers: 13, PcLimit: 5, ActiveChars: 4, Comments: 25, Posts: 90) },
            new() { Title = "Город Грехов", System = "World of Darkness", Setting = "Modern Gothic", Status = Active, Tags = new[] { tagWoD, tagLiterary, tagModern, tagHorror, tagMystic, tagThriller, tagPlotDriven, tagSlowPace, tagPrivateGroup, tagSensitiveTopics }, Variation = new(Readers: 10, PcLimit: null, ActiveChars: 4, Comments: 22, Posts: 80) },
            new() { Title = "Война Гильдий", System = "D&D 5e", Setting = "Ravnica", Status = Active, Tags = new[] { tagDnD5e, tagDnD, tagFantasy, tagPvP, tagStrategy, tagPlotDriven, tagTactics, tagSlowPace }, Variation = new(Readers: 8, PcLimit: 4, ActiveChars: 3, Comments: 18, Posts: 70) },
            new() { Title = "Метро 2033", System = "GURPS", Setting = "Post-Apocalypse Moscow", Status = Active, Tags = new[] { tagGURPS, tagPostApoc, tagHorror, tagSurvival, tagAction, tagPlotDriven, tagSlowPace, tagThriller }, Variation = new(Readers: 7, PcLimit: 5, ActiveChars: 3, Comments: 16, Posts: 65) },
            new() { Title = "Ведьмак: Дикая Охота", System = "Словеска", Setting = "The Witcher", Status = Active, Tags = new[] { tagSloveski, tagFantasy, tagLiterary, tagAction, tagMystic, tagPlotDriven, tagDetective, tagSlowPace }, Variation = new(Readers: 5, PcLimit: 4, ActiveChars: 3, Comments: 14, Posts: 55) },
            new() { Title = "Странники Пустоши", System = "Savage Worlds", Setting = "Fallout", Status = Active, Tags = new[] { tagFallout, tagSavageWorlds, tagPostApoc, tagSurvival, tagAction, tagSandbox, tagShortPost, tagFastPace }, Variation = new(Readers: 4, PcLimit: 5, ActiveChars: 3, Comments: 12, Posts: 50) },
            new() { Title = "Рыцари Круглого Стола", System = "Pendragon", Setting = "Camelot", Status = Active, Tags = new[] { tagAuthor, tagFantasy, tagPlotDriven, tagHistorical, tagLiterary, tagMystic, tagSlowPace, tagGrammarNazi }, Variation = new(Readers: 3, PcLimit: 4, ActiveChars: 2, Comments: 10, Posts: 45) },
            new() { Title = "Космические Волки", System = "Stars Without Number", Setting = "Military Sci-Fi", Status = Active, Tags = new[] { tagStarfinder1e, tagSciFi, tagKosmoopera, tagAction, tagTactics, tagPlotDriven, tagSlowPace }, Variation = new(Readers: 2, PcLimit: 5, ActiveChars: 2, Comments: 9, Posts: 40) },
            new() { Title = "Бегущий по Лезвию", System = "Cyberpunk RED", Setting = "Neo Tokyo", Status = Active, Tags = new[] { tagInterlock, tagCyberpunk, tagSciFi, tagAction, tagDetective, tagPlotDriven, tagThriller, tagLiterary }, Variation = new(Readers: 2, PcLimit: 4, ActiveChars: 2, Comments: 8, Posts: 35) },
            new() { Title = "Темное Средневековье", System = "D&D 5e", Setting = "Dark Ages Europe", Status = Active, Tags = new[] { tagDnD5e, tagDnD, tagFantasy, tagHistorical, tagHorror, tagMystic, tagPlotDriven, tagLiterary }, Variation = new(Readers: 1, PcLimit: 5, ActiveChars: 2, Comments: 7, Posts: 30) },
            new() { Title = "Час Волка", System = "World of Darkness", Setting = "Werewolf", Status = Active, Tags = new[] { tagWoD, tagLiterary, tagPlotDriven, tagModern, tagHorror, tagAction, tagMystic, tagSlowPace }, Variation = new(Readers: 0, PcLimit: 4, ActiveChars: 2, Comments: 6, Posts: 25) },
            // Draft of an experienced master, public
            new() { Title = "Проект: Звездные Врата", System = "Savage Worlds", Setting = "Sci-Fi", Status = Draft, DraftVisibility = Public, Tags = new[] { tagSavageWorlds, tagSciFi, tagKosmoopera, tagAction, tagPlotDriven, tagShortPost }, Variation = new(Readers: 0, PcLimit: 6, ActiveChars: 0, Comments: 0, Posts: 0) },
            new() { Title = "Тайны Древних", System = "Call of Cthulhu", Setting = "1920s", Status = Draft, DraftVisibility = Public, Tags = new[] { tagD100, tagPlotDriven, tagLiterary, tagHorror, tagMystic, tagDetective, tagHistorical }, Variation = new(Readers: 0, PcLimit: null, ActiveChars: 0, Comments: 0, Posts: 0) },
            new() { Title = "Затерянный Континент", System = "D&D 5e", Setting = "Lost World", Status = Draft, DraftVisibility = Public, Tags = new[] { tagDnD5e, tagDnD, tagFantasy, tagSandbox, tagAction, tagDungeonCrawl }, Variation = new(Readers: 0, PcLimit: 5, ActiveChars: 0, Comments: 0, Posts: 0) },
            // Draft of a newbie master, private, awaiting premoderation
            new() { Title = "Моя первая игра", System = "Словеска", Setting = "Фэнтези", Status = Draft, Premod = Awaiting, Tags = new[] { tagSloveski, tagFantasy, tagForNewbies, tagNewbieMaster, tagPlotDriven, tagShortPost, tagNoSwearing }, Variation = new(Readers: 0, PcLimit: 4, ActiveChars: 0, Comments: 0, Posts: 0) },
            new() { Title = "Приключения начинаются", System = "D&D 5e", Setting = "Homebrew", Status = Draft, Premod = Awaiting, Tags = new[] { tagDnD5e, tagDnD, tagFantasy, tagForNewbies, tagNewbieMaster, tagDungeonCrawl, tagShortPost }, Variation = new(Readers: 0, PcLimit: 6, ActiveChars: 0, Comments: 0, Posts: 0) },
            new() { Title = "Легенда о Драконе", System = "D&D 3.5", Setting = "Homebrew", Status = Closed, ClosedReason = Finished, Tags = new[] { tagDnD, tagFantasy, tagPlotDriven, tagDungeonCrawl, tagAction, tagLiterary, tagSlowPace }, Variation = new(Readers: 30, PcLimit: 6, ActiveChars: 3, Comments: 85, Posts: 420) },
            new() { Title = "Падение Империи", System = "GURPS", Setting = "Roman Empire", Status = Closed, ClosedReason = Finished, Tags = new[] { tagGURPS, tagPlotDriven, tagLiterary, tagHistorical, tagAltHistory, tagStrategy, tagSlowPace, tagGrammarNazi }, Variation = new(Readers: 25, PcLimit: 5, ActiveChars: 3, Comments: 70, Posts: 350) },
            new() { Title = "Огни Неона", System = "Cyberpunk 2020", Setting = "Night City", Status = Closed, ClosedReason = Finished, Tags = new[] { tagInterlock, tagCyberpunk, tagSciFi, tagAction, tagPlotDriven, tagThriller, tagLiterary, tagSlowPace }, Variation = new(Readers: 20, PcLimit: 5, ActiveChars: 3, Comments: 55, Posts: 280) },
            new() { Title = "Эпоха Легенд", System = "D&D 5e", Setting = "Forgotten Realms", Status = Closed, ClosedReason = Finished, Tags = new[] { tagDnD5e, tagDnD, tagFantasy, tagLiterary, tagPlotDriven, tagDungeonCrawl, tagAction, tagSlowPace }, Variation = new(Readers: 16, PcLimit: 4, ActiveChars: 3, Comments: 45, Posts: 220) },
            new() { Title = "Звездный Крейсер", System = "Stars Without Number", Setting = "Space Opera", Status = Closed, ClosedReason = Finished, Tags = new[] { tagStarfinder1e, tagSciFi, tagKosmoopera, tagPlotDriven, tagAction, tagStrategy, tagLiterary, tagSlowPace }, Variation = new(Readers: 13, PcLimit: 5, ActiveChars: 2, Comments: 40, Posts: 190) },
            new() { Title = "Темная Башня", System = "Fate Core", Setting = "Post-Apocalypse Fantasy", Status = Closed, ClosedReason = Finished, Tags = new[] { tagFate, tagFantasy, tagPostApoc, tagPlotDriven, tagMystic, tagHorror, tagLiterary, tagSlowPace }, Variation = new(Readers: 10, PcLimit: 4, ActiveChars: 2, Comments: 35, Posts: 160) },
            new() { Title = "Песнь Льда и Пламени", System = "A Song of Ice and Fire RPG", Setting = "Westeros", Status = Closed, ClosedReason = Finished, Tags = new[] { tagAuthor, tagFantasy, tagLiterary, tagPlotDriven, tagStrategy, tagPvP, tagSlowPace, tagGrammarNazi }, Variation = new(Readers: 8, PcLimit: 5, ActiveChars: 2, Comments: 30, Posts: 140) },
            new() { Title = "Властелин Колец", System = "The One Ring", Setting = "Middle-earth", Status = Closed, ClosedReason = Finished, Tags = new[] { tagAuthor, tagFantasy, tagPlotDriven, tagLiterary, tagAction, tagMystic, tagSlowPace }, Variation = new(Readers: 6, PcLimit: 4, ActiveChars: 2, Comments: 28, Posts: 130) },
            new() { Title = "Первая Колония", System = "Stars Without Number", Setting = "Alien Planet", Status = Closed, ClosedReason = Finished, Tags = new[] { tagStarfinder2e, tagSciFi, tagKosmoopera, tagSurvival, tagPlotDriven, tagHorror, tagSlowPace }, Variation = new(Readers: 5, PcLimit: 5, ActiveChars: 2, Comments: 25, Posts: 120) },
            new() { Title = "Тень Мордора", System = "D&D 5e", Setting = "Middle-earth", Status = Closed, ClosedReason = Finished, Tags = new[] { tagDnD5e, tagDnD, tagFantasy, tagAction, tagPlotDriven, tagDungeonCrawl, tagLiterary }, Variation = new(Readers: 4, PcLimit: 4, ActiveChars: 2, Comments: 22, Posts: 110) },
            new() { Title = "Кровь и Вино", System = "Словеска", Setting = "The Witcher", Status = Closed, ClosedReason = Finished, Tags = new[] { tagSloveski, tagFantasy, tagLiterary, tagAction, tagMystic, tagDetective, tagPlotDriven, tagSlowPace }, Variation = new(Readers: 3, PcLimit: 5, ActiveChars: 2, Comments: 20, Posts: 100) },
            new() { Title = "Рассвет Империи", System = "GURPS", Setting = "Alternate History", Status = Closed, ClosedReason = Finished, Tags = new[] { tagGURPS, tagPlotDriven, tagAltHistory, tagHistorical, tagStrategy, tagLiterary, tagSlowPace }, Variation = new(Readers: 2, PcLimit: 4, ActiveChars: 2, Comments: 18, Posts: 90) },
            new() { Title = "Последняя Надежда", System = "Savage Worlds", Setting = "Zombie Apocalypse", Status = Closed, ClosedReason = Finished, Tags = new[] { tagSavageWorlds, tagPostApoc, tagZombie, tagSurvival, tagHorror, tagAction, tagShortPost, tagFastPace }, Variation = new(Readers: 2, PcLimit: 5, ActiveChars: 2, Comments: 15, Posts: 80) },
            new() { Title = "Хроники Нарнии", System = "Fate Core", Setting = "Narnia", Status = Closed, ClosedReason = Finished, Tags = new[] { tagFate, tagFantasy, tagForNewbies, tagPlotDriven, tagMystic, tagNoSwearing, tagNoViolence }, Variation = new(Readers: 1, PcLimit: 4, ActiveChars: 2, Comments: 12, Posts: 70) },
            new() { Title = "Город Ангелов", System = "World of Darkness", Setting = "Los Angeles", Status = Closed, ClosedReason = Finished, Tags = new[] { tagWoD, tagLiterary, tagPlotDriven, tagModern, tagHorror, tagMystic, tagThriller, tagSlowPace }, Variation = new(Readers: 0, PcLimit: 5, ActiveChars: 2, Comments: 10, Posts: 60) },
            new() { Title = "Заброшенный Мир", System = "GURPS", Setting = "Post-Apocalypse", Status = Closed, ClosedReason = Frozen, Tags = new[] { tagGURPS, tagPostApoc, tagSurvival, tagPlotDriven, tagHorror, tagAction, tagDrySeasons }, Variation = new(Readers: 14, PcLimit: 6, ActiveChars: 0, Comments: 12, Posts: 65) },
            new() { Title = "Эхо Войны", System = "Savage Worlds", Setting = "WW2", Status = Closed, ClosedReason = Frozen, Tags = new[] { tagSavageWorlds, tagPlotDriven, tagHistorical, tagAction, tagTactics, tagShortPost, tagDrySeasons }, Variation = new(Readers: 8, PcLimit: 4, ActiveChars: 0, Comments: 8, Posts: 40) },
            new() { Title = "Забытые Руины", System = "D&D 5e", Setting = "Homebrew", Status = Closed, ClosedReason = Frozen, Tags = new[] { tagDnD5e, tagDnD, tagFantasy, tagDungeonCrawl, tagAction, tagPlotDriven, tagDrySeasons }, Variation = new(Readers: 4, PcLimit: 5, ActiveChars: 0, Comments: 5, Posts: 25) },
            new() { Title = "Тихий Омут", System = "Call of Cthulhu", Setting = "Lovecraft Country", Status = Closed, ClosedReason = Frozen, Tags = new[] { tagD100, tagPlotDriven, tagHorror, tagMystic, tagDetective, tagLiterary, tagDrySeasons }, Variation = new(Readers: 2, PcLimit: 4, ActiveChars: 0, Comments: 4, Posts: 20) },
            new() { Title = "Остров Сокровищ", System = "7th Sea", Setting = "Caribbean", Status = Closed, ClosedReason = Frozen, Tags = new[] { tagAuthor, tagFantasy, tagHistorical, tagAction, tagSandbox, tagComedy, tagDrySeasons }, Variation = new(Readers: 0, PcLimit: 5, ActiveChars: 0, Comments: 3, Posts: 15) },
            // Closed, no reason
            new() { Title = "Потерянные Хроники", System = "D&D 5e", Setting = "Homebrew", Status = Closed, Tags = new[] { tagDnD5e, tagDnD, tagFantasy, tagPlotDriven, tagDungeonCrawl, tagAction, tagSlowPace }, Variation = new(Readers: 7, PcLimit: null, ActiveChars: 0, Comments: 8, Posts: 40) },
            new() { Title = "Закат Эпохи", System = "Pathfinder 1e", Setting = "Golarion", Status = Closed, Tags = new[] { tagPathfinder1e, tagFantasy, tagPlotDriven, tagAction, tagTactics, tagLiterary, tagSlowPace }, Variation = new(Readers: 3, PcLimit: 4, ActiveChars: 0, Comments: 5, Posts: 25) },
            new() { Title = "Конец Света", System = "GURPS", Setting = "Apocalypse", Status = Closed, Tags = new[] { tagGURPS, tagPostApoc, tagSurvival, tagHorror, tagAction, tagPlotDriven, tagSlowPace }, Variation = new(Readers: 0, PcLimit: 5, ActiveChars: 0, Comments: 3, Posts: 15) },
            // For the remaining tags
            new() { Title = "Сотворение Миров", System = "Dawn of Worlds", Setting = "Homebrew Universe", Status = Closed, ClosedReason = Finished, Tags = new[] { tagDawnOfWorlds, tagFantasy, tagStrategy, tagSandbox, tagLiterary, tagSlowPace }, Variation = new(Readers: 12, PcLimit: null, ActiveChars: 0, Comments: 10, Posts: 30) },
            new() { Title = "Апокалипсис: День Ноль", System = "Apocalypse World", Setting = "Post-Apocalypse", Status = Closed, ClosedReason = Finished, Tags = new[] { tagPbtA, tagPostApoc, tagSurvival, tagPlotDriven, tagAction, tagShortPost, tagFastPace }, Variation = new(Readers: 6, PcLimit: 4, ActiveChars: 0, Comments: 6, Posts: 20) },
            new() { Title = "Клуб Анонимных Убийц", System = "Мафия", Setting = "Modern Noir", Status = Closed, ClosedReason = Finished, Tags = new[] { tagMafia, tagModern, tagDetective, tagThriller, tagPvP, tagShortPost, tagFastPace }, Variation = new(Readers: 18, PcLimit: null, ActiveChars: 0, Comments: 15, Posts: 0) },
            new() { Title = "Безумные Приключения", System = "Risus", Setting = "Comedy Fantasy", Status = Closed, ClosedReason = Finished, Tags = new[] { tagRisus, tagFantasy, tagComedy, tagTrash, tagAction, tagShortPost, tagFastPace }, Variation = new(Readers: 8, PcLimit: 5, ActiveChars: 0, Comments: 8, Posts: 35) },
            new() { Title = "Эра Водолея: Пробуждение", System = "Эра Водолея", Setting = "Post-Soviet Fantasy", Status = Closed, ClosedReason = Finished, Tags = new[] { tagEraVodolea, tagFantasy, tagMystic, tagModern, tagPlotDriven, tagLiterary, tagSlowPace }, Variation = new(Readers: 4, PcLimit: 4, ActiveChars: 0, Comments: 5, Posts: 25) },
            new() { Title = "FUDGE: Универсум", System = "FUDGE", Setting = "Multigenre", Status = Closed, ClosedReason = Finished, Tags = new[] { tagFudge, tagFantasy, tagSciFi, tagSandbox, tagPlotDriven, tagShortPost, tagFastPace }, Variation = new(Readers: 2, PcLimit: 5, ActiveChars: 0, Comments: 4, Posts: 20) },
            new() { Title = "Паровая Империя", System = "Savage Worlds", Setting = "Victorian Steampunk", Status = Closed, ClosedReason = Finished, Tags = new[] { tagSavageWorlds, tagSteampunk, tagAltHistory, tagDetective, tagAction, tagPlotDriven, tagLiterary }, Variation = new(Readers: 15, PcLimit: 5, ActiveChars: 0, Comments: 12, Posts: 60) },
            new() { Title = "За Гранью Реальности", System = "FUDGE", Setting = "Dreamscape", Status = Closed, ClosedReason = Finished, Tags = new[] { tagFudge, tagPsychedelic, tagMystic, tagHorror, tagPlotDriven, tagLiterary, tagSlowPace }, Variation = new(Readers: 3, PcLimit: 4, ActiveChars: 0, Comments: 5, Posts: 25) },
            new() { Title = "Black Bird: Сказки Старого Города", System = "Black Bird Pie", Setting = "Urban Fantasy", Status = Closed, ClosedReason = Finished, Tags = new[] { tagBlackBirdPie, tagFantasy, tagMystic, tagModern, tagForNewbies, tagShortPost, tagFastPace }, Variation = new(Readers: 5, PcLimit: 4, ActiveChars: 0, Comments: 6, Posts: 30) },
            new() { Title = "Темные Страсти", System = "World of Darkness", Setting = "Gothic Romance", Status = Closed, ClosedReason = Finished, Tags = new[] { tagWoD, tagHorror, tagMystic, tagPlotDriven, tagLiterary, tagSlowPace, tagErotica, tagPrivateGroup }, Variation = new(Readers: 1, PcLimit: 4, ActiveChars: 0, Comments: 3, Posts: 15) },
            new() { Title = "Кровавый Карнавал", System = "Словеска", Setting = "Extreme Horror", Status = Closed, ClosedReason = Finished, Tags = new[] { tagSloveski, tagHorror, tagTrash, tagPlotDriven, tagLiterary, tagSlowPace, tagShockContent, tagPrivateGroup }, Variation = new(Readers: 0, PcLimit: 3, ActiveChars: 0, Comments: 2, Posts: 12) },
        ];
    }
}
