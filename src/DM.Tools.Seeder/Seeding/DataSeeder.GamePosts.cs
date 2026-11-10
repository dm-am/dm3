using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Game.Posts;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Tools.Seeder.Seeding;

internal sealed partial class DataSeeder
{
    // Large post for testing content expansion on homepage (~2000 chars).
    private const string DiopsideLargePostText = """
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

    private static readonly string[] GamePostTexts =
    [
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
    ];

    private static readonly string[] MasterPostTexts =
    [
        "Таинственная фигура выходит из тени. 'Вы пришли за ответами? Возможно, я могу помочь... за определенную цену.'",
        "Внезапно раздается громкий скрежет - каменная дверь начинает медленно опускаться!",
        "Свет факелов мерцает, и на мгновение вам кажется, что тени на стенах двигаются сами по себе.",
        "Издалека доносится приглушенный рев - что-то большое бродит в этих коридорах.",
        "На полу вы замечаете свежие следы крови, ведущие вглубь подземелья.",
    ];

    private async Task SeedGamePosts(
        SeedingGame seeding, Room mainRoom, GameCast cast, List<DbUser> users, GameShowcase showcase,
        ComprehensiveSeedResult result)
    {
        var (game, master, template, variation, _) = seeding;

        var allCharactersForPosts = cast.Characters.Where(c => c.Status == CharacterStatus.Active).ToList(); // Include NPC

        var postsToCreate = variation.Posts;
        if (postsToCreate > 0 && allCharactersForPosts.Count > 0)
        {
            var baseTime = game.ActivatedUtc!.Value;
            var endTime = game.ClosedUtc ?? _now;
            var totalHoursAvailable = (int)(endTime - baseTime).TotalHours;
            var hoursPerPost = Math.Max(1, totalHoursAvailable / postsToCreate);

            for (var pi = 0; pi < postsToCreate; pi++)
            {
                var character = allCharactersForPosts[pi % allCharactersForPosts.Count];
                var isMasterPost = character.IsNpc;
                var postOffsetHours = Math.Min(pi * hoursPerPost + _random.Next(0, hoursPerPost), totalHoursAvailable - 1);
                var useLargePost = !showcase.HasLargePost && pi == 0
                    && template.ClosedReason == ClosedReason.Finished && !isMasterPost;
                if (useLargePost) showcase.HasLargePost = true;

                var post = new Post
                {
                    PostId = _guidFactory.Create(),
                    RoomId = mainRoom.RoomId,
                    CharacterId = character.CharacterId,
                    AuthorId = character.AuthorId ?? master.UserId,
                    CreatedUtc = baseTime.AddHours(postOffsetHours),
                    GameText = useLargePost
                        ? DiopsideLargePostText
                        : isMasterPost
                            ? MasterPostTexts[_random.Next(MasterPostTexts.Length)]
                            : GamePostTexts[_random.Next(GamePostTexts.Length)],
                    MetagameText = useLargePost
                        ? "Пробный пост для тестирования отображения на главной. Вроде норм получилось!"
                        : _random.Next(4) == 0 ? "Интересный поворот!" : null,
                };
                _dbContext.Set<Post>().Add(post);
                result.PostsCreated++;

                if (useLargePost)
                {
                    await SeedPostAttachment(post);
                    SeedPostDiceRolls(post);
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

                users.First(u => u.UserId == post.AuthorId).QuantityRating++;
            }
        }
    }

    // One post carries attachments, so the post block, the moderation list and the serving endpoint
    // have one.
    private async Task SeedPostAttachment(Post post)
    {
        var attachmentBytes = ReadEmbeddedSeedBytes(DiopsideImage);
        var attachment = await SeedImageFromBytesAsync(
            attachmentBytes,
            declaredContentType: "image/jpeg",
            sourceFileName: "карта-подземелья.jpg",
            type: UploadType.PostAttachment,
            uploadId: _guidFactory.Create(),
            userId: post.AuthorId,
            entityId: post.PostId,
            uploadedUtc: post.CreatedUtc);
        _dbContext.Uploads.Add(attachment);
    }

    private void SeedPostDiceRolls(Post post)
    {
        _dbContext.DiceRolls.AddRange(
            new DiceRoll
            {
                DiceRollId = _guidFactory.Create(),
                PostId = post.PostId,
                CreatedUtc = post.CreatedUtc,
                DiceCount = 1,
                EdgesCount = 20,
                Bonus = 7,
                Comment = "Восприятие",
                Result = [new RollResult { Value = 18 }]
            },
            new DiceRoll
            {
                DiceRollId = _guidFactory.Create(),
                PostId = post.PostId,
                CreatedUtc = post.CreatedUtc.AddSeconds(1),
                DiceCount = 1,
                EdgesCount = 20,
                Bonus = 5,
                Comment = "Убеждение",
                Result = [new RollResult { Value = 14 }]
            });
    }
}
