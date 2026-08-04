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
    /// <summary>
    /// Everyone except the account the stand is looked at through.
    /// </summary>
    private static IEnumerable<DbUser> OtherThanPrimary(IEnumerable<DbUser> users) =>
        users.Where(u => u.Username != "SolohinLex");

    private async Task CreatePolls(List<DbUser> users, DateTimeOffset now, ComprehensiveSeedResult result)
    {
        // Polls live in MongoDB and survive a relational re-seed, while their
        // votes reference Postgres user ids. After the relational database is
        // recreated the surviving votes point at users that no longer exist
        // ("ghost votes"): counts stay inflated and public-poll voter lists
        // resolve to nobody, so the voters tooltip silently disappears.
        // Detect that staleness and recreate the polls instead of skipping.
        var existingPollsCount = await _pollRepository.Count(new PollsQuery());
        if (existingPollsCount > 0)
        {
            var pollsQuery = new PollsQuery { Take = (int)existingPollsCount };
            var existingPolls = (await _pollRepository.Get(
                pollsQuery,
                new PagingData(pollsQuery, (int)existingPollsCount, (int)existingPollsCount))).ToList();
            var voterIds = existingPolls
                .SelectMany(p => p.Options.SelectMany(o => o.UserIds))
                .Distinct()
                .ToList();
            var knownVoterCount = await _dbContext.Set<DbUser>()
                .Where(u => voterIds.Contains(u.UserId))
                .CountAsync();

            if (knownVoterCount == voterIds.Count)
            {
                result.Skipped++;
                result.Details.Add($"Polls already exist ({existingPollsCount}), skipping");
                return;
            }

            foreach (var stalePoll in existingPolls)
            {
                await _pollRepository.Delete(stalePoll.Id);
            }
            result.Details.Add(
                $"Stale polls recreated: {voterIds.Count - knownVoterCount} ghost voter(s) from a dropped relational database");
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
        // ACTIVE POLLS (3)
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
        var votersForClosed6 = users.Take(Math.Min(6, users.Count)).ToList();
        var optionIds6 = closedPoll6.Options.Select(o => o.Id).ToList();
        for (var i = 0; i < votersForClosed6.Count; i++)
        {
            await _pollRepository.Vote(closedPoll6.Id, optionIds6[i % optionIds6.Count], votersForClosed6[i].UserId);
        }

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
        var votersForActive2 = OtherThanPrimary(users).Take(4).ToList();
        var optionIds2 = activePoll2.Options.Select(o => o.Id).ToList();
        for (var i = 0; i < votersForActive2.Count; i++)
        {
            await _pollRepository.Vote(activePoll2.Id, optionIds2[i % optionIds2.Count], votersForActive2[i].UserId);
        }

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

        var votersForActive3 = OtherThanPrimary(users).Take(5).ToList();
        var optionIds3 = activePoll3.Options.Select(o => o.Id).ToList();
        for (var i = 0; i < votersForActive3.Count; i++)
        {
            await _pollRepository.Vote(activePoll3.Id, optionIds3[i % optionIds3.Count], votersForActive3[i].UserId);
        }

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

        var votersForEnded1 = users.Skip(1).Take(Math.Min(8, users.Count - 1)).ToList();
        var endedOptionIds1 = endedPoll1.Options.Select(o => o.Id).ToList();
        for (var i = 0; i < votersForEnded1.Count; i++)
        {
            await _pollRepository.Vote(endedPoll1.Id, endedOptionIds1[i % endedOptionIds1.Count], votersForEnded1[i].UserId);
        }
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

        var votersForEnded2 = users.Take(Math.Min(6, users.Count)).ToList();
        var endedOptionIds2 = endedPoll2.Options.Select(o => o.Id).ToList();
        for (var i = 0; i < votersForEnded2.Count; i++)
        {
            await _pollRepository.Vote(endedPoll2.Id, endedOptionIds2[i % endedOptionIds2.Count], votersForEnded2[i].UserId);
        }
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

        var votersForEnded3 = users.Skip(3).Take(Math.Min(3, users.Count - 3)).ToList();
        var endedOptionIds3 = endedPoll3.Options.Select(o => o.Id).ToList();
        for (var i = 0; i < votersForEnded3.Count; i++)
        {
            await _pollRepository.Vote(endedPoll3.Id, endedOptionIds3[i % endedOptionIds3.Count], votersForEnded3[i].UserId);
        }
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
        var votersForEnded4 = users.Take(Math.Min(7, users.Count)).ToList();
        var endedOptionIds4 = endedPoll4.Options.Select(o => o.Id).ToList();
        for (var i = 0; i < votersForEnded4.Count; i++)
        {
            var optionIdx = i < 5 ? 0 : i % endedOptionIds4.Count;
            await _pollRepository.Vote(endedPoll4.Id, endedOptionIds4[optionIdx], votersForEnded4[i].UserId);
        }
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

        var votersForEnded5 = users.Skip(2).Take(Math.Min(4, users.Count - 2)).ToList();
        var endedOptionIds5 = endedPoll5.Options.Select(o => o.Id).ToList();
        for (var i = 0; i < votersForEnded5.Count; i++)
        {
            await _pollRepository.Vote(endedPoll5.Id, endedOptionIds5[i % endedOptionIds5.Count], votersForEnded5[i].UserId);
        }
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
        var votersForPublic2 = users.Skip(3).Take(Math.Min(18, users.Count - 3)).ToList();
        var publicOptionIds2 = publicPoll2.Options.Select(o => o.Id).ToList();
        for (var i = 0; i < votersForPublic2.Count; i++)
        {
            await _pollRepository.Vote(publicPoll2.Id, publicOptionIds2[i % publicOptionIds2.Count], votersForPublic2[i].UserId);
        }
        result.PollsCreated++;

        result.Details.Add($"Created {result.PollsCreated} polls (2 pending, 3 active, 15 closed; 2 public)");
    }
}
