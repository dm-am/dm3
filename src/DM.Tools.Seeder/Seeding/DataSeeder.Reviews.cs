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
    private async Task CreateReviews(List<DbUser> users, List<Guid> gameIds, DateTimeOffset now, ComprehensiveSeedResult result)
    {
        // Check if testimonials already exist
        var existingTestimonialsCount = await _dbContext.WebsiteTestimonials.CountAsync();
        if (existingTestimonialsCount > 0)
        {
            result.Skipped++;
            result.Details.Add($"Testimonials already exist ({existingTestimonialsCount}), skipping reviews seeding");
        }

        var experiencedUsers = users.Where(u => u.QuantityRating >= 100).ToList();
        if (experiencedUsers.Count == 0) experiencedUsers = users.Take(3).ToList();

        // Testimonials - website reviews (Text, DaysAgo) tuples for diverse lengths, styles, and dates
        // Enough for pagination testing (>10 per page)
        if (existingTestimonialsCount == 0)
        {
            var testimonials = new (string Text, int DaysAgo)[]
            {
                // Very short testimonial (like "Ня!" from production)
                ("Ня!", 2),

                // Long testimonials (detailed feedback)
                ("Пришел сюда по рекомендации друга около трех лет назад, и с тех пор это стало моим основным хобби. Особенно нравится, что здесь можно найти игры на любой вкус — от классического фэнтези до киберпанка и постапокалипсиса. Мастера в большинстве своем отзывчивые и готовы помочь новичкам разобраться в правилах. Отдельный плюс — возможность играть в своем темпе, не подстраиваясь под чужое расписание. Конечно, бывают и неудачные игры, но это скорее исключение. В целом — отличное место для тех, кто любит писать и создавать истории вместе с другими людьми.", 350),
                ("Начинал здесь как обычный игрок много лет назад. Тогда площадка была совсем другой — простенький форум, несколько десятков активных пользователей, пара сотен игр. Сейчас это полноценное сообщество с тысячами участников, продуманной системой тегов, удобным редактором постов и множеством других функций. Приятно осознавать, что внес свой вклад в развитие проекта. Здесь я нашел не только увлечение, но и настоящих друзей, с некоторыми из которых общаюсь уже вне площадки. Если вы любите писать, придумывать персонажей и погружаться в интересные сюжеты — вам точно сюда.", 300),
                ("Играю здесь с 2019 года. За это время площадка сильно изменилась в лучшую сторону — новый дизайн, удобные уведомления, быстрый поиск игр. Модерация работает оперативно, конфликты решаются справедливо. Единственное, чего не хватает — мобильного приложения, но и веб-версия на телефоне работает нормально. Рекомендую всем, кто хочет попробовать форумные ролевые игры.", 260),
                ("Долго искал площадку для словесок, и DM.am оказался именно тем, что нужно. Простой интерфейс, понятные правила, живое сообщество. Особенно радует, что можно найти игры практически в любом жанре — от классического фэнтези до современных детективов. Мастера отзывчивые, всегда готовы объяснить непонятные моменты. Единственный минус — иногда сложно выбрать, в какую игру вступить, потому что интересных слишком много!", 220),
                ("Пришла сюда по рекомендации подруги и не пожалела. Первые дни было немного страшно — все-таки новое место, незнакомые люди. Но оказалось, что здесь очень приветливо относятся к новичкам. Помогли разобраться с правилами, подсказали хорошие игры для старта. Сейчас у меня уже три активных персонажа в разных играх, и я планирую создать свою собственную игру в ближайшее время.", 180),
                ("Не ожидал, что форумные игры могут быть настолько затягивающими! Начал полгода назад с небольшого персонажа в фэнтези-игре, а теперь уже сам вожу две игры и участвую еще в трех. Формат постовой игры отлично подходит для работающих людей — можно писать когда удобно, не нужно подстраиваться под чужое расписание. Сообщество отзывчивое, всегда можно найти партнеров для игры или просто пообщаться на форуме.", 145),
                ("Площадка, на которой хочется оставаться. Здесь собрались действительно увлеченные люди, которые создают потрясающие истории. Каждая игра — это маленький мир со своими правилами и атмосферой. Очень нравится система тегов, которая помогает быстро найти игру по интересам. Отдельное спасибо разработчикам за удобный редактор постов с поддержкой форматирования.", 110),
                ("Площадка, которая объединяет людей с похожими интересами. Здесь я нашла не просто игры, а настоящее хобби, которое помогает отвлечься от рутины и погрузиться в увлекательные истории. Нравится, что каждый может найти что-то свое — есть игры с акцентом на боевку, есть чисто отыгрышевые, есть смешанные. Модерация адекватная, правила понятные.", 85),
                ("Пишу здесь уже пять лет. За это время сменилось несколько версий сайта, но атмосфера осталась прежней — дружелюбной и творческой. Особенно ценю возможность участвовать в нескольких играх одновременно и писать в удобное время. Форумный формат идеально подходит для тех, кто любит продумывать каждую реплику своего персонажа.", 65),

                // Medium testimonials (typical user feedback)
                ("Удобный интерфейс, дружелюбное комьюнити. Нашел здесь много интересных игр и познакомился с классными людьми.", 50),
                ("Зашел случайно, остался навсегда. Атмосфера здесь особенная — люди действительно любят то, чем занимаются.", 42),
                ("Классное комьюнити, интересные игры, удобный сайт. Что еще нужно?", 35),
                ("Очень нравится возможность участвовать в нескольких играх одновременно. Форматы игр на любой вкус!", 28),
                ("Отличный сайт для тех, кто ценит качественный отыгрыш и интересные сюжеты.", 22),
                ("Дружелюбная атмосфера, интересные люди, захватывающие истории. Что может быть лучше?", 18),

                // Very long testimonial (~1100 chars, multi-paragraph)
                ("Когда я впервые зашел сюда восемь лет назад, думал — ну, поиграю пару месяцев и забуду. Какие там восемь лет! Это место затягивает так, что не замечаешь, как пролетают годы. И дело не только в играх — хотя они здесь потрясающие — а в людях, которых встречаешь на этом пути.\n\nЗдесь я научился писать. Не в смысле грамматики — с этим и раньше было нормально. Научился чувствовать ритм текста, создавать атмосферу, передавать эмоции через слова. Каждый пост — это маленький вызов самому себе: сделать лучше, интереснее, глубже. И когда соигроки отвечают тем же — когда вместе создаете что-то по-настоящему стоящее — это ощущение ни с чем не сравнить.\n\nЕще я нашел здесь настоящих друзей. Людей, с которыми можно обсуждать не только игры, но и жизнь. Некоторых уже встречал вживую, и это было странно и прекрасно одновременно — узнавать голос человека, которого знаешь только по текстам. Кто бы мог подумать, что форум для ролевых игр станет местом, где найдешь единомышленников?\n\nСпасибо этому месту за все. За бессонные ночи над постами, за жаркие споры о лоре, за смех в чате в три часа ночи. За то, что оно просто есть)", 400),

                // Short testimonials (quick impressions)
                ("Лучшее место для текстовых ролевых игр!", 120),
                ("Отличная площадка для новичков. Всегда рады помочь!", 95),
                ("Отличное место для творческих людей. Рекомендую!", 75),
                ("Лучшая площадка для форумных ролевых игр в рунете. Без преувеличения.", 55),
                ("Годы идут, а DM.am остается любимым местом для творчества.", 40),
                ("Хороший сайт для тех, кто любит писать.", 30),
                ("10/10, рекомендую всем любителям ролевых игр!", 15),
                ("Супер! Всем советую!", 8),
            };

            // Each user gets exactly one testimonial (up to available texts)
            var testimonialCount = Math.Min(users.Count, testimonials.Length);
            for (var i = 0; i < testimonialCount; i++)
            {
                var (text, daysAgo) = testimonials[i];
                _dbContext.WebsiteTestimonials.Add(new WebsiteTestimonial
                {
                    WebsiteTestimonialId = _guidFactory.Create(),
                    AuthorId = users[i].UserId,
                    CreatedUtc = now.AddDays(-daysAgo),
                    Text = text,
                    IsRemoved = false
                });
                result.TestimonialsCreated++;
            }
        }

        // Game reviews (only for experienced users)
        var existingGameReviewsCount = await _dbContext.GameReviews.CountAsync();
        if (gameIds.Count > 0 && experiencedUsers.Count >= 2 && existingGameReviewsCount == 0)
        {
            // Two of them get a review, so which two is a decision, and a LIMIT
            // over an unordered result hands that decision to the query plan.
            // Ordered by the serial number: the order the games were created in,
            // and a number, so no collation gets a say either.
            var games = await _dbContext.Set<DbGame>()
                .Where(g => gameIds.Contains(g.GameId) && g.Status != ModuleStatus.Draft)
                .OrderBy(g => g.SerialNumber)
                .ToListAsync();

            foreach (var game in games.Take(2))
            {
                var reviewer = experiencedUsers.First(u => u.UserId != game.MasterId);
                _dbContext.GameReviews.Add(new DM.Infrastructure.Persistence.Entities.Game.GameReview
                {
                    GameReviewId = _guidFactory.Create(),
                    AuthorId = reviewer.UserId,
                    GameId = game.GameId,
                    CreatedUtc = now.AddDays(-_random.Next(1, 30)),
                    Text = "Отличная игра! Мастер ведет интересно, сюжет захватывающий.",
                    IsRemoved = false
                });
                result.ReviewsCreated++;
            }
        }

        // User reviews (only between users who played together)
        // User endorsements between experienced users
        var existingEndorsementsCount = await _dbContext.UserEndorsements.CountAsync();
        if (experiencedUsers.Count >= 2 && existingEndorsementsCount == 0)
        {
            var endorser = experiencedUsers[0];
            var target = experiencedUsers[1];
            _dbContext.UserEndorsements.Add(new UserEndorsement
            {
                UserEndorsementId = _guidFactory.Create(),
                AuthorId = endorser.UserId,
                TargetUserId = target.UserId,
                CreatedUtc = now.AddDays(-_random.Next(1, 60)),
                Text = "Отличный игрок! Всегда вовремя пишет посты, интересные персонажи.",
                IsRemoved = false
            });
            result.ReviewsCreated++;
        }

        // Post reviews - get posts from change tracker OR database (for re-seeds)
        // Skip if post reviews already exist
        var existingPostReviewsCount = await _dbContext.PostReviews.CountAsync();
        if (existingPostReviewsCount > 0)
        {
            return;
        }

        // Find finished game IDs - first try ChangeTracker, then fall back to database
        var finishedGameIds = _dbContext.ChangeTracker.Entries<DbGame>()
            .Where(e => e.Entity.Status == ModuleStatus.Closed && e.Entity.ClosedReason == ClosedReason.Finished)
            .Select(e => e.Entity.GameId)
            .ToList();

        if (finishedGameIds.Count == 0)
        {
            // Fall back to database query for re-seeds
            finishedGameIds = await _dbContext.Set<DbGame>()
                .Where(g => g.Status == ModuleStatus.Closed && g.ClosedReason == ClosedReason.Finished)
                .Select(g => g.GameId)
                .ToListAsync();
        }

        if (finishedGameIds.Count == 0)
        {
            result.Details.Add("No finished games found for post reviews");
            return;
        }

        // Get rooms for finished games - first try ChangeTracker, then fall back to database
        var roomsFromTracker = _dbContext.ChangeTracker.Entries<Room>()
            .Where(e => finishedGameIds.Contains(e.Entity.GameId) && e.Entity.AccessType == RoomAccessType.Open)
            .Select(e => e.Entity)
            .ToList();

        List<Room> rooms;
        if (roomsFromTracker.Count > 0)
        {
            rooms = roomsFromTracker;
        }
        else
        {
            // Fall back to database query
            rooms = await _dbContext.Set<Room>()
                .Where(r => finishedGameIds.Contains(r.GameId) && r.AccessType == RoomAccessType.Open)
                .ToListAsync();
        }

        var roomIds = rooms.Select(r => r.RoomId).ToList();
        var roomToGameId = rooms.ToDictionary(r => r.RoomId, r => r.GameId);

        // Get posts - first try ChangeTracker, then fall back to database
        var postsFromTracker = _dbContext.ChangeTracker.Entries<Post>()
            .Where(e => !e.Entity.IsRemoved && roomIds.Contains(e.Entity.RoomId))
            .Select(e => e.Entity)
            .ToList();

        List<Post> posts;
        if (postsFromTracker.Count > 0)
        {
            posts = postsFromTracker;
        }
        else
        {
            // Fall back to database query
            posts = await _dbContext.Set<Post>()
                .Where(p => !p.IsRemoved && roomIds.Contains(p.RoomId))
                .ToListAsync();
        }

        // Find the Diopside post (long post with ~2000 chars about crystalline creature)
        // This will be "Latest rated" - the most recently reviewed post
        var diopsidePost = posts.FirstOrDefault(p => p.GameText.Contains("Диопсид"));

        // Find a post from "Эльвира Чародейка" for "Best of week" (highest rating this week)
        var elviraCharId = _dbContext.ChangeTracker.Entries<Character>()
            .FirstOrDefault(e => e.Entity.Name == "Эльвира Чародейка" && e.Entity.Status == CharacterStatus.Active)
            ?.Entity.CharacterId;
        var bestOfWeekPost = elviraCharId.HasValue
            ? posts.FirstOrDefault(p => p.CharacterId == elviraCharId.Value)
            : null;
        // Replace the post text with a thematic "burning the mage" RP scene
        if (bestOfWeekPost != null)
            bestOfWeekPost.GameText = "Сжигаю мага";

        // Create "Latest rated" - Diopside post with multiple reviews (most recent 1 hour ago)
        if (diopsidePost != null)
        {
            var gameId = roomToGameId.GetValueOrDefault(diopsidePost.RoomId);
            if (gameId != Guid.Empty)
            {
                var diopsideReviewers = experiencedUsers.Where(u => u.UserId != diopsidePost.AuthorId).Take(4).ToList();

                // Varied reviews for Diopside: 2 positive, 1 neutral, 1 negative (net +1)
                // Some reviews use BBCode for testing rendering
                var diopsideReviewData = new[]
                {
                    (ReviewSign.Positive, """
[quote]"Туман рассеялся, открывая древние руины"[/quote]
Отличное начало! Чувствуется проработка мира и внимание к деталям окружения. Персонаж сразу вызывает интерес.

[spoiler]Особенно понравилось: описание руин и первая встреча с драконом. [b]Атмосфера загадочности[/b] передана очень удачно![/spoiler]
""", -2), // 2 hours ago (SolohinLex master post review at now takes "Latest rated")
                    (ReviewSign.Positive, "Люблю такие детальные описания! [b]Атмосфера на высоте.[/b]", -3), // 3 hours ago
                    (ReviewSign.Neutral, "Нормальный пост. Стиль интересный, но не для всех.", -5), // 5 hours ago
                    (ReviewSign.Negative, """
Слишком длинно на мой вкус.

[spoiler]Развернутая критика: понимаю, что автор старался создать атмосферу, но местами текст затянут. Можно было бы сократить описания без потери смысла.

Впрочем, это субъективно — кому-то такой стиль нравится.[/spoiler]
""", -7), // 7 hours ago
                };

                var qualityDelta = 0;
                for (var i = 0; i < diopsideReviewers.Count && i < diopsideReviewData.Length; i++)
                {
                    var reviewer = diopsideReviewers[i];
                    var (sign, text, hoursAgo) = diopsideReviewData[i];
                    _dbContext.PostReviews.Add(new DM.Infrastructure.Persistence.Entities.Game.PostReview
                    {
                        PostReviewId = _guidFactory.Create(),
                        AuthorId = reviewer.UserId,
                        PostId = diopsidePost.PostId,
                        PostAuthorId = diopsidePost.AuthorId,
                        GameId = gameId,
                        CreatedUtc = now.AddHours(hoursAgo),
                        Text = text,
                        SignValue = (short)sign,
                        IsRemoved = false
                    });
                    result.ReviewsCreated++;
                    qualityDelta += (int)sign;
                }
                var postAuthor = users.FirstOrDefault(u => u.UserId == diopsidePost.AuthorId);
                if (postAuthor != null) postAuthor.QualityRating += qualityDelta;
            }
        }

        // Create "Best of week" - different post with 5 reviews from earlier today (within current week)
        if (bestOfWeekPost != null)
        {
            var gameId = roomToGameId.GetValueOrDefault(bestOfWeekPost.RoomId);
            if (gameId != Guid.Empty)
            {
                var reviewCount = Math.Min(5, experiencedUsers.Count(u => u.UserId != bestOfWeekPost.AuthorId));
                var reviewersForBest = experiencedUsers.Where(u => u.UserId != bestOfWeekPost.AuthorId).Take(reviewCount).ToList();

                // All positive reviews (net +5) — master's post should clearly be best of the week
                var reviewData = new[]
                {
                    (ReviewSign.Positive, "Отличный пост! Замечательный отыгрыш мастера, атмосфера на высоте."),
                    (ReviewSign.Positive, "Очень атмосферно, браво! Мастер задал отличный тон сцене."),
                    (ReviewSign.Positive, "Красивое описание, мне понравилось. Сразу чувствуется мастерство."),
                    (ReviewSign.Positive, "Прекрасная подача! Мир оживает в каждой строчке."),
                    (ReviewSign.Positive, "Лучший мастерский пост за последнее время, однозначно!"),
                };

                var qualityDelta = 0;
                for (var i = 0; i < reviewersForBest.Count; i++)
                {
                    var reviewer = reviewersForBest[i];
                    var (sign, text) = reviewData[i % reviewData.Length];
                    var reviewDate = now.AddHours(-_random.Next(2, 13));
                    _dbContext.PostReviews.Add(new DM.Infrastructure.Persistence.Entities.Game.PostReview
                    {
                        PostReviewId = _guidFactory.Create(),
                        AuthorId = reviewer.UserId,
                        PostId = bestOfWeekPost.PostId,
                        PostAuthorId = bestOfWeekPost.AuthorId,
                        GameId = gameId,
                        CreatedUtc = reviewDate,
                        Text = text,
                        SignValue = (short)sign,
                        IsRemoved = false
                    });
                    result.ReviewsCreated++;
                    qualityDelta += (int)sign;
                }
                var postAuthor = users.FirstOrDefault(u => u.UserId == bestOfWeekPost.AuthorId);
                if (postAuthor != null) postAuthor.QualityRating += qualityDelta;
            }
        }

        // Create master post from SolohinLex — "Latest rated" on homepage
        // Find a finished game where SolohinLex is master, create a master post (no character)
        var testAdmin = users.FirstOrDefault(u => u.Username == "SolohinLex");
        Post? masterPost = null;
        if (testAdmin != null)
        {
            // Find a finished game mastered by SolohinLex
            var masterGameEntry = _dbContext.ChangeTracker.Entries<DbGame>()
                .FirstOrDefault(e => finishedGameIds.Contains(e.Entity.GameId) && e.Entity.MasterId == testAdmin.UserId);
            var masterGameId = masterGameEntry?.Entity.GameId ?? Guid.Empty;
            var masterRoom = masterGameId != Guid.Empty
                ? rooms.FirstOrDefault(r => roomToGameId.GetValueOrDefault(r.RoomId) == masterGameId)
                : null;

            if (masterRoom != null)
            {
                masterPost = new Post
                {
                    PostId = _guidFactory.Create(),
                    RoomId = masterRoom.RoomId,
                    CharacterId = null, // Master post — no character
                    AuthorId = testAdmin.UserId,
                    CreatedUtc = now.AddHours(-3),
                    GameText = "Отвоеванный у отца камин, 2 спальни, телевизор",
                    IsRemoved = false
                };
                _dbContext.Set<Post>().Add(masterPost);
                result.PostsCreated++;

                // 1 positive review at now — makes it "Latest rated" (most recent review)
                var masterReviewer = experiencedUsers.FirstOrDefault(u => u.UserId != testAdmin.UserId);
                if (masterReviewer != null)
                {
                    _dbContext.PostReviews.Add(new DM.Infrastructure.Persistence.Entities.Game.PostReview
                    {
                        PostReviewId = _guidFactory.Create(),
                        AuthorId = masterReviewer.UserId,
                        PostId = masterPost.PostId,
                        PostAuthorId = testAdmin.UserId,
                        GameId = masterGameId,
                        CreatedUtc = now, // Most recent review → "Latest rated"
                        Text = "Атмосфера на высоте, сразу чувствуется стиль мастера!",
                        SignValue = (short)ReviewSign.Positive,
                        IsRemoved = false
                    });
                    result.ReviewsCreated++;
                    testAdmin.QualityRating += 1;
                }
            }
        }

        // Create a rated post from LongestLoginPossible (tests long username + long character name)
        var longestUser = users.FirstOrDefault(u => u.Username == "LongestLoginPossible");
        Post? longestUserPost = null;
        if (longestUser != null && finishedGameIds.Count > 0)
        {
            var longestGameId = finishedGameIds[0];
            var longestRoom = rooms.FirstOrDefault(r => roomToGameId.GetValueOrDefault(r.RoomId) == longestGameId);
            if (longestRoom != null)
            {
                // Create a character with a long multi-word name
                var longestChar = new Character
                {
                    CharacterId = _guidFactory.Create(),
                    GameId = longestGameId,
                    AuthorId = longestUser.UserId,
                    Status = CharacterStatus.Active,
                    CreatedUtc = now.AddDays(-20),
                    Name = "Сэр Максимилиан фон Штернберг",
                    IsNpc = false,
                    AccessPolicy = CharacterAccessPolicy.NoAccess,
                    IsRemoved = false
                };
                _dbContext.Set<Character>().Add(longestChar);
                AddLegacyCharacterAttributes(longestChar.CharacterId,
                    race: "Человек", @class: "Паладин",
                    appearance: "Высокий светловолосый мужчина в сияющих доспехах.");
                result.CharactersCreated++;

                longestUserPost = new Post
                {
                    PostId = _guidFactory.Create(),
                    RoomId = longestRoom.RoomId,
                    CharacterId = longestChar.CharacterId,
                    AuthorId = longestUser.UserId,
                    CreatedUtc = now.AddHours(-6),
                    GameText = "Максимилиан поднял забрало и оглядел зал. Руны на платформе мерцали, и в их свете его доспехи отбрасывали мягкие блики на стены. Он покачал головой — за годы странствий он научился не доверять местам, которые выглядят слишком спокойно.",
                    IsRemoved = false
                };
                _dbContext.Set<Post>().Add(longestUserPost);
                result.PostsCreated++;
                longestUser.QuantityRating++;

                // 2 reviews (net +1)
                var longestReviewers = experiencedUsers.Where(u => u.UserId != longestUser.UserId).Take(2).ToList();
                var longestReviewData = new[]
                {
                    (ReviewSign.Positive, "Хороший отыгрыш, чувствуется характер персонажа."),
                    (ReviewSign.Neutral, "Коротковато, но по делу."),
                };
                var longestQualityDelta = 0;
                for (var i = 0; i < longestReviewers.Count && i < longestReviewData.Length; i++)
                {
                    var reviewer = longestReviewers[i];
                    var (sign, text) = longestReviewData[i];
                    _dbContext.PostReviews.Add(new DM.Infrastructure.Persistence.Entities.Game.PostReview
                    {
                        PostReviewId = _guidFactory.Create(),
                        AuthorId = reviewer.UserId,
                        PostId = longestUserPost.PostId,
                        PostAuthorId = longestUser.UserId,
                        GameId = longestGameId,
                        CreatedUtc = now.AddHours(-_random.Next(1, 5)),
                        Text = text,
                        SignValue = (short)sign,
                        IsRemoved = false
                    });
                    result.ReviewsCreated++;
                    longestQualityDelta += (int)sign;
                }
                longestUser.QualityRating += longestQualityDelta;
            }
        }

        // Remaining posts - random reviews (excluding already processed)
        var processedPostIds = new HashSet<Guid>();
        if (diopsidePost != null) processedPostIds.Add(diopsidePost.PostId);
        if (bestOfWeekPost != null) processedPostIds.Add(bestOfWeekPost.PostId);
        if (masterPost != null) processedPostIds.Add(masterPost.PostId);
        if (longestUserPost != null) processedPostIds.Add(longestUserPost.PostId);

        // Review texts for variety (some with BBCode for testing)
        var positiveTexts = new[]
        {
            "Отличный пост!",
            "Хорошо написано!",
            "Мне понравилось!",
            "Красиво!",
            "Атмосферно!",
            "Браво!",
            "[b]Очень хорошо![/b] Продолжай в том же духе.",
            "Супер!",
            """
Отличная работа!

[spoiler]Подробнее: особенно понравилось описание окружения. Чувствуется, что автор хорошо понимает своего персонажа.[/spoiler]
""",
            "[quote]Прочитал с удовольствием[/quote]\nОтличный пост, жду продолжения!",
        };
        var neutralTexts = new[]
        {
            "Нормально",
            "Неплохо",
            "Сойдет",
            "Ок",
            "Средненько, но читабельно.",
        };
        var negativeTexts = new[]
        {
            "Можно лучше",
            "Не очень",
            "Слабовато",
            "[spoiler]Критика: текст сыроват, стоит поработать над стилем.[/spoiler]",
        };

        // This week posts - 30 posts with 2-4 reviews each (within last 6 days)
        // Enough for 2+ pages of pagination at 20/page
        var thisWeekPosts = posts.Where(p => !processedPostIds.Contains(p.PostId)).Take(30).ToList();
        foreach (var post in thisWeekPosts)
        {
            processedPostIds.Add(post.PostId);
            var gameId = roomToGameId.GetValueOrDefault(post.RoomId);
            if (gameId == Guid.Empty) continue;

            // 2-4 reviews per post
            var reviewCount = _random.Next(2, 5);
            var availableReviewers = experiencedUsers.Where(u => u.UserId != post.AuthorId).ToList();

            for (var r = 0; r < reviewCount && r < availableReviewers.Count; r++)
            {
                var reviewer = availableReviewers[r];
                var sign = (ReviewSign)_random.Next(-1, 2);
                // Within current week: 0-6 days ago
                var reviewDate = now.AddDays(-_random.Next(0, 6)).AddHours(-_random.Next(1, 24));
                var text = sign switch
                {
                    ReviewSign.Positive => positiveTexts[_random.Next(positiveTexts.Length)],
                    ReviewSign.Negative => negativeTexts[_random.Next(negativeTexts.Length)],
                    _ => neutralTexts[_random.Next(neutralTexts.Length)]
                };

                _dbContext.PostReviews.Add(new DM.Infrastructure.Persistence.Entities.Game.PostReview
                {
                    PostReviewId = _guidFactory.Create(),
                    AuthorId = reviewer.UserId,
                    PostId = post.PostId,
                    PostAuthorId = post.AuthorId,
                    GameId = gameId,
                    CreatedUtc = reviewDate,
                    Text = text,
                    SignValue = (short)sign,
                    IsRemoved = false
                });
                result.ReviewsCreated++;
                var postAuthor = users.FirstOrDefault(u => u.UserId == post.AuthorId);
                if (postAuthor != null) postAuthor.QualityRating += (int)sign;
            }
        }

        // Older posts - 8 posts with single reviews (2-4 weeks ago, for history)
        foreach (var post in posts.Where(p => !processedPostIds.Contains(p.PostId)).Take(8))
        {
            var gameId = roomToGameId.GetValueOrDefault(post.RoomId);
            if (gameId == Guid.Empty) continue;

            var reviewer = experiencedUsers.FirstOrDefault(u => u.UserId != post.AuthorId);
            if (reviewer != null)
            {
                var sign = (ReviewSign)_random.Next(-1, 2);
                // Older: 2-4 weeks ago
                var reviewDate = now.AddDays(-_random.Next(14, 28)).AddHours(-_random.Next(1, 24));
                var text = sign switch
                {
                    ReviewSign.Positive => positiveTexts[_random.Next(positiveTexts.Length)],
                    ReviewSign.Negative => negativeTexts[_random.Next(negativeTexts.Length)],
                    _ => neutralTexts[_random.Next(neutralTexts.Length)]
                };

                _dbContext.PostReviews.Add(new DM.Infrastructure.Persistence.Entities.Game.PostReview
                {
                    PostReviewId = _guidFactory.Create(),
                    AuthorId = reviewer.UserId,
                    PostId = post.PostId,
                    PostAuthorId = post.AuthorId,
                    GameId = gameId,
                    CreatedUtc = reviewDate,
                    Text = text,
                    SignValue = (short)sign,
                    IsRemoved = false
                });
                result.ReviewsCreated++;
                var postAuthor = users.FirstOrDefault(u => u.UserId == post.AuthorId);
                if (postAuthor != null) postAuthor.QualityRating += (int)sign;
            }
        }

        // Create Chuck character in first active recruiting game — "Best of week" on homepage
        // Chuck is a grapefruit-obsessed barbarian whose post gets 7 positive reviews (net +7)
        var activeRecruitingGame = _dbContext.ChangeTracker.Entries<DbGame>()
            .FirstOrDefault(e => e.Entity.Status == ModuleStatus.Active && e.Entity.IsRecruitmentOpen);
        if (activeRecruitingGame != null)
        {
            var chuckGameId = activeRecruitingGame.Entity.GameId;
            var chuckRoom = _dbContext.ChangeTracker.Entries<Room>()
                .FirstOrDefault(e => e.Entity.GameId == chuckGameId && e.Entity.AccessType == RoomAccessType.Open)
                ?.Entity;

            if (chuckRoom != null)
            {
                // Pick a player for Chuck (not the master)
                var chuckPlayer = users.FirstOrDefault(u =>
                    u.UserId != activeRecruitingGame.Entity.MasterId &&
                    u.Username != "SolohinLex");

                if (chuckPlayer != null)
                {
                    var chuckChar = new Character
                    {
                        CharacterId = _guidFactory.Create(),
                        GameId = chuckGameId,
                        AuthorId = chuckPlayer.UserId,
                        Status = CharacterStatus.Active,
                        CreatedUtc = now.AddDays(-10),
                        Name = "Чак",
                        IsNpc = false,
                        AccessPolicy = CharacterAccessPolicy.NoAccess,
                        IsRemoved = false
                    };
                    _dbContext.Set<Character>().Add(chuckChar);
                    AddLegacyCharacterAttributes(chuckChar.CharacterId,
                        race: "Человек", @class: "Варвар",
                        appearance: "Коренастый мужчина с обветренным лицом и мощными руками. На поясе всегда висит мешочек с грейпфрутами.",
                        temper: "Буйный, но добродушный. Впадает в ярость при виде несправедливости. И при виде апельсинов.",
                        story: "Бывший фермер из долины Золотых Рощ, где выращивают лучшие грейпфруты континента. Ушел в приключенцы после того, как орки сожгли его плантацию.",
                        skills: "Двуручное оружие, выживание, кулинария (грейпфрутовые блюда), запугивание.",
                        inventory: "Двуручный топор, 3 грейпфрута, фляга с грейпфрутовым соком, потрепанная кулинарная книга.");
                    result.CharactersCreated++;

                    // Upload Chuck avatar through the real pipeline
                    // (EXIF-strip, WebP _m/_s thumbnails).
                    var chuckBytes = ReadEmbeddedSeedBytes("DM.Tools.Seeder.Assets.Seed.Chuck.png");
                    var chuckUpload = await SeedAvatarFromBytesAsync(
                        chuckBytes,
                        declaredContentType: "image/png",
                        sourceFileName: "Chuck.png",
                        type: UploadType.CharacterAvatar,
                        uploadId: _guidFactory.Create(),
                        userId: chuckPlayer.UserId,
                        entityId: chuckChar.CharacterId,
                        now: chuckChar.CreatedUtc);
                    _dbContext.Set<DM.Infrastructure.Persistence.Entities.Shared.Upload>().Add(chuckUpload);

                    // Chuck's magnum opus about grapefruit.
                    // Clamped to the start of the current week: the homepage
                    // "лучший пост недели" block keeps posts created since
                    // Monday, and a seed run in the first hours of Monday would
                    // put `now` minus eight hours in the week that just ended,
                    // leaving the block empty from the very first page view.
                    var chuckWeekStart = WeekStartUtc(now);
                    var chuckPostCreatedUtc = now.AddHours(-8) < chuckWeekStart
                        ? chuckWeekStart
                        : now.AddHours(-8);
                    var chuckPost = new Post
                    {
                        PostId = _guidFactory.Create(),
                        RoomId = chuckRoom.RoomId,
                        CharacterId = chuckChar.CharacterId,
                        AuthorId = chuckPlayer.UserId,
                        CreatedUtc = chuckPostCreatedUtc,
                        GameText = """
                            Чак сел на поваленное бревно, достал из мешка грейпфрут и некоторое время просто держал его в руках. Тяжелый. Теплый от солнца. Идеальный.

                            Он провел большим пальцем по шершавой кожуре и закрыл глаза. Запах — горьковато-сладкий, с нотками утреннего тумана над рощей — ударил в нос, и на мгновение Чак оказался дома. Золотые Рощи. Ряды деревьев до горизонта. Мать на веранде, отец в саду. Корзины, полные розовато-желтых плодов.

                            "Знаете, в чем проблема этого мира?" — произнес он, ни к кому конкретно не обращаясь. Спутники уже привыкли к его монологам. — "Все едят яблоки. Яблоки! Пресные, скучные, предсказуемые яблоки. Ни горечи, ни вызова. Откусил — и забыл. А грейпфрут? Грейпфрут — это диалог. Он не сразу раскрывается. Сначала горчит, потом кислит, потом — вот оно — сладость. Настоящая, заслуженная сладость. Как жизнь, понимаете?"

                            Он аккуратно надрезал кожуру ножом — не топором, хотя мог бы — и начал чистить. Каждую дольку он отделял с почтением хирурга.

                            "Мой дед говорил: покажи мне, как человек ест грейпфрут, и я скажу тебе, кто он. Если морщится и бросает — трус. Если заливает сахаром — слабак. А если ест как есть, с горечью, с кислинкой, с мякотью между зубами — вот это воин."

                            Чак откусил дольку и жевал медленно, с выражением абсолютного блаженства на лице. Сок потек по бороде. Ему было все равно.

                            "В таверне в Ривенделле однажды попросил грейпфрутовый сок. Трактирщик посмотрел на меня как на сумасшедшего. 'У нас есть яблочный', говорит. Яблочный! Я чуть стол не перевернул. Нет, я перевернул. Но потом извинился и заплатил за ремонт. Я же не дикарь. Я варвар, но не дикарь. Есть разница."

                            Он доел грейпфрут, аккуратно сложил кожуру в мешок — "на сушку, для чая" — и вытер руки о штаны.

                            "Вот когда мы закончим это приключение и я получу свою долю золота — знаете, что я сделаю? Куплю участок земли. Посажу грейпфрутовые деревья. И буду жить. Просто жить. Каждое утро — свежий грейпфрут с дерева. Каждый вечер — грейпфрутовый пирог. По праздникам — грейпфрутовое вино. Рай."

                            Он помолчал, глядя на закат.

                            "Но сначала надо убить этого дракона. Потому что, по слухам, он сжег три грейпфрутовые рощи к югу отсюда. И за это он ответит."
                            """,
                        MetagameText = "Это было прекрасно. У меня слезы на глазах.",
                        IsRemoved = false
                    };
                    _dbContext.Set<Post>().Add(chuckPost);
                    result.PostsCreated++;
                    chuckPlayer.QuantityRating++;

                    // 7 positive reviews, net +7. That is the top of the week by
                    // construction: the bulk of seeded posts gets at most +4,
                    // Elvira's master post +5, and leaderboard coverage never
                    // touches a post created this week (EnsureLeaderboardCoverage).
                    var chuckReviewers = experiencedUsers
                        .Where(u => u.UserId != chuckPlayer.UserId)
                        .Take(7)
                        .ToList();
                    var chuckReviewTexts = new[]
                    {
                        "Лучший пост, что я читал за последний год. Грейпфрут — это философия.",
                        "Я буквально плакал от смеха. И от красоты. Одновременно.",
                        "Чак — национальное достояние. Этот монолог надо высечь в камне.",
                        "Подписываюсь под каждым словом. Яблоки — это скучно. Грейпфрут — это жизнь.",
                        "Персонаж раскрыт на все сто. Глубина, юмор, драма — все в одном посте.",
                        "Отыгрыш уровня бог. Сцена с трактирщиком — шедевр.",
                        "Жду продолжения грейпфрутовой саги. Это лучше 'Властелина Колец'.",
                    };
                    // Reviews land strictly between the post and `now`: dating
                    // them 2..11 hours back could put them BEFORE a post placed
                    // eight hours back, and the fireplace review at `now` has to
                    // stay the freshest one on the site ("последний оцененный").
                    var chuckReviewSpanMinutes =
                        (int)Math.Max(2, (now - chuckPostCreatedUtc).TotalMinutes);
                    for (var i = 0; i < chuckReviewers.Count; i++)
                    {
                        _dbContext.PostReviews.Add(new DM.Infrastructure.Persistence.Entities.Game.PostReview
                        {
                            PostReviewId = _guidFactory.Create(),
                            AuthorId = chuckReviewers[i].UserId,
                            PostId = chuckPost.PostId,
                            PostAuthorId = chuckPlayer.UserId,
                            GameId = chuckGameId,
                            CreatedUtc = chuckPostCreatedUtc.AddMinutes(_random.Next(1, chuckReviewSpanMinutes)),
                            Text = chuckReviewTexts[i],
                            SignValue = (short)ReviewSign.Positive,
                            IsRemoved = false
                        });
                        result.ReviewsCreated++;
                    }
                    chuckPlayer.QualityRating += 7;
                }
            }
        }

        result.Details.Add($"Created {result.ReviewsCreated} reviews");
    }
}
