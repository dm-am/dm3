using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Community.Features.Polls;
using DbPollVote = DM.Infrastructure.Persistence.Entities.Community.PollVote;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Tools.Seeder.Seeding;

internal sealed partial class DataSeeder
{
    private static IEnumerable<DbUser> OtherThanPrimary(IEnumerable<DbUser> users) =>
        users.Where(u => u.Username != PrimaryLogin);

    /// <remarks>
    /// Through the context: <c>IPollRepository.Vote</c> stamps the moment from the clock. One voter, one option
    /// holds by construction, every voter list being distinct accounts.
    /// </remarks>
    private async Task Vote(
        CreatePollEntity poll,
        IReadOnlyList<DbUser> voters,
        Func<int, int>? choice = null)
    {
        var options = poll.Options.Select(o => o.Id).ToList();
        for (var i = 0; i < voters.Count; i++)
        {
            _dbContext.PollVotes.Add(new DbPollVote
            {
                PollId = poll.Id,
                UserId = voters[i].UserId,
                PollOptionId = options[(choice?.Invoke(i) ?? i) % options.Count],
                VotedUtc = new DateTimeOffset(poll.StartsUtc, TimeSpan.Zero).AddHours(i + 1),
            });
        }

        await _dbContext.SaveChangesAsync();
    }

    private CreatePollEntity NewPoll(
        DateTimeOffset startsUtc, DateTimeOffset endsUtc, string title, params string[] options) => new()
        {
            Id = _guidFactory.Create(),
            StartsUtc = startsUtc.UtcDateTime,
            EndsUtc = endsUtc.UtcDateTime,
            Title = title,
            Options = options
            .Select(text => new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = text })
            .ToArray(),
        };

    private async Task CreatePoll(CreatePollEntity poll, ComprehensiveSeedResult result)
    {
        await _pollRepository.Create(poll);
        result.PollsCreated++;
    }

    private async Task CreatePolls(List<DbUser> users, ComprehensiveSeedResult result)
    {
        var existingPollsCount = await _pollRepository.Count(new PollsQuery());
        if (existingPollsCount > 0)
        {
            result.Details.Add($"Polls already exist ({existingPollsCount}), skipping");
            return;
        }

        await SeedPendingPolls(result);
        await SeedActivePolls(users, result);
        await SeedEndedPolls(users, result);
        await SeedPastPolls(users, result);
        await SeedLongRunningPoll(users, result);

        result.Details.Add($"Created {result.PollsCreated} polls");
    }

    /// <remarks>
    /// The server reads the status of a poll by its own clock, so on a stand seeded on a pinned past epoch
    /// (scripts/visual/rebuild.mjs) the polls of a few weeks are over, and the retraction of a vote has no
    /// poll to be shown on (the senior-poll-cancel-vote state of the frames stand in scripts/visual/states.ts). Fixed
    /// identifiers, for the reason of <see cref="BanListCaseIds"/>.
    /// </remarks>
    private async Task SeedLongRunningPoll(List<DbUser> users, ComprehensiveSeedResult result)
    {
        var seniorModerator = users.FirstOrDefault(u => u.Username == "TestSeniorMod");
        if (seniorModerator == null)
        {
            result.Details.Add("Long-running poll skipped: no TestSeniorMod");
            return;
        }

        var poll = new CreatePollEntity
        {
            Id = FixedGuid(0xe0, 1),
            StartsUtc = _now.AddDays(-10).UtcDateTime,
            EndsUtc = _now.AddYears(10).UtcDateTime,
            Title = "Откуда вы узнали о сайте?",
            Options = new[] { "От друзей", "Из поиска", "Из соцсетей", "Не помню" }
                .Select((text, i) => new CreatePollOptionEntity { Id = FixedGuid(0xe1, i + 1), Text = text })
                .ToArray(),
        };
        await CreatePoll(poll, result);

        await Vote(poll, [seniorModerator]);
    }

    private async Task SeedPendingPolls(ComprehensiveSeedResult result)
    {
        // Pending: New Year event setting
        var newYearPoll = NewPoll(_now.AddDays(3), _now.AddDays(17), "Сеттинг для новогоднего ваншота",
            "Зимняя сказка", "Хоррор", "Киберпанк", "Советский ретро");
        await CreatePoll(newYearPoll, result);

        // Pending: anniversary celebration format
        var anniversaryPoll = NewPoll(_now.AddDays(7), _now.AddDays(21), "Как отметим юбилей сайта?",
            "Конкурс постов", "Ретроспектива игр", "Марафон ваншотов");
        await CreatePoll(anniversaryPoll, result);
    }

    private async Task SeedActivePolls(List<DbUser> users, ComprehensiveSeedResult result)
    {
        // Closed: RPG systems preference
        var systemsPoll = NewPoll(_now.AddDays(-30), _now.AddDays(-1), "Ваша любимая система правил?",
            "D&D 5e", "Pathfinder", "Savage Worlds", "GURPS", "Словеска");
        await CreatePoll(systemsPoll, result);

        await Vote(systemsPoll, users.Take(Math.Min(6, users.Count)).ToList());

        // Active: site improvements priority
        var improvementsPoll = NewPoll(_now.AddDays(-1), _now.AddDays(21), "Что улучшить на сайте?",
            "Мобильную версию", "Редактор постов", "Поиск игр", "Уведомления");
        await CreatePoll(improvementsPoll, result);

        // Never the primary account, by name: having voted, it would show the retraction and not the vote.
        await Vote(improvementsPoll, OtherThanPrimary(users).Take(4).ToList());

        // Active: weekly one-shot time. Public, so the right sidebar shows the per-option voters tooltip.
        var oneShotTimePoll = NewPoll(_now.AddDays(-5), _now.AddDays(9), "Время для еженедельных ваншотов",
            "14:00", "16:00", "18:00", "20:00");
        oneShotTimePoll.Details = "По субботам, время по МСК";
        oneShotTimePoll.IsAnonymous = false;
        await CreatePoll(oneShotTimePoll, result);

        await Vote(oneShotTimePoll, OtherThanPrimary(users).Take(5).ToList());
    }

    private async Task SeedEndedPolls(List<DbUser> users, ComprehensiveSeedResult result)
    {
        // Ended: favorite genre
        var genrePoll = NewPoll(_now.AddDays(-30), _now.AddDays(7), "Ваш любимый жанр?",
            "Фэнтези", "Sci-Fi", "Хоррор", "Современность", "Исторический");
        await CreatePoll(genrePoll, result);

        await Vote(genrePoll, users.Skip(1).Take(Math.Min(8, users.Count - 1)).ToList());
        await _pollRepository.Update(genrePoll.Id, null, null, null, _now.AddDays(-1), null);

        // Ended: post frequency
        var postFrequencyPoll = NewPoll(_now.AddDays(-45), _now.AddDays(7), "Как часто вы пишете посты?",
            "Каждый день", "Несколько раз в неделю", "Раз в неделю", "Реже");
        await CreatePoll(postFrequencyPoll, result);

        await Vote(postFrequencyPoll, users.Take(Math.Min(6, users.Count)).ToList());
        await _pollRepository.Update(postFrequencyPoll.Id, null, null, null, _now.AddDays(-7), null);

        // Ended: preferred game length
        var gameLengthPoll = NewPoll(_now.AddDays(-90), _now.AddDays(7), "Предпочтительная длина игры?",
            "Ваншот (1-2 недели)", "Короткая (1-3 месяца)", "Средняя (до года)", "Долгая (год+)");
        await CreatePoll(gameLengthPoll, result);

        await Vote(gameLengthPoll, users.Skip(3).Take(Math.Min(3, users.Count - 3)).ToList());
        await _pollRepository.Update(gameLengthPoll.Id, null, null, null, _now.AddDays(-30), null);

        // Ended: dark theme request
        var darkThemePoll = NewPoll(_now.AddDays(-60), _now.AddDays(7), "Нужна ли темная тема?",
            "Да", "Было бы неплохо", "Все равно", "Нет");
        await CreatePoll(darkThemePoll, result);

        // Most users want the dark theme
        await Vote(darkThemePoll, users.Take(Math.Min(7, users.Count)).ToList(), i => i < 5 ? 0 : i);
        await _pollRepository.Update(darkThemePoll.Id, null, null, null, _now.AddDays(-14), null);

        // Ended: dice rolling, without details
        var dicePoll = NewPoll(_now.AddDays(-120), _now.AddDays(7), "Нужны ли кубики на сайте?",
            "Да, встроенные", "Да, через бота", "Нет, хватает внешних");
        await CreatePoll(dicePoll, result);

        await Vote(dicePoll, users.Skip(2).Take(Math.Min(4, users.Count - 2)).ToList());
        await _pollRepository.Update(dicePoll.Id, null, null, null, _now.AddDays(-60), null);
    }

    private async Task SeedPastPolls(List<DbUser> users, ComprehensiveSeedResult result)
    {
        // Ended: character creation preference
        var characterCreationPoll = NewPoll(_now.AddDays(-100), _now.AddDays(-70), "Как вы создаете персонажей?",
            "Начинаю с концепта", "Начинаю с механики", "По ситуации");
        await CreatePoll(characterCreationPoll, result);

        // Ended: PvP attitude
        var pvpPoll = NewPoll(_now.AddDays(-110), _now.AddDays(-80), "Ваше отношение к PvP?",
            "Люблю PvP", "Иногда интересно", "Предпочитаю кооператив", "Не люблю PvP");
        await CreatePoll(pvpPoll, result);

        // Ended: games run at once
        var gamesAtOncePoll = NewPoll(_now.AddDays(-150), _now.AddDays(-120), "Сколько игр вы ведете одновременно?",
            "Одну", "2-3", "4-5", "Больше 5");
        await CreatePoll(gamesAtOncePoll, result);

        // Ended: communication preference
        var communicationPoll = NewPoll(_now.AddDays(-180), _now.AddDays(-150), "Где обсуждаете игру с мастером?",
            "В личных сообщениях на сайте", "В Discord/Telegram", "В комментариях игры");
        await CreatePoll(communicationPoll, result);

        // Ended: combat preference
        var combatPoll = NewPoll(_now.AddDays(-200), _now.AddDays(-170), "Что важнее в бою?",
            "Тактика и механика", "Нарратив и описания", "Баланс того и другого");
        await CreatePoll(combatPoll, result);

        // Ended: worldbuilding preference
        var settingPoll = NewPoll(_now.AddDays(-220), _now.AddDays(-190), "Какой сеттинг предпочитаете?",
            "Готовые миры (Forgotten Realms, etc.)", "Авторские сеттинги", "Совместное создание мира");
        await CreatePoll(settingPoll, result);

        // Ended: long title
        var longTitlePoll = NewPoll(_now.AddDays(-240), _now.AddDays(-210),
            "Как вы относитесь к длинным описаниям персонажей, которые включают подробную предысторию, характер, мотивацию и внешность?",
            "Чем подробнее, тем лучше", "Предпочитаю краткость", "Зависит от игры", "Раскрываю персонажа по ходу игры");
        await CreatePoll(longTitlePoll, result);

        // Ended: long details
        var longDetailsPoll = NewPoll(_now.AddDays(-260), _now.AddDays(-230), "Формат постов в играх",
            "Короткие посты (2-5 предложений)", "Средние посты (1-3 абзаца)",
            "Длинные посты (развернутые описания)", "Адаптируюсь под партию");
        longDetailsPoll.Details = "Мы хотим понять, какой формат постов предпочитает наше сообщество. " +
                              "Это поможет нам лучше настроить редактор и подсказки для новых игроков. " +
                              "Под \"коротким постом\" мы понимаем 2-5 предложений, под \"средним\" — 1-3 абзаца, " +
                              "под \"длинным\" — развернутые описания на несколько экранов. " +
                              "Учитывайте свой обычный стиль игры, а не идеальные пожелания.";
        await CreatePoll(longDetailsPoll, result);

        // Public, ended, with votes
        var eventFeedbackPoll = NewPoll(_now.AddDays(-20), _now.AddDays(-10), "Оцените прошедшее мероприятие",
            "Отлично!", "Хорошо", "Могло быть лучше", "Не участвовал");
        eventFeedbackPoll.Details = "Открытый опрос для участников ивента.";
        eventFeedbackPoll.IsAnonymous = false;
        await CreatePoll(eventFeedbackPoll, result);

        await Vote(eventFeedbackPoll, users.Skip(3).Take(Math.Min(18, users.Count - 3)).ToList());
    }
}
