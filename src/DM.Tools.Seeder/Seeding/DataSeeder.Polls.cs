using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Community.Features.Polls;
using DM.Domain.Core.Identity;
using DM.Infrastructure.Persistence.Entities.Game.Characters;
using DM.Infrastructure.Persistence.Entities.Game.Posts;
using Microsoft.Extensions.Options;
using DbPollVote = DM.Infrastructure.Persistence.Entities.Community.PollVote;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Tools.Seeder.Seeding;

internal sealed partial class DataSeeder
{
    /// <summary>
    /// Everyone except the account the stand is looked at through.
    /// </summary>
    private static IEnumerable<DbUser> OtherThanPrimary(IEnumerable<DbUser> users) =>
        users.Where(u => u.Username != "SolohinLex");

    /// <summary>
    /// Casts the seeded votes of one poll, handing the ballot round the voters
    /// in turn unless <paramref name="choice"/> says otherwise.
    /// </summary>
    /// <remarks>
    /// Written through the context rather than through <c>IPollRepository.Vote</c>,
    /// which stamps the moment from the clock: that one column was the whole of
    /// what kept two runs of the seeder from matching, and the repository has no
    /// parameter to hand a moment to. Here the vote is placed against the poll it
    /// belongs to - an hour apart per voter, counted from that poll's own start -
    /// so it is an offset from the resolved epoch like every other seeded date,
    /// and it lands inside the voting window of every poll below.
    ///
    /// The rule the repository enforces on a live site, one voter one option,
    /// holds here by construction: every voter list below is a slice of distinct
    /// accounts, and the primary key of PollVotes refuses a repeat regardless.
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

    private async Task CreatePolls(List<DbUser> users, DateTimeOffset now, ComprehensiveSeedResult result)
    {
        // Polls live in the same database as their voters now, and every vote
        // carries a foreign key to the user it belongs to: a reset takes polls
        // and votes together, so a ghost vote is unrepresentable and the
        // self-healing block this method used to open with is gone.
        var existingPollsCount = await _pollRepository.Count(new PollsQuery());
        if (existingPollsCount > 0)
        {
            result.Skipped++;
            result.Details.Add($"Polls already exist ({existingPollsCount}), skipping");
            return;
        }

        // ═══════════════════════════════════════════════════════════════════
        // PENDING POLLS (2) - start in future
        // ═══════════════════════════════════════════════════════════════════

        // Pending poll 1 - New Year event setting
        var pendingPoll1 = new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = now.AddDays(3).UtcDateTime,
            EndsUtc = now.AddDays(17).UtcDateTime,
            Title = "Сеттинг для новогоднего ваншота",
            Options = new[]
            {
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Зимняя сказка" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Хоррор" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Киберпанк" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Советский ретро" },
            }
        };
        await _pollRepository.Create(pendingPoll1);
        result.PollsCreated++;

        // Pending poll 2 - Anniversary celebration format
        var pendingPoll2 = new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = now.AddDays(7).UtcDateTime,
            EndsUtc = now.AddDays(21).UtcDateTime,
            Title = "Как отметим юбилей сайта?",
            Options = new[]
            {
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Конкурс постов" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Ретроспектива игр" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Марафон ваншотов" },
            }
        };
        await _pollRepository.Create(pendingPoll2);
        result.PollsCreated++;

        // ═══════════════════════════════════════════════════════════════════
        // ACTIVE POLLS (2)
        // ═══════════════════════════════════════════════════════════════════

        // Closed poll - RPG systems preference
        var closedPoll6 = new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = now.AddDays(-30).UtcDateTime,
            EndsUtc = now.AddDays(-1).UtcDateTime,
            Title = "Ваша любимая система правил?",
            Options = new[]
            {
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "D&D 5e" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Pathfinder" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Savage Worlds" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "GURPS" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Словеска" },
            }
        };
        await _pollRepository.Create(closedPoll6);
        result.PollsCreated++;

        // Add votes to closed poll 6
        await Vote(closedPoll6, users.Take(Math.Min(6, users.Count)).ToList());

        // Active poll 2 - Site improvements priority
        var activePoll2 = new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = now.AddDays(-1).UtcDateTime,
            EndsUtc = now.AddDays(21).UtcDateTime,
            Title = "Что улучшить на сайте?",
            Options = new[]
            {
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Мобильную версию" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Редактор постов" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Поиск игр" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Уведомления" },
            }
        };
        await _pollRepository.Create(activePoll2);
        result.PollsCreated++;

        // Add votes to active poll 2. Never the primary account: an open poll
        // it has already voted in shows the retraction and not the vote, and
        // that account is the one every developer and the browser tier log in
        // as - so the control the block exists for was on no screen at all.
        // By name rather than by position: the list is ordered by role and then
        // by username, and Skip(n) stopped excluding it the moment another
        // account sorted above it.
        await Vote(activePoll2, OtherThanPrimary(users).Take(4).ToList());

        // Active poll 3 - Weekly one-shot time. PUBLIC (not anonymous):
        // scheduling polls naturally show who votes for which slot, and the
        // right sidebar needs one public active poll so the per-option
        // voters tooltip is demonstrable.
        var activePoll3 = new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = now.AddDays(-5).UtcDateTime,
            EndsUtc = now.AddDays(9).UtcDateTime,
            Title = "Время для еженедельных ваншотов",
            Details = "По субботам, время по МСК",
            IsAnonymous = false,
            Options = new[]
            {
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "14:00" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "16:00" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "18:00" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "20:00" },
            }
        };
        await _pollRepository.Create(activePoll3);
        result.PollsCreated++;

        await Vote(activePoll3, OtherThanPrimary(users).Take(5).ToList());

        // ═══════════════════════════════════════════════════════════════════
        // ENDED POLLS (5)
        // ═══════════════════════════════════════════════════════════════════

        // Ended poll 1 - Favorite genre
        var endedPoll1 = new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = now.AddDays(-30).UtcDateTime,
            EndsUtc = now.AddDays(7).UtcDateTime,
            Title = "Ваш любимый жанр?",
            Options = new[]
            {
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Фэнтези" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Sci-Fi" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Хоррор" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Современность" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Исторический" },
            }
        };
        await _pollRepository.Create(endedPoll1);

        await Vote(endedPoll1, users.Skip(1).Take(Math.Min(8, users.Count - 1)).ToList());
        await _pollRepository.Update(endedPoll1.Id, null, null, null, now.AddDays(-1), null);
        result.PollsCreated++;

        // Ended poll 2 - Post frequency
        var endedPoll2 = new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = now.AddDays(-45).UtcDateTime,
            EndsUtc = now.AddDays(7).UtcDateTime,
            Title = "Как часто вы пишете посты?",
            Options = new[]
            {
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Каждый день" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Несколько раз в неделю" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Раз в неделю" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Реже" },
            }
        };
        await _pollRepository.Create(endedPoll2);

        await Vote(endedPoll2, users.Take(Math.Min(6, users.Count)).ToList());
        await _pollRepository.Update(endedPoll2.Id, null, null, null, now.AddDays(-7), null);
        result.PollsCreated++;

        // Ended poll 3 - Preferred game length
        var endedPoll3 = new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = now.AddDays(-90).UtcDateTime,
            EndsUtc = now.AddDays(7).UtcDateTime,
            Title = "Предпочтительная длина игры?",
            Options = new[]
            {
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Ваншот (1-2 недели)" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Короткая (1-3 месяца)" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Средняя (до года)" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Долгая (год+)" },
            }
        };
        await _pollRepository.Create(endedPoll3);

        await Vote(endedPoll3, users.Skip(3).Take(Math.Min(3, users.Count - 3)).ToList());
        await _pollRepository.Update(endedPoll3.Id, null, null, null, now.AddDays(-30), null);
        result.PollsCreated++;

        // Ended poll 4 - Dark theme request
        var endedPoll4 = new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = now.AddDays(-60).UtcDateTime,
            EndsUtc = now.AddDays(7).UtcDateTime,
            Title = "Нужна ли темная тема?",
            Options = new[]
            {
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Да" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Было бы неплохо" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Все равно" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Нет" },
            }
        };
        await _pollRepository.Create(endedPoll4);

        // Most users vote for first option (dark theme wanted)
        await Vote(endedPoll4, users.Take(Math.Min(7, users.Count)).ToList(), i => i < 5 ? 0 : i);
        await _pollRepository.Update(endedPoll4.Id, null, null, null, now.AddDays(-14), null);
        result.PollsCreated++;

        // Ended poll 5 - Dice rolling (no details, simple question)
        var endedPoll5 = new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = now.AddDays(-120).UtcDateTime,
            EndsUtc = now.AddDays(7).UtcDateTime,
            Title = "Нужны ли кубики на сайте?",
            Options = new[]
            {
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Да, встроенные" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Да, через бота" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Нет, хватает внешних" },
            }
        };
        await _pollRepository.Create(endedPoll5);

        await Vote(endedPoll5, users.Skip(2).Take(Math.Min(4, users.Count - 2)).ToList());
        await _pollRepository.Update(endedPoll5.Id, null, null, null, now.AddDays(-60), null);
        result.PollsCreated++;

        // Ended poll 6 - Character creation preference
        var endedPoll6 = new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = now.AddDays(-100).UtcDateTime,
            EndsUtc = now.AddDays(-70).UtcDateTime,
            Title = "Как вы создаете персонажей?",
            Options = new[]
            {
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Начинаю с концепта" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Начинаю с механики" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "По ситуации" },
            }
        };
        await _pollRepository.Create(endedPoll6);
        result.PollsCreated++;

        // Ended poll 7 - PvP attitude
        var endedPoll7 = new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = now.AddDays(-110).UtcDateTime,
            EndsUtc = now.AddDays(-80).UtcDateTime,
            Title = "Ваше отношение к PvP?",
            Options = new[]
            {
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Люблю PvP" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Иногда интересно" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Предпочитаю кооператив" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Не люблю PvP" },
            }
        };
        await _pollRepository.Create(endedPoll7);
        result.PollsCreated++;

        // Ended poll 8 - Session frequency
        var endedPoll8 = new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = now.AddDays(-150).UtcDateTime,
            EndsUtc = now.AddDays(-120).UtcDateTime,
            Title = "Сколько игр вы ведете одновременно?",
            Options = new[]
            {
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Одну" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "2-3" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "4-5" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Больше 5" },
            }
        };
        await _pollRepository.Create(endedPoll8);
        result.PollsCreated++;

        // Ended poll 9 - Communication preference
        var endedPoll9 = new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = now.AddDays(-180).UtcDateTime,
            EndsUtc = now.AddDays(-150).UtcDateTime,
            Title = "Где обсуждаете игру с мастером?",
            Options = new[]
            {
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "В личных сообщениях на сайте" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "В Discord/Telegram" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "В комментариях игры" },
            }
        };
        await _pollRepository.Create(endedPoll9);
        result.PollsCreated++;

        // Ended poll 10 - Combat preference
        var endedPoll10 = new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = now.AddDays(-200).UtcDateTime,
            EndsUtc = now.AddDays(-170).UtcDateTime,
            Title = "Что важнее в бою?",
            Options = new[]
            {
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Тактика и механика" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Нарратив и описания" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Баланс того и другого" },
            }
        };
        await _pollRepository.Create(endedPoll10);
        result.PollsCreated++;

        // Ended poll 11 - Worldbuilding preference
        var endedPoll11 = new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = now.AddDays(-220).UtcDateTime,
            EndsUtc = now.AddDays(-190).UtcDateTime,
            Title = "Какой сеттинг предпочитаете?",
            Options = new[]
            {
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Готовые миры (Forgotten Realms, etc.)" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Авторские сеттинги" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Совместное создание мира" },
            }
        };
        await _pollRepository.Create(endedPoll11);
        result.PollsCreated++;

        // Ended poll 12 - Long title test
        var endedPoll12 = new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = now.AddDays(-240).UtcDateTime,
            EndsUtc = now.AddDays(-210).UtcDateTime,
            Title = "Как вы относитесь к длинным описаниям персонажей, которые включают подробную предысторию, характер, мотивацию и внешность?",
            Options = new[]
            {
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Чем подробнее, тем лучше" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Предпочитаю краткость" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Зависит от игры" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Раскрываю персонажа по ходу игры" },
            }
        };
        await _pollRepository.Create(endedPoll12);
        result.PollsCreated++;

        // Ended poll 13 - Long details test
        var endedPoll13 = new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = now.AddDays(-260).UtcDateTime,
            EndsUtc = now.AddDays(-230).UtcDateTime,
            Title = "Формат постов в играх",
            Details = "Мы хотим понять, какой формат постов предпочитает наше сообщество. " +
                      "Это поможет нам лучше настроить редактор и подсказки для новых игроков. " +
                      "Под \"коротким постом\" мы понимаем 2-5 предложений, под \"средним\" — 1-3 абзаца, " +
                      "под \"длинным\" — развернутые описания на несколько экранов. " +
                      "Учитывайте свой обычный стиль игры, а не идеальные пожелания.",
            Options = new[]
            {
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Короткие посты (2-5 предложений)" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Средние посты (1-3 абзаца)" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Длинные посты (развернутые описания)" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Адаптируюсь под партию" },
            }
        };
        await _pollRepository.Create(endedPoll13);
        result.PollsCreated++;

        // ═══════════════════════════════════════════════════════════════════
        // PUBLIC POLLS (voters visible)
        // ═══════════════════════════════════════════════════════════════════

        // Public poll - Ended, with votes
        var publicPoll2 = new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = now.AddDays(-20).UtcDateTime,
            EndsUtc = now.AddDays(-10).UtcDateTime,
            Title = "Оцените прошедшее мероприятие",
            Details = "Открытый опрос для участников ивента.",
            IsAnonymous = false,
            Options = new[]
            {
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Отлично!" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Хорошо" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Могло быть лучше" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Не участвовал" },
            }
        };
        await _pollRepository.Create(publicPoll2);

        // Add votes to public poll 2
        await Vote(publicPoll2, users.Skip(3).Take(Math.Min(18, users.Count - 3)).ToList());
        result.PollsCreated++;

        result.Details.Add($"Created {result.PollsCreated} polls (2 pending, 2 active, 15 closed; 2 public)");
    }
}
