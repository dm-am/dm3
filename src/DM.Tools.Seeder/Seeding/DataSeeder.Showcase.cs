using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Community.Features.Statistics;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Game.Characters;
using DM.Infrastructure.Persistence.Entities.Game.Posts;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;

namespace DM.Tools.Seeder.Seeding;

internal sealed partial class DataSeeder
{
    /// <summary>
    /// The rated posts the home page and the rated posts list are read against:
    /// Diopside's post, the best post of the week, SolohinLex's master post and
    /// the post of the longest login.
    /// </summary>
    /// <returns>The posts written or rated here, which the random reviews leave alone.</returns>
    private async Task<HashSet<Guid>> SeedRatedShowcasePosts(
        FinishedGamePosts finished, List<DbUser> users, List<DbUser> experiencedUsers,
        ComprehensiveSeedResult result)
    {
        var (finishedGameIds, rooms, roomToGameId, posts) = finished;

        // The Diopside post, the latest rated one
        var diopsidePost = posts.FirstOrDefault(p => p.GameText.Contains("Диопсид"));

        // A post of "Эльвира Чародейка", the best of the week
        var elviraCharId = _dbContext.ChangeTracker.Entries<Character>()
            .FirstOrDefault(e => e.Entity.Name == "Эльвира Чародейка" && e.Entity.Status == CharacterStatus.Active)
            ?.Entity.CharacterId;
        var bestOfWeekPost = elviraCharId.HasValue
            ? posts.FirstOrDefault(p => p.CharacterId == elviraCharId.Value)
            : null;
        // Its text becomes a "burning the mage" scene
        if (bestOfWeekPost != null)
            bestOfWeekPost.GameText = "Сжигаю мага";

        if (diopsidePost != null)
        {
            SeedLatestRatedReviews(diopsidePost, roomToGameId, experiencedUsers, result);
        }

        if (bestOfWeekPost != null)
        {
            SeedBestOfWeekReviews(bestOfWeekPost, roomToGameId, experiencedUsers, result);
        }

        var masterPost = SeedMasterPost(finishedGameIds, rooms, roomToGameId, users, experiencedUsers, result);
        var longestUserPost = SeedLongestLoginPost(finishedGameIds, rooms, roomToGameId, users, experiencedUsers, result);

        var processedPostIds = new HashSet<Guid>();
        if (diopsidePost != null) processedPostIds.Add(diopsidePost.PostId);
        if (bestOfWeekPost != null) processedPostIds.Add(bestOfWeekPost.PostId);
        if (masterPost != null) processedPostIds.Add(masterPost.PostId);
        if (longestUserPost != null) processedPostIds.Add(longestUserPost.PostId);

        return processedPostIds;
    }

    /// <summary>
    /// "Latest rated": the Diopside post with several reviews, the newest an hour ago.
    /// </summary>
    private void SeedLatestRatedReviews(
        Post diopsidePost, Dictionary<Guid, Guid> roomToGameId, List<DbUser> experiencedUsers,
        ComprehensiveSeedResult result)
    {
        var gameId = roomToGameId.GetValueOrDefault(diopsidePost.RoomId);
        if (gameId != Guid.Empty)
        {
            var diopsideReviewers = experiencedUsers.Where(u => u.UserId != diopsidePost.AuthorId).Take(4).ToList();

            // 2 positive, 1 neutral, 1 negative; some with BBCode
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

            for (var i = 0; i < diopsideReviewers.Count && i < diopsideReviewData.Length; i++)
            {
                var reviewer = diopsideReviewers[i];
                var (sign, text, hoursAgo) = diopsideReviewData[i];
                AddPostReview(diopsidePost.PostId, diopsidePost.AuthorId, gameId, reviewer.UserId,
                    _now.AddHours(hoursAgo), text, sign, result);
            }
        }
    }

    /// <summary>
    /// "Best of week": another post with 5 reviews from earlier today.
    /// </summary>
    private void SeedBestOfWeekReviews(
        Post bestOfWeekPost, Dictionary<Guid, Guid> roomToGameId, List<DbUser> experiencedUsers,
        ComprehensiveSeedResult result)
    {
        var gameId = roomToGameId.GetValueOrDefault(bestOfWeekPost.RoomId);
        if (gameId != Guid.Empty)
        {
            var reviewCount = Math.Min(5, experiencedUsers.Count(u => u.UserId != bestOfWeekPost.AuthorId));
            var reviewersForBest = experiencedUsers.Where(u => u.UserId != bestOfWeekPost.AuthorId).Take(reviewCount).ToList();

            // All positive, so the master's post is clearly the best of the week
            var reviewData = new[]
            {
                (ReviewSign.Positive, "Отличный пост! Замечательный отыгрыш мастера, атмосфера на высоте."),
                (ReviewSign.Positive, "Очень атмосферно, браво! Мастер задал отличный тон сцене."),
                (ReviewSign.Positive, "Красивое описание, мне понравилось. Сразу чувствуется мастерство."),
                (ReviewSign.Positive, "Прекрасная подача! Мир оживает в каждой строчке."),
                (ReviewSign.Positive, "Лучший мастерский пост за последнее время, однозначно!"),
            };

            for (var i = 0; i < reviewersForBest.Count; i++)
            {
                var reviewer = reviewersForBest[i];
                var (sign, text) = reviewData[i % reviewData.Length];
                var reviewDate = _now.AddHours(-_random.Next(2, 13));
                AddPostReview(bestOfWeekPost.PostId, bestOfWeekPost.AuthorId, gameId, reviewer.UserId,
                    reviewDate, text, sign, result);
            }
        }
    }

    /// <summary>
    /// A master post of SolohinLex, without a character, in a finished game he masters: "Latest rated".
    /// </summary>
    private Post? SeedMasterPost(
        List<Guid> finishedGameIds, List<Room> rooms, Dictionary<Guid, Guid> roomToGameId, List<DbUser> users,
        List<DbUser> experiencedUsers, ComprehensiveSeedResult result)
    {
        var solohin = users.FirstOrDefault(u => u.Username == PrimaryLogin);
        Post? masterPost = null;
        if (solohin != null)
        {
            var masterGameEntry = _dbContext.ChangeTracker.Entries<DbGame>()
                .FirstOrDefault(e => finishedGameIds.Contains(e.Entity.GameId) && e.Entity.MasterId == solohin.UserId);
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
                    AuthorId = solohin.UserId,
                    CreatedUtc = _now.AddHours(-3),
                    GameText = "Отвоеванный у отца камин, 2 спальни, телевизор",
                };
                _dbContext.Set<Post>().Add(masterPost);
                result.PostsCreated++;

                // One positive review at now: the most recent review
                var masterReviewer = experiencedUsers.FirstOrDefault(u => u.UserId != solohin.UserId);
                if (masterReviewer != null)
                {
                    AddPostReview(masterPost.PostId, solohin.UserId, masterGameId, masterReviewer.UserId,
                        _now, "Атмосфера на высоте, сразу чувствуется стиль мастера!", ReviewSign.Positive, result); // Most recent review → "Latest rated"
                }
            }
        }

        return masterPost;
    }

    /// <summary>
    /// A rated post of LongestLoginPossible, for a long username and a long character name.
    /// </summary>
    private Post? SeedLongestLoginPost(
        List<Guid> finishedGameIds, List<Room> rooms, Dictionary<Guid, Guid> roomToGameId, List<DbUser> users,
        List<DbUser> experiencedUsers, ComprehensiveSeedResult result)
    {
        var longestUser = users.FirstOrDefault(u => u.Username == "LongestLoginPossible");
        Post? longestUserPost = null;
        if (longestUser != null && finishedGameIds.Count > 0)
        {
            var longestGameId = finishedGameIds[0];
            var longestRoom = rooms.FirstOrDefault(r => roomToGameId.GetValueOrDefault(r.RoomId) == longestGameId);
            if (longestRoom != null)
            {
                var longestChar = new Character
                {
                    CharacterId = _guidFactory.Create(),
                    GameId = longestGameId,
                    AuthorId = longestUser.UserId,
                    Status = CharacterStatus.Active,
                    CreatedUtc = _now.AddDays(-20),
                    Name = "Сэр Максимилиан фон Штернберг",
                };
                _dbContext.Set<Character>().Add(longestChar);
                AddCharacterSheetValues(longestChar.CharacterId,
                    appearance: "Высокий светловолосый мужчина в сияющих доспехах.");
                result.CharactersCreated++;

                longestUserPost = new Post
                {
                    PostId = _guidFactory.Create(),
                    RoomId = longestRoom.RoomId,
                    CharacterId = longestChar.CharacterId,
                    AuthorId = longestUser.UserId,
                    CreatedUtc = _now.AddHours(-6),
                    GameText = "Максимилиан поднял забрало и оглядел зал. Руны на платформе мерцали, и в их свете его доспехи отбрасывали мягкие блики на стены. Он покачал головой — за годы странствий он научился не доверять местам, которые выглядят слишком спокойно.",
                };
                _dbContext.Set<Post>().Add(longestUserPost);
                result.PostsCreated++;
                longestUser.QuantityRating++;

                var longestReviewers = experiencedUsers.Where(u => u.UserId != longestUser.UserId).Take(2).ToList();
                var longestReviewData = new[]
                {
                    (ReviewSign.Positive, "Хороший отыгрыш, чувствуется характер персонажа."),
                    (ReviewSign.Neutral, "Коротковато, но по делу."),
                };
                for (var i = 0; i < longestReviewers.Count && i < longestReviewData.Length; i++)
                {
                    var reviewer = longestReviewers[i];
                    var (sign, text) = longestReviewData[i];
                    AddPostReview(longestUserPost.PostId, longestUser.UserId, longestGameId, reviewer.UserId,
                        _now.AddHours(-_random.Next(1, 5)), text, sign, result);
                }
            }
        }

        return longestUserPost;
    }

    /// <summary>
    /// Chuck, a grapefruit-obsessed barbarian, in the first active recruiting game: his post outscores
    /// everything else of the week, "Best of week".
    /// </summary>
    private async Task SeedChuckPost(
        List<DbUser> users, List<DbUser> experiencedUsers, ComprehensiveSeedResult result)
    {
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
                // Not the master
                var chuckPlayer = users.FirstOrDefault(u =>
                    u.UserId != activeRecruitingGame.Entity.MasterId &&
                    u.Username != PrimaryLogin);

                if (chuckPlayer != null)
                {
                    var chuckChar = await SeedChuckCharacter(chuckGameId, chuckPlayer, result);

                    // Eight hours back, inside the rolling seven-day window of the homepage block.
                    var chuckPostCreatedUtc = _now.AddHours(-8);
                    var chuckPost = new Post
                    {
                        PostId = _guidFactory.Create(),
                        RoomId = chuckRoom.RoomId,
                        CharacterId = chuckChar.CharacterId,
                        AuthorId = chuckPlayer.UserId,
                        CreatedUtc = chuckPostCreatedUtc,
                        GameText = ChuckPostText,
                        MetagameText = "Это было прекрасно. У меня слезы на глазах.",
                    };
                    _dbContext.Set<Post>().Add(chuckPost);
                    result.PostsCreated++;
                    chuckPlayer.QuantityRating++;

                    SeedChuckReviews(chuckPost, chuckPlayer, chuckGameId, users, experiencedUsers, result);
                }
            }
        }
    }

    /// <summary>The character Chuck, with his avatar uploaded through the real pipeline.</summary>
    private async Task<Character> SeedChuckCharacter(
        Guid chuckGameId, DbUser chuckPlayer, ComprehensiveSeedResult result)
    {
        var chuckChar = new Character
        {
            CharacterId = _guidFactory.Create(),
            GameId = chuckGameId,
            AuthorId = chuckPlayer.UserId,
            Status = CharacterStatus.Active,
            CreatedUtc = _now.AddDays(-10),
            Name = "Чак",
        };
        _dbContext.Set<Character>().Add(chuckChar);
        AddCharacterSheetValues(chuckChar.CharacterId,
            appearance: "Коренастый мужчина с обветренным лицом и мощными руками. На поясе всегда висит мешочек с грейпфрутами.",
            temper: "Буйный, но добродушный. Впадает в ярость при виде несправедливости. И при виде апельсинов.",
            story: "Бывший фермер из долины Золотых Рощ, где выращивают лучшие грейпфруты континента. Ушел в приключенцы после того, как орки сожгли его плантацию.",
            skills: "Двуручное оружие, выживание, кулинария (грейпфрутовые блюда), запугивание.",
            inventory: "Двуручный топор, 3 грейпфрута, фляга с грейпфрутовым соком, потрепанная кулинарная книга.");
        result.CharactersCreated++;

        // Through the real pipeline
        var chuckBytes = ReadEmbeddedSeedBytes(ChuckImage);
        var chuckUpload = await SeedImageFromBytesAsync(
            chuckBytes,
            declaredContentType: "image/png",
            sourceFileName: "Chuck.png",
            type: UploadType.CharacterAvatar,
            uploadId: _guidFactory.Create(),
            userId: chuckPlayer.UserId,
            entityId: chuckChar.CharacterId,
            uploadedUtc: chuckChar.CreatedUtc);
        _dbContext.Uploads.Add(chuckUpload);

        return chuckChar;
    }

    /// <summary>Chuck's magnum opus about grapefruit.</summary>
    private const string ChuckPostText = """
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
        """;

    /// <summary>
    /// The reviews of Chuck's post, all positive, one more than leaderboard
    /// coverage puts on its top post.
    /// </summary>
    private void SeedChuckReviews(
        Post chuckPost, DbUser chuckPlayer, Guid chuckGameId, List<DbUser> users, List<DbUser> experiencedUsers,
        ComprehensiveSeedResult result)
    {
        var chuckPostCreatedUtc = chuckPost.CreatedUtc;

        // One above the coverage top (BoardSize + 2), from the same constant: in the first week of a month the
        // coverage reviews fall inside the weekly window too.
        var chuckReviewCount = Leaderboards.BoardSize + 3;

        // Experienced reviewers first, the rest only when there are not enough of them.
        var chuckReviewers = experiencedUsers
            .Concat(users.Except(experiencedUsers))
            .Where(u => u.UserId != chuckPlayer.UserId)
            .Take(chuckReviewCount)
            .ToList();

        if (chuckReviewers.Count < chuckReviewCount)
        {
            throw new InvalidOperationException(
                $"Showcase post needs {chuckReviewCount} distinct reviewers to outscore " +
                $"leaderboard coverage, and the seed has {chuckReviewers.Count}. " +
                "Add users, or the home page shows a filler post as the best of the week.");
        }
        // Strictly between the post and now: the fireplace review at now stays the freshest.
        var chuckReviewSpanMinutes =
            (int)Math.Max(2, (_now - chuckPostCreatedUtc).TotalMinutes);
        for (var i = 0; i < chuckReviewers.Count; i++)
        {
            AddPostReview(chuckPost.PostId, chuckPlayer.UserId, chuckGameId, chuckReviewers[i].UserId,
                chuckPostCreatedUtc.AddMinutes(_random.Next(1, chuckReviewSpanMinutes)), ChuckReviewTexts[i], ReviewSign.Positive, result);
        }
    }

    private static readonly string[] ChuckReviewTexts =
    [
        "Лучший пост, что я читал за последний год. Грейпфрут — это философия.",
        "Я буквально плакал от смеха. И от красоты. Одновременно.",
        "Чак — национальное достояние. Этот монолог надо высечь в камне.",
        "Подписываюсь под каждым словом. Яблоки — это скучно. Грейпфрут — это жизнь.",
        "Персонаж раскрыт на все сто. Глубина, юмор, драма — все в одном посте.",
        "Отыгрыш уровня бог. Сцена с трактирщиком — шедевр.",
        "Жду продолжения грейпфрутовой саги. Это лучше 'Властелина Колец'.",
        "Прочитал трижды. На третий раз пошел за грейпфрутом.",
        "Вот так и надо писать посты. Без пафоса, а берет за душу.",
        "Мастер, дайте этому человеку премию. И еще грейпфрутов.",
        "Никогда не думал, что буду сопереживать цитрусовым. А вот.",
        "Половина игры ради таких постов и существует.",
        "Сохранил себе. Буду показывать новичкам как образец.",
        "Тот случай, когда пост лучше самого модуля. Без обид, мастер.",
        "Читал в метро, чуть не проехал станцию.",
        "Единственная претензия: слишком коротко.",
    ];
}
