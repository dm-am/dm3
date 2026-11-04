using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Community;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbGameReview = DM.Infrastructure.Persistence.Entities.Game.GameReview;
using Microsoft.EntityFrameworkCore;

namespace DM.Tools.Seeder.Seeding;

internal sealed partial class DataSeeder
{
    /// <summary>
    /// The reviews of the seed: testimonials of the site, game reviews and
    /// endorsements, then the post reviews of the finished games, the showcase
    /// posts of the home page among them.
    /// </summary>
    private async Task CreateReviews(List<DbUser> users, List<Guid> gameIds, ComprehensiveSeedResult result)
    {
        var existingTestimonialsCount = await _dbContext.WebsiteTestimonials.CountAsync();
        if (existingTestimonialsCount > 0)
        {
            result.Details.Add($"Testimonials already exist ({existingTestimonialsCount}), skipping reviews seeding");
        }

        var experiencedUsers = users.Where(u => !ProbationPolicy.IsNewbie(u.QuantityRating)).ToList();
        if (experiencedUsers.Count == 0) experiencedUsers = users.Take(3).ToList();

        if (existingTestimonialsCount == 0)
        {
            SeedWebsiteTestimonials(users, result);
        }

        await SeedGameReviews(gameIds, experiencedUsers, result);
        await SeedUserEndorsements(experiencedUsers, result);

        var existingPostReviewsCount = await _dbContext.PostReviews.CountAsync();
        if (existingPostReviewsCount > 0)
        {
            return;
        }

        var finished = await ReadFinishedGamePosts();
        if (finished == null)
        {
            result.Details.Add("No finished games found for post reviews");
            return;
        }

        var processedPostIds = await SeedRatedShowcasePosts(finished, users, experiencedUsers, result);
        SeedRandomPostReviews(finished, processedPostIds, experiencedUsers, result);
        await SeedChuckPost(users, experiencedUsers, result);

        result.Details.Add($"Created {result.ReviewsCreated} reviews");
    }

    /// <summary>
    /// Testimonials of the site: texts and how many days ago, enough for more than one page.
    /// </summary>
    private void SeedWebsiteTestimonials(List<DbUser> users, ComprehensiveSeedResult result)
    {
        var testimonials = new (string Text, int DaysAgo)[]
        {
            // Very short
            ("Ня!", 2),

            // Long
            ("Пришел сюда по рекомендации друга около трех лет назад, и с тех пор это стало моим основным хобби. Особенно нравится, что здесь можно найти игры на любой вкус — от классического фэнтези до киберпанка и постапокалипсиса. Мастера в большинстве своем отзывчивые и готовы помочь новичкам разобраться в правилах. Отдельный плюс — возможность играть в своем темпе, не подстраиваясь под чужое расписание. Конечно, бывают и неудачные игры, но это скорее исключение. В целом — отличное место для тех, кто любит писать и создавать истории вместе с другими людьми.", 350),
            ("Начинал здесь как обычный игрок много лет назад. Тогда площадка была совсем другой — простенький форум, несколько десятков активных пользователей, пара сотен игр. Сейчас это полноценное сообщество с тысячами участников, продуманной системой тегов, удобным редактором постов и множеством других функций. Приятно осознавать, что внес свой вклад в развитие проекта. Здесь я нашел не только увлечение, но и настоящих друзей, с некоторыми из которых общаюсь уже вне площадки. Если вы любите писать, придумывать персонажей и погружаться в интересные сюжеты — вам точно сюда.", 300),
            ("Играю здесь с 2019 года. За это время площадка сильно изменилась в лучшую сторону — новый дизайн, удобные уведомления, быстрый поиск игр. Модерация работает оперативно, конфликты решаются справедливо. Единственное, чего не хватает — мобильного приложения, но и веб-версия на телефоне работает нормально. Рекомендую всем, кто хочет попробовать форумные ролевые игры.", 260),
            ("Долго искал площадку для словесок, и DM.am оказался именно тем, что нужно. Простой интерфейс, понятные правила, живое сообщество. Особенно радует, что можно найти игры практически в любом жанре — от классического фэнтези до современных детективов. Мастера отзывчивые, всегда готовы объяснить непонятные моменты. Единственный минус — иногда сложно выбрать, в какую игру вступить, потому что интересных слишком много!", 220),
            ("Пришла сюда по рекомендации подруги и не пожалела. Первые дни было немного страшно — все-таки новое место, незнакомые люди. Но оказалось, что здесь очень приветливо относятся к новичкам. Помогли разобраться с правилами, подсказали хорошие игры для старта. Сейчас у меня уже три активных персонажа в разных играх, и я планирую создать свою собственную игру в ближайшее время.", 180),
            ("Не ожидал, что форумные игры могут быть настолько затягивающими! Начал полгода назад с небольшого персонажа в фэнтези-игре, а теперь уже сам вожу две игры и участвую еще в трех. Формат постовой игры отлично подходит для работающих людей — можно писать когда удобно, не нужно подстраиваться под чужое расписание. Сообщество отзывчивое, всегда можно найти партнеров для игры или просто пообщаться на форуме.", 145),
            ("Площадка, на которой хочется оставаться. Здесь собрались действительно увлеченные люди, которые создают потрясающие истории. Каждая игра — это маленький мир со своими правилами и атмосферой. Очень нравится система тегов, которая помогает быстро найти игру по интересам. Отдельное спасибо разработчикам за удобный редактор постов с поддержкой форматирования.", 110),
            ("Площадка, которая объединяет людей с похожими интересами. Здесь я нашла не просто игры, а настоящее хобби, которое помогает отвлечься от рутины и погрузиться в увлекательные истории. Нравится, что каждый может найти что-то свое — есть игры с акцентом на боевку, есть чисто отыгрышевые, есть смешанные. Модерация адекватная, правила понятные.", 85),
            ("Пишу здесь уже пять лет. За это время сменилось несколько версий сайта, но атмосфера осталась прежней — дружелюбной и творческой. Особенно ценю возможность участвовать в нескольких играх одновременно и писать в удобное время. Форумный формат идеально подходит для тех, кто любит продумывать каждую реплику своего персонажа.", 65),

            // Medium
            ("Удобный интерфейс, дружелюбное комьюнити. Нашел здесь много интересных игр и познакомился с классными людьми.", 50),
            ("Зашел случайно, остался навсегда. Атмосфера здесь особенная — люди действительно любят то, чем занимаются.", 42),
            ("Классное комьюнити, интересные игры, удобный сайт. Что еще нужно?", 35),
            ("Очень нравится возможность участвовать в нескольких играх одновременно. Форматы игр на любой вкус!", 28),
            ("Отличный сайт для тех, кто ценит качественный отыгрыш и интересные сюжеты.", 22),
            ("Люди отвечают, игры доводят до конца. Мне этого хватает.", 18),

            // Very long, several paragraphs
            ("Когда я впервые зашел сюда восемь лет назад, думал — ну, поиграю пару месяцев и забуду. Какие там восемь лет! Это место затягивает так, что не замечаешь, как пролетают годы. И дело не только в играх — хотя они здесь потрясающие — а в людях, которых встречаешь на этом пути.\n\nЗдесь я научился писать. Не в смысле грамматики — с этим и раньше было нормально. Научился чувствовать ритм текста, создавать атмосферу, передавать эмоции через слова. Каждый пост — это маленький вызов самому себе: сделать лучше, интереснее, глубже. И когда соигроки отвечают тем же — когда вместе создаете что-то по-настоящему стоящее — это ощущение ни с чем не сравнить.\n\nЕще я нашел здесь настоящих друзей. Людей, с которыми можно обсуждать не только игры, но и жизнь. Некоторых уже встречал вживую, и это было странно и прекрасно одновременно — узнавать голос человека, которого знаешь только по текстам. Кто бы мог подумать, что форум для ролевых игр станет местом, где найдешь единомышленников?\n\nСпасибо этому месту за все. За бессонные ночи над постами, за жаркие споры о лоре, за смех в чате в три часа ночи. За то, что оно просто есть)", 400),

            // Short
            ("Лучшее место для текстовых ролевых игр!", 120),
            ("Отличная площадка для новичков. Всегда рады помочь!", 95),
            ("Отличное место для творческих людей. Рекомендую!", 75),
            ("Лучшая площадка для форумных ролевых игр в рунете. Без преувеличения.", 55),
            ("Годы идут, а DM.am остается любимым местом для творчества.", 40),
            ("Хороший сайт для тех, кто любит писать.", 30),
            ("10/10, рекомендую всем любителям ролевых игр!", 15),
            ("Супер! Всем советую!", 8),
        };

        var testimonialCount = Math.Min(users.Count, testimonials.Length);
        for (var i = 0; i < testimonialCount; i++)
        {
            var (text, daysAgo) = testimonials[i];
            _dbContext.WebsiteTestimonials.Add(new WebsiteTestimonial
            {
                WebsiteTestimonialId = _guidFactory.Create(),
                AuthorId = users[i].UserId,
                CreatedUtc = _now.AddDays(-daysAgo),
                Text = text,
            });
            result.TestimonialsCreated++;
        }
    }

    /// <summary>
    /// Game reviews (only for experienced users): two closed games, each by
    /// somebody other than its master. A review is written on a closed game alone.
    /// </summary>
    private async Task SeedGameReviews(
        List<Guid> gameIds, List<DbUser> experiencedUsers, ComprehensiveSeedResult result)
    {
        var existingGameReviewsCount = await _dbContext.GameReviews.CountAsync();
        if (gameIds.Count > 0 && experiencedUsers.Count >= 2 && existingGameReviewsCount == 0)
        {
            // Ordered by the serial number: a LIMIT over an unordered result leaves the choice to the query plan.
            var games = await _dbContext.Set<DbGame>()
                .Where(g => gameIds.Contains(g.GameId) && g.Status == ModuleStatus.Closed)
                .OrderBy(g => g.SerialNumber)
                .ToListAsync();

            foreach (var game in games.Take(2))
            {
                var reviewer = experiencedUsers.First(u => u.UserId != game.MasterId);
                _dbContext.GameReviews.Add(new DbGameReview
                {
                    GameReviewId = _guidFactory.Create(),
                    AuthorId = reviewer.UserId,
                    GameId = game.GameId,
                    CreatedUtc = _now.AddDays(-_random.Next(1, 30)),
                    Text = "Хорошая игра. Мастер отвечает быстро, сюжет держит.",
                });
                result.ReviewsCreated++;
            }
        }
    }

    /// <summary>
    /// User endorsements between experienced users.
    /// </summary>
    private async Task SeedUserEndorsements(
        List<DbUser> experiencedUsers, ComprehensiveSeedResult result)
    {
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
                CreatedUtc = _now.AddDays(-_random.Next(1, 60)),
                Text = "Отличный игрок! Всегда вовремя пишет посты, интересные персонажи.",
            });
            result.ReviewsCreated++;
        }
    }
}
